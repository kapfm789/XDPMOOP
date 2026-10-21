using System.Text.RegularExpressions;
using FluentValidation;
using Oism.Identity.Domain;

namespace Oism.Identity.Application.Branches.UpsertBranch;

public sealed class UpsertBranchValidator : AbstractValidator<UpsertBranchCommand>
{
    private static readonly Regex BinPattern = new("^[0-9]{6}$");
    private static readonly Regex AccountPattern = new("^[0-9A-Za-z]{1,19}$");

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

        // Tài khoản nhận chuyển khoản (W5-12): BIN 6 chữ số, số tài khoản chữ và số tối đa 19 ký tự, có cả hai hoặc không có.
        RuleFor(command => command.BankBin)
            .Must(bin => BinPattern.IsMatch(bin!.Trim())).WithMessage("Mã BIN ngân hàng gồm đúng 6 chữ số")
            .When(command => !string.IsNullOrWhiteSpace(command.BankBin));
        RuleFor(command => command.BankAccount)
            .Must(account => AccountPattern.IsMatch(account!.Trim())).WithMessage("Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự")
            .When(command => !string.IsNullOrWhiteSpace(command.BankAccount));
        RuleFor(command => command.BankAccount)
            .Must((command, account) => string.IsNullOrWhiteSpace(command.BankBin) == string.IsNullOrWhiteSpace(account))
            .WithMessage("Nhập cả ngân hàng và số tài khoản, hoặc bỏ trống cả hai");
    }
}
