using FluentValidation;

namespace Oism.Identity.Application.Tenants.RegisterTenant;

public sealed class RegisterTenantValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantValidator()
    {
        RuleFor(command => command.TenantName).RequiredName();
        RuleFor(command => command.OwnerName).RequiredName();
        RuleFor(command => command.Email).OptionalEmail();
        RuleFor(command => command.Phone).OptionalPhone();
        RuleFor(command => command.Email)
            .Must((command, _) => AccountRules.HasContact(command.Email, command.Phone))
            .WithMessage(AccountRules.ContactRequired);
        RuleFor(command => command.Password).NewPassword();
    }
}
