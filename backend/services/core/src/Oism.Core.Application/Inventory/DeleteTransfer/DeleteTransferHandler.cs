using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.DeleteTransfer;

public sealed record DeleteTransferCommand(Guid TransferId);

// UC-INV-03: phiếu chuyển kho còn Draft chưa tác động tồn nên xóa được; phiếu đã xuất thì không (ADR-0009).
public sealed class DeleteTransferHandler(IUnitOfWork unitOfWork, IStockTransferRepository transfers)
{
    public async Task Handle(DeleteTransferCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var transfer = await transfers.GetForUpdateAsync(command.TransferId, ct)
            ?? throw new NotFoundException("phiếu chuyển kho");
        transfer.EnsureCanDelete();
        transfers.Remove(transfer);

        await transaction.CommitAsync(ct);
    }
}
