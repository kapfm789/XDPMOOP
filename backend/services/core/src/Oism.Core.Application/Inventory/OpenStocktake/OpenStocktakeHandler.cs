using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Inventory.OpenStocktake;

public sealed record OpenStocktakeCommand(Guid BranchId, Guid? CreatedBy);

// UC-INV-04: mở phiên kiểm kê ở Open cho một chi nhánh; tồn chưa bị tác động tới khi chốt.
public sealed class OpenStocktakeHandler(
    IUnitOfWork unitOfWork, IStocktakeRepository stocktakes, IReferenceRepository references)
{
    public async Task<StocktakeDto> Handle(OpenStocktakeCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var branch = await references.FindBranchAsync(command.BranchId, ct)
            ?? throw new ReferenceNotReadyException("Chi nhánh", command.BranchId);
        branch.EnsureActive();

        var stocktake = Stocktake.Open(command.BranchId, command.CreatedBy);
        stocktakes.Add(stocktake);

        await transaction.CommitAsync(ct);
        return await references.ToDtoAsync(stocktake, ct);
    }
}
