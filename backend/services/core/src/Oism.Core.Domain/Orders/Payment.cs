using Oism.SharedKernel;

namespace Oism.Core.Domain.Orders;

public enum PaymentMethod
{
    Cash,
    QR,
}

// Thanh toán của đơn POS: docs/design/data-model/core.md mục "Chứng từ kho và bảng phụ".
// Thu ngân xác nhận đã nhận tiền trước khi bấm thanh toán; không có trạng thái chờ (ADR-0007).
public sealed class Payment : ITenantOwned
{
    private Payment()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid OrderId { get; private set; }

    public PaymentMethod Method { get; private set; }

    // Số tiền khách đưa; không nhỏ hơn tổng đơn.
    public decimal Amount { get; private set; }

    public Guid ConfirmedBy { get; private set; }

    public DateTimeOffset ConfirmedAt { get; private set; }

    internal static Payment Create(Guid orderId, PaymentMethod method, decimal amount, Guid confirmedBy, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        Method = method,
        Amount = amount,
        ConfirmedBy = confirmedBy,
        ConfirmedAt = now,
    };
}
