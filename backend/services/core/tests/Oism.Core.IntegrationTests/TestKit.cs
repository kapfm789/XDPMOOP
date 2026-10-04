using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oism.BuildingBlocks.Messaging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Contracts;
using Oism.Core.Application;
using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Application.Orders.ReserveStock;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;
using Oism.Core.Infrastructure;

namespace Oism.Core.IntegrationTests;

internal static class TestKit
{
    // Mỗi lần gọi là một scope, tức một DbContext mới mang tenant của test, như một request.
    public static async Task<T> InTenantAsync<T>(this ApiFactory factory, Guid tenantId, Func<IServiceProvider, Task<T>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        return await run(scope.ServiceProvider);
    }

    public static Task<T> QueryAsync<T>(this ApiFactory factory, Guid tenantId, Func<CoreDbContext, Task<T>> query) =>
        factory.InTenantAsync(tenantId, services => query(services.GetRequiredService<CoreDbContext>()));

    // Bản sao chi nhánh, như consumer BranchUpserted dựng.
    public static async Task<Guid> SeedBranchAsync(this ApiFactory factory, Guid tenantId, bool isActive = true)
    {
        var branch = new BranchRef(Guid.NewGuid());
        branch.Apply("CH-01", "Cửa hàng 1", "Store", isActive, version: 1);
        await factory.QueryAsync(tenantId, db =>
        {
            db.Add(branch);
            return db.SaveChangesAsync();
        });
        return branch.BranchId;
    }

    // Bản sao SKU, như consumer SkuUpserted dựng.
    public static async Task<Guid> SeedSkuAsync(
        this ApiFactory factory, Guid tenantId, string skuCode, string name = "Áo thun", decimal retailPrice = 150_000, bool isActive = true)
    {
        var sku = new SkuRef(Guid.NewGuid());
        sku.Apply(skuCode, name, [], retailPrice, wholesalePrice: retailPrice, isActive, version: 1);
        await factory.QueryAsync(tenantId, db =>
        {
            db.Add(sku);
            return db.SaveChangesAsync();
        });
        return sku.SkuId;
    }

    public static LedgerEntry In(Guid skuId, int quantity, decimal unitCost = 100_000, LedgerReason reason = LedgerReason.Purchase) =>
        new(skuId, LedgerType.IN, reason, quantity, unitCost);

    public static LedgerEntry Out(Guid skuId, int quantity, decimal unitCost = 100_000, LedgerReason reason = LedgerReason.StocktakeAdjust) =>
        new(skuId, LedgerType.OUT, reason, quantity, unitCost);

    // Một use case gọi PostLedger: mở transaction, ghi các bút toán của một chứng từ, commit.
    public static Task<IReadOnlyList<InventoryTransaction>> PostLedgerAsync(
        this ApiFactory factory, Guid tenantId, Guid branchId, params LedgerEntry[] entries) =>
        factory.InTenantAsync(tenantId, async services =>
        {
            await using var transaction = await services.GetRequiredService<IUnitOfWork>().BeginAsync(default);
            var posted = await services.GetRequiredService<PostLedgerHandler>().Handle(
                new PostLedgerCommand(branchId, LedgerReference.PurchaseReceipt, Guid.NewGuid(), entries, CreatedBy: null), default);
            await transaction.CommitAsync(default);
            return posted;
        });

    public static Task<InventoryBalance?> BalanceAsync(this ApiFactory factory, Guid tenantId, Guid branchId, Guid skuId) =>
        factory.QueryAsync(tenantId, db => db.InventoryBalances.AsNoTracking()
            .SingleOrDefaultAsync(balance => balance.BranchId == branchId && balance.SkuId == skuId));

    // Sổ của tenant theo thứ tự ghi.
    public static Task<List<InventoryTransaction>> LedgerAsync(this ApiFactory factory, Guid tenantId) =>
        factory.QueryAsync(tenantId, db => db.InventoryTransactions.AsNoTracking().OrderBy(line => line.Seq).ToListAsync());

