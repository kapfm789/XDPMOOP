using FluentValidation;
using FluentValidation.Results;
using Oism.Core.Application.Orders.ReserveStock;
using Oism.Core.Application.References;
using Oism.Core.Domain.Orders;
using Oism.Core.Domain.References;

namespace Oism.Core.Application.Orders;

// Dựng đơn Draft theo Canonical Order từ các dòng do người dùng hoặc sàn gửi lên.
// Đơn thủ công, đơn online và đơn POS dùng chung: cùng cách tra chi nhánh, SKU và cùng cách chọn đơn giá.
internal static class DraftOrder
{
    public static async Task<Order> CreateAsync(
        IReferenceRepository references,
        Guid branchId,
        OrderChannel channel,
        string? externalOrderId,
        string? idempotencyKey,
        string? note,
        IReadOnlyList<ReserveStockLine> lines,
        Guid? createdBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Chi nhánh và SKU tra trong tenant hiện tại (docs/architecture/multi-tenancy.md quy tắc 4).
        var branch = await references.FindBranchAsync(branchId, ct)
            ?? throw new ReferenceNotReadyException("Chi nhánh", branchId);
        branch.EnsureActive();

        var skus = (await references.ListSkusAsync(lines.Select(line => line.SkuId).ToHashSet(), ct))
            .ToDictionary(sku => sku.SkuId);

        var order = Order.Create(branchId, channel, externalOrderId, idempotencyKey, note, createdBy, now);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!skus.TryGetValue(line.SkuId, out var sku))
                throw new ReferenceNotReadyException("SKU", line.SkuId);
            sku.EnsureActive();

            // UC-ORD-02 AC-3: không nhập đơn giá thì lấy giá lẻ hiện tại.
            var unitPrice = line.UnitPrice ?? sku.RetailPrice;
            var discount = line.Discount ?? 0;
            if (!OrderItem.IsValidDiscount(line.Quantity, unitPrice, discount))
            {
                throw new ValidationException(
                    [new ValidationFailure($"Items[{index}].Discount", "Giảm giá không được vượt thành tiền của dòng")]);
            }

            order.AddItem(sku.SkuId, sku.SkuCode, sku.Name, line.Quantity, unitPrice, discount);
        }

        return order;
    }
}
