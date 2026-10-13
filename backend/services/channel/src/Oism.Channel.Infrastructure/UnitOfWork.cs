using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Oism.BuildingBlocks.Messaging;
using Oism.Channel.Application;
using Oism.SharedKernel;

namespace Oism.Channel.Infrastructure;

internal sealed class UnitOfWork(ChannelDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginAsync(CancellationToken ct) =>
        new Transaction(db, await db.Database.BeginTransactionAsync(ct));

    private sealed class Transaction(ChannelDbContext db, IDbContextTransaction transaction) : ITransaction
    {
        public async Task CommitAsync(CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception)
                when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Chỉ mục unique là nơi quyết định trùng, kể cả khi hai request tới cùng lúc.
                throw new DuplicateException("Dữ liệu trùng với bản ghi đã có");
            }

            await transaction.CommitAsync(ct);
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}

// Thông điệp nằm trong DbContext của use case nên được lưu cùng transaction với thay đổi nghiệp vụ.
internal sealed class OutboxEventPublisher(ChannelDbContext db, IClock clock) : IEventPublisher
{
    public void Enqueue<TPayload>(TPayload payload)
        where TPayload : notnull => db.Add(OutboxMessage.Create(payload, clock.UtcNow));
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
