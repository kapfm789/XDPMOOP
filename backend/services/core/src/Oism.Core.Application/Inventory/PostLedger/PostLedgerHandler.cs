using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory.PostLedger;

// Cửa duy nhất đổi on_hand (docs/architecture/transactions-and-concurrency.md mục "Một cửa duy nhất để đổi tồn"):
// khóa các dòng số dư, rồi mỗi bút toán đổi số dư và để lại đúng một dòng sổ.
// Không mở transaction: chạy trong transaction của use case gọi nó, sau khi use case đó đã khóa chứng từ.
public sealed class PostLedgerHandler(IInventoryRepository inventory, IClock clock)
{
    public async Task<IReadOnlyList<InventoryTransaction>> Handle(PostLedgerCommand command, CancellationToken ct)
    {
        var balances = await inventory.LockBalancesAsync(
            command.BranchId, command.Entries.Select(entry => entry.SkuId).ToHashSet(), ct);

        var now = clock.UtcNow;
        var posted = new List<InventoryTransaction>(command.Entries.Count);
        foreach (var entry in command.Entries)
        {
            var line = balances[entry.SkuId].Post(
                entry.Type, entry.Reason, entry.Quantity, entry.UnitCost,
                command.ReferenceType, command.ReferenceId, command.CreatedBy, now, entry.ReversalOfId);
            inventory.Add(line);
            posted.Add(line);
        }

        return posted;
    }
}
