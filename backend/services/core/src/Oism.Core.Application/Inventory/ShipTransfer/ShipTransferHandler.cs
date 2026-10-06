using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.ShipTransfer;

public sealed record ShipTransferCommand(Guid TransferId, Guid? ShippedBy, bool IncludeCost);

// UC-INV-03, bước xuất: một transaction ở nơi gửi. Trình tự: docs/design/flows/stock-transfer.md.
public sealed class ShipTransferHandler(
    IUnitOfWork unitOfWork,
    IStockTransferRepository transfers,
    PostLedgerHandler postLedger,
    IReferenceRepository references,
    IClock clock)
{
    public async Task<TransferDto> Handle(ShipTransferCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var transfer = await transfers.GetForUpdateAsync(command.TransferId, ct)
            ?? throw new NotFoundException("phiếu chuyển kho");
        transfer.Ship(clock.UtcNow);

        // PostLedger khóa số dư nơi gửi theo sku_id, rồi mỗi dòng phiếu ghi một dòng sổ OUT lý do TransferOut với đơn giá
        // là giá vốn bình quân nơi gửi. Xuất quá tồn khả dụng thì ném InsufficientStockException: hàng đang giữ cho đơn
        // không được chuyển đi (AC-2).
        var items = transfer.Items.ToList();
        var posted = await postLedger.Handle(
            new PostLedgerCommand(
                transfer.FromBranchId, LedgerReference.StockTransfer, transfer.Id,
                items.Select(item => new LedgerEntry(item.SkuId, LedgerType.OUT, LedgerReason.TransferOut, item.Quantity, UnitCost: null)).ToList(),
                command.ShippedBy),
            ct);
        transfer.SnapshotCosts(items.Zip(posted).ToDictionary(pair => pair.First.Id, pair => pair.Second.UnitCost));

        await transaction.CommitAsync(ct);
        return await references.ToDtoAsync(transfer, command.IncludeCost, ct);
    }
}
