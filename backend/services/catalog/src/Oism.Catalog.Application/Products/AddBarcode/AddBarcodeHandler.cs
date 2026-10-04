using FluentValidation;
using Oism.Catalog.Domain;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Products.AddBarcode;

// Code trống với Symbology EAN13 là yêu cầu tự sinh.
public sealed record AddBarcodeCommand(Guid SkuId, string Symbology, string? Code);

public sealed class AddBarcodeValidator : AbstractValidator<AddBarcodeCommand>
{
    public AddBarcodeValidator()
    {
        RuleFor(command => command.Symbology)
            .Must(symbology => symbology is nameof(BarcodeSymbology.EAN13) or nameof(BarcodeSymbology.Code128))
            .WithMessage("Loại mã vạch phải là EAN13 hoặc Code128");

        // UC-PROD-03 AC-2: EAN-13 nhập tay phải đúng số kiểm tra.
        RuleFor(command => command.Code)
            .Must(code => Ean13.IsValid(code!.Trim()))
            .WithMessage("Mã EAN-13 gồm 13 chữ số và phải đúng số kiểm tra")
            .When(command => command.Symbology == nameof(BarcodeSymbology.EAN13) && !string.IsNullOrWhiteSpace(command.Code));

        // UC-PROD-03 AC-3: Code128 nhập tay là chuỗi chữ và số tới 48 ký tự.
        RuleFor(command => command.Code)
            .Must(code => !string.IsNullOrWhiteSpace(code) && code.Trim() is { Length: <= 48 } trimmed && trimmed.All(char.IsAsciiLetterOrDigit))
            .WithMessage("Mã Code128 gồm chữ và số, tối đa 48 ký tự")
            .When(command => command.Symbology == nameof(BarcodeSymbology.Code128));
    }
}

// UC-PROD-03: gán mã vạch cho SKU, tự sinh EAN-13 hoặc nhập tay.
public sealed class AddBarcodeHandler(IUnitOfWork unitOfWork, IProductRepository products, IEventPublisher events)
{
    private static readonly AddBarcodeValidator Validator = new();

    public async Task<BarcodeDto> Handle(AddBarcodeCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        await using var transaction = await unitOfWork.BeginAsync(ct);

        var product = await products.GetBySkuForUpdateAsync(command.SkuId, ct) ?? throw new NotFoundException("SKU");
        var sku = product.Skus.Single(candidate => candidate.Id == command.SkuId);

        var code = string.IsNullOrWhiteSpace(command.Code)
            ? Ean13.Generate(await products.NextEan13SequenceAsync(ct))
            : command.Code.Trim();
        var barcode = sku.AddBarcode(code, Enum.Parse<BarcodeSymbology>(command.Symbology));
        events.EnqueueSkuUpserted(product, sku);

        // Mã vạch đã có trong tenant (UC-PROD-03 AC-4): chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return BarcodeDto.From(barcode);
    }
}
