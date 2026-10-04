using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Oism.Catalog.Application;
using Oism.SharedKernel;

namespace Oism.Catalog.Infrastructure;

internal sealed class UnitOfWork(CatalogDbContext db) : IUnitOfWork
{
    public async Task<ITransaction> BeginAsync(CancellationToken ct) =>
        new Transaction(db, await db.Database.BeginTransactionAsync(ct));

    private sealed class Transaction(CatalogDbContext db, IDbContextTransaction transaction) : ITransaction
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
