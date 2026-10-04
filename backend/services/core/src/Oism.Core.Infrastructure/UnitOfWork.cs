using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Oism.Core.Application;
using Oism.SharedKernel;

namespace Oism.Core.Infrastructure;

internal sealed class UnitOfWork(CoreDbContext db) : IUnitOfWork
{
    private const string Savepoint = "use_case";

    // Use case gọi từ consumer chạy bên trong transaction mà consumer base đã mở và đã ghi inbox
    // (docs/architecture/messaging.md mục "Phía nhận"). Khi đó transaction của use case là một savepoint:
    // rollback chỉ bỏ phần của use case, dòng inbox ở lại và được commit cùng những gì consumer ghi sau đó.
    public async Task<ITransaction> BeginAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is { } outer)
        {
            await outer.CreateSavepointAsync(Savepoint, ct);
            return new Transaction(db, outer, nested: true);
        }

        return new Transaction(db, await db.Database.BeginTransactionAsync(ct), nested: false);
    }

    private sealed class Transaction(CoreDbContext db, IDbContextTransaction transaction, bool nested) : ITransaction
    {
        private bool _committed;

        public async Task SaveAsync(CancellationToken ct)
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
        }

        public async Task CommitAsync(CancellationToken ct)
        {
            await SaveAsync(ct);
            if (!nested)
                await transaction.CommitAsync(ct);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!nested)
            {
                await transaction.DisposeAsync();
                return;
            }

            if (_committed)
                return;

            await transaction.RollbackToSavepointAsync(Savepoint);
            // Entity của use case vừa rollback không được theo lần lưu sau của consumer.
            db.ChangeTracker.Clear();
        }
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
