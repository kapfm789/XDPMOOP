namespace Oism.Identity.Application.Auth.Logout;

public sealed record LogoutCommand(Guid UserId, string RefreshToken);

// UC-AUTH-02 AC-5: thu hồi refresh token. Access token còn hiệu lực tới khi hết hạn (ADR-0011).
public sealed class LogoutHandler(IUnitOfWork unitOfWork, IRefreshTokenRepository refreshTokens, IClock clock)
{
    public async Task Handle(LogoutCommand command, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginAsync(ct);

        // Token không có hoặc không phải của người gọi thì không làm gì: đăng xuất lặp lại vẫn thành công.
        var token = await refreshTokens.GetAsync(Domain.RefreshToken.Hash(command.RefreshToken), ct);
        if (token?.UserId == command.UserId)
            token.Revoke(clock.UtcNow);

        await transaction.CommitAsync(ct);
    }
}
