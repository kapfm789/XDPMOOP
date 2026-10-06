using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.ReceiveTransfer;

public sealed record ReceiveTransferCommand(Guid TransferId, Guid? ReceivedBy, bool IncludeCost);

// UC-INV-03, bước nhận: một transaction ở nơi nhận. Trình tự: docs/design/flows/stock-transfer.md.
public sealed class ReceiveTransferHandler(
    IUnitOfWork unitOfWork,
    IStockTransferRepository transfers,
    PostLedgerHandler postLedger,
    IReferenceRepository references,
    IClock clock)
{
    public async Task<TransferDto> Handle(ReceiveTransferCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var transfer = await transfers.GetForUpdateAsync(command.TransferId, ct)
            ?? throw new NotFoundException("phiếu chuyển kho");

        // Phiếu đã Received: trả phiếu hiện có, không tác động lần hai (AC-6).
        if (transfer.Receive(clock.UtcNow))
        {
            // Nhận đủ số lượng đã xuất (ADR-0009). PostLedger khóa số dư nơi nhận, tính lại giá vốn bình quân nơi nhận
            // bằng đơn giá mang theo trên phiếu và ghi một dòng sổ IN lý do TransferIn cho mỗi dòng phiếu.
            var entries = transfer.Items
                .Select(item => new LedgerEntry(item.SkuId, LedgerType.IN, LedgerReason.TransferIn, item.Quantity, item.UnitCost))
                .ToList();
            await postLedger.Handle(
                new PostLedgerCommand(transfer.ToBranchId, LedgerReference.StockTransfer, transfer.Id, entries, command.ReceivedBy), ct);
        }

        await transaction.CommitAsync(ct);
        return await references.ToDtoAsync(transfer, command.IncludeCost, ct);
    }
}
