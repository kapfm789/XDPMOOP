namespace Oism.Identity.Application.Branches.UpsertBranch;

// Id null là tạo mới. Mã chi nhánh chỉ đặt lúc tạo; khi sửa thì Code bị bỏ qua.
public sealed record UpsertBranchCommand(Guid? Id, string? Code, string Name, string Type, string? Address);
