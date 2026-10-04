using FluentValidation;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Branches.UpsertBranch;

public sealed class UpsertBranchValidator : AbstractValidator<UpsertBranchCommand>
{
    public UpsertBranchValidator()
    {
        RuleFor(command => command.Code)
            .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Không được để trống")
            .MaximumLength(50).WithMessage("Tối đa 50 ký tự")
            .When(command => command.Id is null);
        RuleFor(command => command.Name).RequiredName();
        RuleFor(command => command.Type)
            .Must(type => Enum.TryParse<BranchType>(type, out var parsed) && Enum.IsDefined(parsed) && parsed.ToString() == type)
            .WithMessage("Loại chi nhánh phải là Store hoặc Warehouse");
        RuleFor(command => command.Address).MaximumLength(500).WithMessage("Tối đa 500 ký tự");
    }
}
