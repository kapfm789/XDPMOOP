using Oism.Identity.Domain;

namespace Oism.Identity.Application.Auth;

public sealed record AuthUser(Guid Id, string FullName, string Role, Guid? BranchId);

// Phản hồi của đăng nhập và làm mới: docs/design/api/identity.md.
public sealed record AuthResult(string AccessToken, int ExpiresIn, string RefreshToken, AuthUser User)
{
    // Cấp access token và một refresh token mới cho người dùng. `issued` là bản ghi refresh token vừa thêm.
    internal static AuthResult Issue(
        User user, IAccessTokenIssuer accessTokens, IRefreshTokenRepository refreshTokens, DateTimeOffset now,
        out RefreshToken issued)
    {
        issued = Domain.RefreshToken.Issue(user.Id, now, out var refreshToken);
        refreshTokens.Add(issued);

        var accessToken = accessTokens.Issue(user, now);
        return new AuthResult(
            accessToken.Value, accessToken.ExpiresIn, refreshToken,
            new AuthUser(user.Id, user.FullName, user.Role.ToString(), user.BranchId));
    }
}
