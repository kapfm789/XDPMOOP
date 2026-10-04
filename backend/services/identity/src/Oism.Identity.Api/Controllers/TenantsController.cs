using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.Identity.Application.Tenants.RegisterTenant;

namespace Oism.Identity.Api.Controllers;

public sealed record RegisterTenantRequest(string TenantName, string OwnerName, string? Email, string? Phone, string Password);

[ApiController]
[Route("tenants")]
public sealed class TenantsController(RegisterTenantHandler registerTenant) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterTenantResult>> Register(RegisterTenantRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await registerTenant.Handle(
            new RegisterTenantCommand(request.TenantName, request.OwnerName, request.Email, request.Phone, request.Password), ct));
}
