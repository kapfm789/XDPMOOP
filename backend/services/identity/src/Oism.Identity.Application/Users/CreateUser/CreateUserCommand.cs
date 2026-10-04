namespace Oism.Identity.Application.Users.CreateUser;

public sealed record CreateUserCommand(string FullName, string? Email, string? Phone, string Password, string Role, Guid? BranchId);