    public static Task<List<Order>> OrdersAsync(this ApiFactory factory, Guid tenantId) =>
        factory.QueryAsync(tenantId, db => db.Orders.AsNoTracking().Include(order => order.Items).ToListAsync());

    public static Task<List<Reservation>> ReservationsAsync(this ApiFactory factory, Guid tenantId) =>
        factory.QueryAsync(tenantId, db => db.Reservations.AsNoTracking().ToListAsync());

    // Đơn online như consumer SubmitOrder đưa vào: cùng handler, không qua RabbitMQ.
    public static Task SubmitOrderAsync(this ApiFactory factory, Guid tenantId, SubmitOrder message) =>
        factory.InTenantAsync(tenantId, async services =>
        {
            await services.GetRequiredService<ReserveStockHandler>().Handle(message, default);
            return true;
        });

    public static SubmitOrder Online(string externalOrderId, Guid branchId, params SubmitOrderLine[] lines) =>
        new("Shopee", externalOrderId, branchId, DateTimeOffset.UtcNow, lines);

    // Thông điệp loại TPayload trong outbox của tenant.
    public static async Task<List<TPayload>> OutboxAsync<TPayload>(this ApiFactory factory, Guid tenantId)
    {
        var messages = await factory.QueryAsync(tenantId, db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(message => message.Type == typeof(TPayload).Name).OrderBy(message => message.OccurredAt).ToListAsync());
        return messages.Select(message => JsonSerializer.Deserialize<TPayload>(message.Payload, EventEnvelope.JsonOptions)!).ToList();
    }

    // Kiểm mã HTTP và trường `code` của ProblemDetails (docs/design/api/README.md).
    public static async Task AssertProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    // Bất biến tồn kho của một tenant: docs/testing/strategy.md mục "Kiểm bất biến sau mỗi test của core".
    public static Task AssertInventoryInvariantsAsync(this ApiFactory factory, Guid tenantId) =>
        factory.QueryAsync(tenantId, async db =>
        {
            var balances = await db.InventoryBalances.AsNoTracking().ToListAsync();
            var ledger = await db.InventoryTransactions.AsNoTracking().OrderBy(line => line.Seq).ToListAsync();
            var held = await db.Reservations.AsNoTracking().Where(hold => hold.Status == ReservationStatus.Active).ToListAsync();

            foreach (var balance in balances)
            {
                // Sổ liền mạch: balance_after của mỗi dòng bằng dòng trước cộng hoặc trừ quantity.
                var running = 0;
                foreach (var line in ledger.Where(line => line.BranchId == balance.BranchId && line.SkuId == balance.SkuId))
                {
                    running += line.Type == LedgerType.IN ? line.Quantity : -line.Quantity;
                    Assert.Equal(running, line.BalanceAfter);
                }

                // Số dư khớp sổ: on_hand bằng tổng IN trừ tổng OUT.
                Assert.Equal(running, balance.OnHand);
                // Không âm.
                Assert.True(balance.OnHand >= 0 && balance.Reserved >= 0 && balance.Reserved <= balance.OnHand);
                // Phần giữ khớp số dư: reserved bằng tổng số lượng các phần giữ Active.
                Assert.Equal(
                    held.Where(hold => hold.BranchId == balance.BranchId && hold.SkuId == balance.SkuId).Sum(hold => hold.Quantity),
                    balance.Reserved);
            }

            // Không dòng sổ hay phần giữ nào nằm ngoài một dòng số dư.
            Assert.All(held, hold =>
                Assert.Contains(balances, balance => balance.BranchId == hold.BranchId && balance.SkuId == hold.SkuId));
            Assert.All(ledger, line =>
                Assert.Contains(balances, balance => balance.BranchId == line.BranchId && balance.SkuId == line.SkuId));
            return true;
        });
}
