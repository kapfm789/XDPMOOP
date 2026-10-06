using FluentValidation;
using Oism.Core.Application.Orders.ReserveStock;
using Oism.Core.Domain.Orders;

namespace Oism.Core.Application.Orders.PosCheckout;

// Method: Cash hoặc QR. Amount là số tiền khách đưa, không nhỏ hơn tổng đơn.
public sealed record PosPayment(string? Method, decimal Amount);

// IdempotencyKey lấy từ header `Idempotency-Key`, là một uuid do POS sinh cho mỗi lần bấm thanh toán.
public sealed record PosCheckoutCommand(
    string? IdempotencyKey,
    Guid BranchId,
    IReadOnlyList<ReserveStockLine> Items,
    PosPayment? Payment,
    Guid CashierId,
    bool IncludeCost);

// Created false khi khóa đã dùng: Order là đơn đã tạo từ lần gửi trước.
public sealed record PosCheckoutResult(OrderDto Order, bool Created);

public sealed class PosCheckoutValidator : AbstractValidator<PosCheckoutCommand>
{
    public PosCheckoutValidator()
    {
        RuleFor(command => command.IdempotencyKey)
            .Must(key => Guid.TryParse(key, out _)).WithMessage("Header Idempotency-Key phải là một uuid");
        RuleFor(command => command.BranchId).NotEmpty().WithMessage("Chọn chi nhánh");
        RuleFor(command => command.Items).NotEmpty().WithMessage("Giỏ hàng cần ít nhất một dòng");
        RuleForEach(command => command.Items).SetValidator(new ReserveStockLineValidator());
        RuleFor(command => command.Payment).NotNull().WithMessage("Thiếu thông tin thanh toán");
        RuleFor(command => command.Payment!.Method)
            .Must(method => Enum.GetNames<PaymentMethod>().Contains(method)).WithMessage("Phương thức là Cash hoặc QR")
            .When(command => command.Payment is not null);
    }
}
