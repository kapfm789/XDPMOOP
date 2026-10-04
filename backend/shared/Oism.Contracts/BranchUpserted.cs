namespace Oism.Contracts;

// docs/design/events.md mục "BranchUpserted". Bên nhận bỏ qua bản có Version nhỏ hơn hoặc bằng bản đang giữ.
public sealed record BranchUpserted(Guid BranchId, string Code, string Name, string Type, bool IsActive, long Version);
