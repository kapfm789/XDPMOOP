using FluentValidation;

namespace Oism.Core.Application.Orders.ReserveStock;

// Một dòng đơn do người dùng gửi lên; đơn thủ công và POS checkout dùng chung.
public sealed class ReserveStockLineValidator : AbstractValidator<ReserveStockLine>
{
    public ReserveStockLineValidator()
    {
        RuleFor(item => item.SkuId).NotEmpty().WithMessage("Chọn SKU");
        RuleFor(item => item.Quantity).GreaterThan(0).WithMessage("Số lượng phải dương");
        RuleFor(item => item.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm");
        RuleFor(item => item.Discount).GreaterThanOrEqualTo(0).WithMessage("Giảm giá không được âm");
    }
}

public sealed class ReserveStockValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(command => command.Items).NotEmpty().WithMessage("Đơn cần ít nhất một dòng");
        RuleForEach(command => command.Items).SetValidator(new ReserveStockLineValidator());
    }
}
