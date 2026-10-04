using FluentValidation;
using Oism.Catalog.Domain;
using Oism.SharedKernel;

namespace Oism.Catalog.Application.Brands.UpsertBrand;

public sealed class UpsertBrandHandler(IUnitOfWork unitOfWork, IBrandRepository brands)
{
    private static readonly UpsertBrandValidator Validator = new();

    public async Task<BrandDto> Handle(UpsertBrandCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);
        var name = command.Name.Trim();

        await using var transaction = await unitOfWork.BeginAsync(ct);

        Brand brand;
        if (command.Id is { } id)
        {
            brand = await brands.GetAsync(id, ct) ?? throw new NotFoundException("thương hiệu");
            brand.Rename(name);
        }
        else
        {
            brand = new Brand(name);
            brands.Add(brand);
        }

        // Trùng tên trong tenant (UC-PROD-01 AC-4): chỉ mục unique từ chối, CommitAsync ném DuplicateException.
        await transaction.CommitAsync(ct);
        return new BrandDto(brand.Id, brand.Name);
    }
}
