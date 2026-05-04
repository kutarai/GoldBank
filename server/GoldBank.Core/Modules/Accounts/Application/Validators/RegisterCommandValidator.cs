using FluentValidation;
using GoldBank.Core.Modules.Accounts.Application.Commands;

namespace GoldBank.Core.Modules.Accounts.Application.Validators;

/// <summary>
/// Validates the RegisterCommand before it reaches the handler.
/// Checks phone format (E.164), device ID presence, and tenant ID presence.
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Matches(@"^\+(?:27|263|260|258|267|266|268)\d{8,9}$")
            .WithMessage("Invalid phone number format. Expected E.164 with Southern African country code.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("Device ID is required.")
            .MaximumLength(256)
            .WithMessage("Device ID must not exceed 256 characters.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required.");
    }
}
