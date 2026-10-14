using Microsoft.EntityFrameworkCore.Storage;
using Oism.Insights.Application;

namespace Oism.Insights.Infrastructure;

internal sealed class UnitOfWork(InsightsDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginAsync(CancellationToken ct) =>
        new Transaction(db, await db.Database.BeginTransactionAsync(ct));

    private sealed class Transaction(InsightsDbContext db, IDbContextTransaction transaction) : ITransaction
    {
        public async Task CommitAsync(CancellationToken ct)
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

internal sealed class SystemClock : IClock
{
    // PostgreSQL giữ thời gian tới micro giây. Cắt ngay ở đây để giá trị trả về lúc tạo bằng đúng giá trị đọc lại sau đó.
    public DateTimeOffset UtcNow
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
        }
    }
}
