using Oism.SharedKernel;

namespace Oism.Core.Domain.References;

// SKU hoặc chi nhánh chưa có bản sao ở `core`: event chưa tới, giao diện tự thử lại
// (docs/architecture/messaging.md mục "Hệ quả phải chấp nhận"). ID của tenant khác cũng rơi vào đây,
// vì `core` không phân biệt được "chưa tới" với "không phải của tenant này".
public sealed class ReferenceNotReadyException(string resource, Guid id)
    : OismException("reference_not_ready", $"{resource} chưa được đồng bộ tới kho, thử lại sau", new { id });

// Chi nhánh đã tắt hoặc SKU đã ngừng bán. `reason` dùng cùng giá trị với OrderRejected (docs/design/events.md).
public sealed class InactiveReferenceException(string reason, Guid id)
    : OismException("inactive_reference", "Chi nhánh đã tắt hoặc SKU đã ngừng bán", new { reason, id })
{
    public string Reason { get; } = reason;

    public Guid Id { get; } = id;
}
