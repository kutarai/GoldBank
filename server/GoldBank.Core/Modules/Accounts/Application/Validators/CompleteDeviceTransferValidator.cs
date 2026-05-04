using FluentValidation;
using GoldBank.Core.Modules.Accounts.Application.Commands;

namespace GoldBank.Core.Modules.Accounts.Application.Validators;

public sealed class CompleteDeviceTransferValidator : AbstractValidator<CompleteDeviceTransferCommand>
{
    public CompleteDeviceTransferValidator()
    {
        RuleFor(x => x.TransferReference)
            .NotEmpty()
            .WithMessage("Transfer reference is required.");

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(6)
            .Matches(@"^\d{6}$")
            .WithMessage("OTP must be exactly 6 digits.");

        RuleFor(x => x.Pin)
            .NotEmpty()
            .WithMessage("PIN is required for device transfer verification.");

        RuleFor(x => x.NewDeviceId)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("New device ID is required.");
    }
}
