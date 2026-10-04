using Oism.Identity.Application.Users;
using Oism.SharedKernel;

namespace Oism.Identity.Application.Auth.Refresh;

// UC-AUTH-02: đổi refresh token lấy cặp token mới; token cũ hết dùng được.
public sealed class RefreshHandler(
    IUnitOfWork unitOfWork,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IAccessTokenIssuer accessTokens,
    IClock clock)
{
    public async Task<AuthResult> Handle(RefreshCommand command, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await using var transaction = await unitOfWork.BeginAsync(ct);

        var current = await refreshTokens.FindForUpdateAsync(Domain.RefreshToken.Hash(command.RefreshToken), ct)
            ?? throw new UnauthenticatedException();

        if (current.WasReplaced)
        {
            // Token đã xoay vòng mà còn được dùng lại: coi như bị lộ, thu hồi mọi refresh token của người dùng (AC-4).
            // Việc thu hồi phải được lưu dù request này bị từ chối.
            await refreshTokens.RevokeAllAsync(current.UserId, now, ct);
            await transaction.CommitAsync(ct);
            throw new UnauthenticatedException();
        }

        var user = await users.GetAsync(current.UserId, ct);
        if (!current.IsUsable(now) || user is not { IsActive: true })
            throw new UnauthenticatedException();

        var result = AuthResult.Issue(user, accessTokens, refreshTokens, now, out var next);
        current.ReplaceWith(next, now);

        await transaction.CommitAsync(ct);
        return result;
    }
}
