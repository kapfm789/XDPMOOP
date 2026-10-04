using FluentValidation;

namespace Oism.Core.Application.Orders.ReserveStock;

public sealed class ReserveStockValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(command => command.Items).NotEmpty().WithMessage("Đơn cần ít nhất một dòng");
        RuleForEach(command => command.Items).ChildRules(line =>
        {
            line.RuleFor(item => item.SkuId).NotEmpty().WithMessage("Chọn SKU");
            line.RuleFor(item => item.Quantity).GreaterThan(0).WithMessage("Số lượng phải dương");
            line.RuleFor(item => item.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm");
            line.RuleFor(item => item.Discount).GreaterThanOrEqualTo(0).WithMessage("Giảm giá không được âm");
        });
    }
}
