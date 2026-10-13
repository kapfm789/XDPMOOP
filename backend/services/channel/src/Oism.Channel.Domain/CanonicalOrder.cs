namespace Oism.Channel.Domain;

// Payload của sàn không đổi được về Canonical Order: thiếu trường hoặc giá trị sai. Thông báo là lý do hiện cho người dùng
// trên bản ghi webhook Failed (docs/design/flows/webhook-ingestion.md mục "Trường hợp biên").
public sealed class InvalidPayloadException(string reason) : Exception(reason);

public sealed record CanonicalOrderLine(string SkuCode, int Quantity, decimal UnitPrice, decimal Discount);

// Đơn của mọi sàn sau khi adapter chuẩn hóa (FR-ORD-01): docs/design/api/channel.md mục "Ánh xạ về Canonical Order".
public sealed record CanonicalOrder
{
    private CanonicalOrder(string externalOrderId, DateTimeOffset placedAt, IReadOnlyList<CanonicalOrderLine> lines)
    {
        ExternalOrderId = externalOrderId;
        PlacedAt = placedAt;
        Lines = lines;
    }

    public string ExternalOrderId { get; }

    public DateTimeOffset PlacedAt { get; }

    public IReadOnlyList<CanonicalOrderLine> Lines { get; }

    // Nhận các giá trị adapter đọc được từ payload, còn có thể thiếu. Quy tắc của từng dòng khớp quy tắc `core` áp cho dòng đơn,
    // để đơn sai bị chặn ở đây thành Failed thay vì làm consumer SubmitOrder lỗi.
    public static CanonicalOrder From(
        string? externalOrderId,
        DateTimeOffset? placedAt,
        IEnumerable<(string? SkuCode, int? Quantity, decimal? UnitPrice, decimal? Discount)>? lines)
    {
        if (string.IsNullOrWhiteSpace(externalOrderId))
            throw new InvalidPayloadException("Thiếu mã đơn");
        if (placedAt is null)
            throw new InvalidPayloadException("Thiếu thời điểm đặt hàng");

        var items = new List<CanonicalOrderLine>();
        foreach (var (skuCode, quantity, unitPrice, discount) in lines ?? [])
        {
            var number = items.Count + 1;
            if (string.IsNullOrWhiteSpace(skuCode))
                throw new InvalidPayloadException($"Dòng {number}: thiếu mã SKU");
            if (quantity is not > 0)
                throw new InvalidPayloadException($"Dòng {number}: số lượng phải dương");
            if (unitPrice is not >= 0)
                throw new InvalidPayloadException($"Dòng {number}: thiếu đơn giá hoặc đơn giá âm");
            if (discount is < 0 || discount > quantity * unitPrice)
                throw new InvalidPayloadException($"Dòng {number}: giảm giá âm hoặc vượt thành tiền của dòng");

            items.Add(new CanonicalOrderLine(skuCode.Trim(), quantity.Value, unitPrice.Value, discount ?? 0));
        }

        if (items.Count == 0)
            throw new InvalidPayloadException("Đơn không có dòng hàng");

        return new CanonicalOrder(externalOrderId.Trim(), placedAt.Value, items);
    }
}
