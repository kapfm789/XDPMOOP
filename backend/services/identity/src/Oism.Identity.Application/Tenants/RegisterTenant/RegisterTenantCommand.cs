namespace Oism.Identity.Application.Tenants.RegisterTenant;

public sealed record RegisterTenantCommand(string TenantName, string OwnerName, string? Email, string? Phone, string Password);

public sealed record RegisterTenantResult(Guid TenantId, Guid UserId);
