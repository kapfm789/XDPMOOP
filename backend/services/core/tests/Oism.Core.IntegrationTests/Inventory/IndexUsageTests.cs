using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Oism.Core.IntegrationTests.Inventory;

// W2-03 (NFR-TENANT-02): sổ và đơn có chỉ mục bắt đầu bằng tenant_id, và PostgreSQL dùng chúng cho các truy vấn
// lọc theo tenant và thời gian, theo tenant, chi nhánh và SKU. Chỉ mục: docs/design/data-model/core.md.
public sealed class IndexUsageTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Tenant, chi nhánh và SKU của dữ liệu nền, tính lại được ngay trong câu truy vấn.
    private const string Tenant = "md5('tenant-7')::uuid";
    private const string Branch = "md5('branch-7')::uuid";
    private const string Sku = "md5('sku-7-2')::uuid";

    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-2")]
    public async Task LedgerAndOrders_QueriesByTenantAndTimeOrByTenantBranchAndSku_UseTheirIndexes()
    {
        // Khởi động service để chạy migration.
        factory.CreateClient().Dispose();
        await using var connection = new NpgsqlConnection(factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Oism"));
        await connection.OpenAsync();

        // Dữ liệu nền của 200 tenant, 20.000 dòng mỗi bảng, chèn thẳng bằng SQL: bảng đủ lớn thì planner mới cân nhắc chỉ mục.
        // Đây là dữ liệu để đo kế hoạch truy vấn, không phải nghiệp vụ, nên không đi qua PostLedger.
        await ExecuteAsync(connection,
            """
            INSERT INTO core.inventory_transactions
              (id, tenant_id, branch_id, sku_id, type, reason, quantity, balance_after, unit_cost, reference_type, reference_id, created_at)
            SELECT gen_random_uuid(), md5('tenant-' || g % 200)::uuid, md5('branch-' || g % 200)::uuid, md5('sku-' || g % 200 || '-' || g % 5)::uuid,
                   'IN', 'Purchase', 1, 1, 0, 'PurchaseReceipt', gen_random_uuid(), now() - make_interval(mins => g)
            FROM generate_series(1, 20000) AS g;

            INSERT INTO core.orders (id, tenant_id, branch_id, order_number, channel, status, total_amount, created_at)
            SELECT md5('order-' || g)::uuid, md5('tenant-' || g % 200)::uuid, md5('branch-' || g % 200)::uuid, 'DH' || g,
                   'Admin', 'Reserved', 0, now() - make_interval(mins => g)
            FROM generate_series(1, 20000) AS g;

            INSERT INTO core.order_items (id, tenant_id, order_id, sku_id, sku_code, sku_name, quantity, unit_price, discount)
            SELECT gen_random_uuid(), md5('tenant-' || g % 200)::uuid, md5('order-' || g)::uuid, md5('sku-' || g % 200 || '-' || g % 5)::uuid,
                   'SKU', 'SKU', 1, 0, 0
            FROM generate_series(1, 20000) AS g;

            ANALYZE core.inventory_transactions;
            ANALYZE core.orders;
            ANALYZE core.order_items;
            """);

        // Sổ theo tenant và khoảng thời gian (GET /ledger với from, to).
        Assert.Contains("ix_inventory_transactions_tenant_id_created_at", await ExplainAsync(connection,
            $"SELECT * FROM core.inventory_transactions WHERE tenant_id = {Tenant} AND created_at >= now() - interval '1 day' AND created_at <= now()"));
        // Sổ của một SKU tại một chi nhánh theo thứ tự ghi (GET /ledger với branchId, skuId).
        Assert.Contains("ix_inventory_transactions_tenant_id_branch_id_sku_id_seq", await ExplainAsync(connection,
            $"SELECT * FROM core.inventory_transactions WHERE tenant_id = {Tenant} AND branch_id = {Branch} AND sku_id = {Sku} ORDER BY seq"));
        // Đơn theo tenant và khoảng thời gian.
        Assert.Contains("ix_orders_tenant_id_created_at", await ExplainAsync(connection,
            $"SELECT * FROM core.orders WHERE tenant_id = {Tenant} AND created_at >= now() - interval '1 day'"));
        // Dòng đơn theo tenant và SKU.
        Assert.Contains("ix_order_items_tenant_id_sku_id", await ExplainAsync(connection,
            $"SELECT * FROM core.order_items WHERE tenant_id = {Tenant} AND sku_id = {Sku}"));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ExplainAsync(NpgsqlConnection connection, string query)
    {
        await using var command = new NpgsqlCommand($"EXPLAIN {query}", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var plan = new List<string>();
        while (await reader.ReadAsync())
            plan.Add(reader.GetString(0));
        return string.Join('\n', plan);
    }
}
