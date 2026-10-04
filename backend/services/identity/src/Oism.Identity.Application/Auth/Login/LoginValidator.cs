using FluentValidation;

namespace Oism.Identity.Application.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Identifier).NotEmpty().WithMessage("Không được để trống");
        RuleFor(command => command.Password).NotEmpty().WithMessage("Không được để trống");
    }
}
