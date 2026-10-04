namespace Oism.Identity.Application.Users.UpdateUser;

public sealed record UpdateUserCommand(Guid Id, string FullName, string Role, Guid? BranchId, bool IsActive);
