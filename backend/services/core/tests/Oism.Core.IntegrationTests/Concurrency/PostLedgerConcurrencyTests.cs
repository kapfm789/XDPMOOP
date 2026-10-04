using static Oism.Core.IntegrationTests.TestKit;

namespace Oism.Core.IntegrationTests.Concurrency;

public sealed class PostLedgerConcurrencyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Các transaction cùng đổi một dòng số dư phải xếp hàng ở khóa dòng: không lần đổi nào bị ghi đè,
    // và balance_after tính dưới khóa nên sổ liền mạch theo seq. Dòng số dư chưa có nên cả 20 cùng tranh tạo nó.
    [Fact]
    [Trait("UseCase", "UC-INV-01 AC-3")]
    public async Task PostLedger_TwentyTransactionsOnOneBalanceAtOnce_NoChangeIsLostAndTheLedgerStaysContinuous()
    {
        var (tenantId, branchId, sku) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // Khởi động service và chạy migration trước, để 20 luồng chỉ tranh nhau ở dòng số dư.
        factory.CreateClient().Dispose();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await gate.Task;
            await factory.PostLedgerAsync(tenantId, branchId, In(sku, 1));
        }).ToArray();
        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.Equal((20, 20L), await OnHandAndVersionAsync());
        Assert.Equal(Enumerable.Range(1, 20), (await factory.LedgerAsync(tenantId)).Select(line => line.BalanceAfter));
        await factory.AssertInventoryInvariantsAsync(tenantId);

        async Task<(int, long)> OnHandAndVersionAsync()
        {
            var balance = (await factory.BalanceAsync(tenantId, branchId, sku))!;
            return (balance.OnHand, balance.Version);
        }
    }
}
