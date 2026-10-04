using FluentValidation;
using Oism.Identity.Application.Users;
using Oism.SharedKernel;

namespace Oism.Identity.Application.Auth.Login;

// UC-AUTH-02: đăng nhập bằng email hoặc số điện thoại.
public sealed class LoginHandler(
    IUnitOfWork unitOfWork,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwords,
    IAccessTokenIssuer accessTokens,
    IClock clock)
{
    private static readonly LoginValidator Validator = new();

    public async Task<AuthResult> Handle(LoginCommand command, CancellationToken ct)
    {
        Validator.ValidateAndThrow(command);

        // Sai mật khẩu, không có tài khoản, tài khoản bị vô hiệu hóa: cùng một lỗi (AC-2, AC-6).
        var user = await users.FindByIdentifierAsync(command.Identifier, ct);
        if (user is null || !user.IsActive || !passwords.Verify(command.Password, user.PasswordHash))
            throw new UnauthenticatedException();

        await using var transaction = await unitOfWork.BeginAsync(ct);
        var result = AuthResult.Issue(user, accessTokens, refreshTokens, clock.UtcNow, out _);
        await transaction.CommitAsync(ct);
        return result;
    }
}
