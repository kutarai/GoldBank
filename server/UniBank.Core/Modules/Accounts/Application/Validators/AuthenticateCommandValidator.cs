using FluentValidation;
using UniBank.Core.Modules.Accounts.Application.Commands;

namespace UniBank.Core.Modules.Accounts.Application.Validators;

public sealed class AuthenticateCommandValidator : AbstractValidator<AuthenticateCommand>
{
    public AuthenticateCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\+(?:27|263|260|258|267|266|268)\d{8,9}$")
            .WithMessage("Invalid phone number format.");

        RuleFor(x => x.Pin)
            .NotEmpty()
            .WithMessage("PIN is required.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("Device ID is required.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required.");
    }
}
