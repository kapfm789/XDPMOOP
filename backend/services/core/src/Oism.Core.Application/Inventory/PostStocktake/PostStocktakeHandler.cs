using Oism.Core.Application.Inventory.PostLedger;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.PostStocktake;

public sealed record PostStocktakeCommand(Guid StocktakeId, Guid? PostedBy);

// UC-INV-04: chốt phiên kiểm kê trong một transaction cho cả phiên: hoặc mọi SKU được điều chỉnh, hoặc không SKU nào.
// Trình tự: docs/design/flows/stocktake.md.
public sealed class PostStocktakeHandler(
    IUnitOfWork unitOfWork,
    IStocktakeRepository stocktakes,
    IInventoryRepository inventory,
    PostLedgerHandler postLedger,
    IReferenceRepository references,
    IClock clock)
{
    public async Task<StocktakeDto> Handle(PostStocktakeCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var stocktake = await stocktakes.GetForUpdateAsync(command.StocktakeId, ct)
            ?? throw new NotFoundException("phiên kiểm kê");

        // Phiên đã Posted: trả phiên hiện có, không tác động lần hai (AC-5).
        if (stocktake.Status == StocktakeStatus.Open)
        {
            // Số trên sổ và lượng đang giữ đọc dưới khóa, lúc chốt: bán hàng giữa lúc đếm và lúc chốt đã được tính vào.
            var balances = await inventory.LockBalancesAsync(
                stocktake.BranchId, stocktake.Items.Select(item => item.SkuId).ToHashSet(), ct);

            // AC-4: một SKU đếm thấp hơn lượng đang giữ thì cả phiên bị từ chối, kèm các đơn đang giữ SKU đó.
            var below = stocktake.FindBelowReserved(balances);
            if (below.Count > 0)
            {
                var held = await stocktakes.ListHeldOrdersAsync(stocktake.BranchId, below.Select(item => item.SkuId).ToHashSet(), ct);
                throw new StocktakeBelowReservedException(below
                    .Select(item => new StocktakeShortfall(
                        item.SkuId, item.CountedQty, balances[item.SkuId].Reserved, held[item.SkuId].ToList()))
                    .ToList());
            }

            stocktake.Post(balances, clock.UtcNow);

            // SKU đếm bằng sổ thì không ghi gì (AC-3). Đơn giá bỏ trống: dòng sổ mang giá vốn bình quân hiện tại,
            // và bút toán StocktakeAdjust không tính lại giá vốn (AC-6).
            var entries = stocktake.Items
                .Where(item => item.Difference != 0)
                .Select(item => new LedgerEntry(
                    item.SkuId, item.Difference > 0 ? LedgerType.IN : LedgerType.OUT, LedgerReason.StocktakeAdjust,
                    Math.Abs(item.Difference!.Value), UnitCost: null))
                .ToList();
            if (entries.Count > 0)
            {
                await postLedger.Handle(
                    new PostLedgerCommand(stocktake.BranchId, LedgerReference.Stocktake, stocktake.Id, entries, command.PostedBy), ct);
            }
        }

        await transaction.CommitAsync(ct);
        return await references.ToDtoAsync(stocktake, ct);
    }
}
