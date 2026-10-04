using FluentValidation;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Users.UpdateUser;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(command => command.FullName).RequiredName();
        RuleFor(command => command.Role).AssignableRole();
        RuleFor(command => command.BranchId)
            .NotNull().When(command => command.Role == nameof(UserRole.Cashier))
            .WithMessage(AccountRules.CashierNeedsBranch);
    }
}
