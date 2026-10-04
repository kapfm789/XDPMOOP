using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Domain.Inventory;
using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.ConfirmPurchaseReceipt;

public sealed record ConfirmPurchaseReceiptCommand(Guid ReceiptId, Guid? ConfirmedBy);

// UC-INV-02: xác nhận phiếu nhập là một transaction. Trình tự: docs/design/flows/purchase-receipt.md.
public sealed class ConfirmPurchaseReceiptHandler(
    IUnitOfWork unitOfWork,
    IPurchaseReceiptRepository receipts,
    PostLedgerHandler postLedger,
    IStockQueries queries,
    IClock clock)
{
    public async Task<PurchaseReceiptDto> Handle(ConfirmPurchaseReceiptCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var receipt = await receipts.GetForUpdateAsync(command.ReceiptId, ct)
            ?? throw new NotFoundException("phiếu nhập");

        // Phiếu đã Confirmed: trả phiếu hiện có, không tác động lần hai (AC-5).
        if (receipt.Confirm(command.ConfirmedBy, clock.UtcNow))
        {
            // PostLedger khóa các dòng số dư theo sku_id, rồi mỗi dòng phiếu tính lại giá vốn bình quân, tăng on_hand
            // và để lại một dòng sổ IN lý do Purchase. Cùng một SKU ở hai dòng phiếu: dòng sau dùng kết quả của dòng trước.
            var entries = receipt.Items
                .Select(item => new LedgerEntry(item.SkuId, LedgerType.IN, LedgerReason.Purchase, item.Quantity, item.UnitCost))
                .ToList();
            await postLedger.Handle(
                new PostLedgerCommand(receipt.BranchId, LedgerReference.PurchaseReceipt, receipt.Id, entries, command.ConfirmedBy), ct);
        }

        await transaction.CommitAsync(ct);
        return (await queries.FindPurchaseReceiptAsync(receipt.Id, ct))!;
    }
}
