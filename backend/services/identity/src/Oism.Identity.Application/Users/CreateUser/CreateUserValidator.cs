using FluentValidation;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Users.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(command => command.FullName).RequiredName();
        RuleFor(command => command.Email).OptionalEmail();
        RuleFor(command => command.Phone).OptionalPhone();
        RuleFor(command => command.Email)
            .Must((command, _) => AccountRules.HasContact(command.Email, command.Phone))
            .WithMessage(AccountRules.ContactRequired);
        RuleFor(command => command.Password).NewPassword();
        RuleFor(command => command.Role).AssignableRole();
        RuleFor(command => command.BranchId)
            .NotNull().When(command => command.Role == nameof(UserRole.Cashier))
            .WithMessage(AccountRules.CashierNeedsBranch);
    }
}
