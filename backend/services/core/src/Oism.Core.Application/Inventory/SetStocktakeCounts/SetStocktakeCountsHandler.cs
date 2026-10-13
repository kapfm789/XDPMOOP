using FluentValidation;
using Oism.Core.Application.References;
using Oism.Core.Domain.Inventory;
using Oism.Core.Domain.References;
using Oism.SharedKernel;

namespace Oism.Core.Application.Inventory.SetStocktakeCounts;

public sealed record SetStocktakeCountsCommand(Guid StocktakeId, IReadOnlyList<StocktakeCount> Items);

public sealed class SetStocktakeCountsValidator : AbstractValidator<SetStocktakeCountsCommand>
{
    public SetStocktakeCountsValidator()
    {
        RuleFor(command => command.Items).NotEmpty().WithMessage("Phiên cần ít nhất một dòng số đếm");
        RuleFor(command => command.Items)
            .Must(items => items.Select(item => item.SkuId).Distinct().Count() == items.Count)
            .WithMessage("Mỗi SKU chỉ được một dòng số đếm");
        RuleForEach(command => command.Items).ChildRules(line =>
        {
            line.RuleFor(item => item.SkuId).NotEmpty().WithMessage("Chọn SKU");
            line.RuleFor(item => item.CountedQty).GreaterThanOrEqualTo(0).WithMessage("Số đếm không được âm");
        });
    }
}

// UC-INV-04 AC-1: lưu số đếm của phiên còn Open, thay cả bộ số đếm cũ; tồn chưa bị tác động tới khi chốt.
public sealed class SetStocktakeCountsHandler(
    IUnitOfWork unitOfWork, IStocktakeRepository stocktakes, IReferenceRepository references)
{
    private static readonly SetStocktakeCountsValidator Validator = new();

    public async Task<StocktakeDto> Handle(SetStocktakeCountsCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var stocktake = await stocktakes.GetForUpdateAsync(command.StocktakeId, ct)
            ?? throw new NotFoundException("phiên kiểm kê");

        // SKU không có trong sku_refs bị từ chối ngay lúc nhập số đếm, không đợi tới lúc chốt.
        var known = (await references.ListSkusAsync(command.Items.Select(item => item.SkuId).ToHashSet(), ct))
            .Select(sku => sku.SkuId).ToHashSet();
        if (command.Items.FirstOrDefault(item => !known.Contains(item.SkuId)) is { } unknown)
            throw new ReferenceNotReadyException("SKU", unknown.SkuId);

        stocktake.SetCounts(command.Items);

        await transaction.CommitAsync(ct);
        return await references.ToDtoAsync(stocktake, ct);
    }
}
