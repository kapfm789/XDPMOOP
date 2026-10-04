using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oism.BuildingBlocks.Auth;
using Oism.Identity.Application.Auth;
using Oism.Identity.Application.Auth.GetMe;
using Oism.Identity.Application.Auth.Login;
using Oism.Identity.Application.Auth.Logout;
using Oism.Identity.Application.Auth.Refresh;

namespace Oism.Identity.Api.Controllers;

public sealed record LoginRequest(string Identifier, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

[ApiController]
public sealed class AuthController(LoginHandler login, RefreshHandler refresh, LogoutHandler logout, GetMeHandler getMe)
    : ControllerBase
{
    // Claim `sub` của JWT đã kiểm.
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await login.Handle(new LoginCommand(request.Identifier, request.Password), ct));

    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Refresh(RefreshTokenRequest request, CancellationToken ct) =>
        Ok(await refresh.Handle(new RefreshCommand(request.RefreshToken), ct));

    [HttpPost("auth/logout")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
    {
        await logout.Handle(new LogoutCommand(UserId, request.RefreshToken), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.AnyRole)]
    public async Task<ActionResult<MeDto>> Me(CancellationToken ct) => Ok(await getMe.Handle(UserId, ct));
}
