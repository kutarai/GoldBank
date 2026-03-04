using FluentValidation;
using UniBank.Core.Modules.Accounts.Application.Commands;

namespace UniBank.Core.Modules.Accounts.Application.Validators;

public sealed class InitiateDeviceTransferValidator : AbstractValidator<InitiateDeviceTransferCommand>
{
    public InitiateDeviceTransferValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+(?:27|263|260|258|267|266|268)\d{8,9}$")
            .WithMessage("Invalid phone number format.");

        RuleFor(x => x.NewDeviceId)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("New device ID is required.");
    }
}
