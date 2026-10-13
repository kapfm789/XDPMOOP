using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oism.BuildingBlocks.Tenancy;
using Oism.Core.Application;
using Oism.Core.Application.Orders.CancelOrder;
using Oism.Core.Domain;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Infrastructure.Jobs;

// UC-ORD-05: hủy các đơn Reserved có phần giữ đã quá hạn. Trình tự: docs/design/flows/cancel-expire.md mục "Job hết hạn".
// Job chỉ đọc danh sách mà không khóa; việc khóa đơn và kiểm lại trạng thái nằm trong CancelOrderHandler,
// nên job và thao tác duyệt không cùng thắng trên một đơn (T07).
public sealed class ExpireReservationsJob(IServiceScopeFactory scopes, IClock clock, ILogger<ExpireReservationsJob> logger)
{
    public const string Id = "expire-reservations";

    public async Task RunAsync(CancellationToken ct)
    {
        // ponytail: mỗi lượt lấy hết đơn quá hạn và xử lý lần lượt; tồn đọng lớn tới mức một lượt dài hơn một phút
        // thì chia lô theo `Take` hoặc xử lý song song theo tenant.
        foreach (var (tenantId, orderId) in await ListExpiredAsync(ct))
        {
            try
            {
                // Mỗi đơn một scope: tenant context của chính đơn đó, một DbContext và một transaction riêng.
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
                await scope.ServiceProvider.GetRequiredService<CancelOrderHandler>()
                    .Handle(new CancelOrderCommand(orderId, OrderCancelReason.Expired), ct);
            }
            catch (InvalidStateTransitionException)
            {
                // Đơn vừa được duyệt giữa lúc job đọc và lúc job khóa được dòng đơn: bỏ qua.
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Transaction của đơn này đã rollback; các đơn sau vẫn được xử lý, đơn này chờ lần chạy sau.
                logger.LogError(exception, "Expiring order {OrderId} of tenant {TenantId} failed", orderId, tenantId);
            }
        }
    }

    // Một trong các chỗ được phép bỏ filter tenant: docs/architecture/multi-tenancy.md.
    private async Task<List<(Guid TenantId, Guid OrderId)>> ListExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var now = clock.UtcNow;
        var expired = await scope.ServiceProvider.GetRequiredService<CoreDbContext>().Reservations
            .IgnoreQueryFilters().AsNoTracking()
            .Where(hold => hold.Status == ReservationStatus.Active && hold.ExpiresAt < now)
            .Select(hold => new { hold.TenantId, hold.OrderId })
            .Distinct()
            .ToListAsync(ct);
        return expired.Select(hold => (hold.TenantId, hold.OrderId)).ToList();
    }
}

// Đặt lịch lúc service khởi động: job hết hạn chạy mỗi phút (ADR-0005).
internal sealed class JobSchedule(IRecurringJobManager jobs) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        jobs.AddOrUpdate<ExpireReservationsJob>(
            ExpireReservationsJob.Id, job => job.RunAsync(CancellationToken.None), Cron.Minutely());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
