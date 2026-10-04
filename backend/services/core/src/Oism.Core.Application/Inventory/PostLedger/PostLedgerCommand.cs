using Oism.Core.Domain.Inventory;

namespace Oism.Core.Application.Inventory.PostLedger;

// Một bút toán của chứng từ: IN hoặc OUT, lý do, số lượng, đơn giá.
public sealed record LedgerEntry(
    Guid SkuId, LedgerType Type, LedgerReason Reason, int Quantity, decimal UnitCost, Guid? ReversalOfId = null);

// Các bút toán của một chứng từ tại một chi nhánh. CreatedBy null khi do job hệ thống.
public sealed record PostLedgerCommand(
    Guid BranchId, LedgerReference ReferenceType, Guid ReferenceId, IReadOnlyList<LedgerEntry> Entries, Guid? CreatedBy);
