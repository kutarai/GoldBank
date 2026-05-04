using FluentValidation;
using GoldBank.Core.Modules.Accounts.Application.Commands;

namespace GoldBank.Core.Modules.Accounts.Application.Validators;

/// <summary>
/// Validates the VerifyOtpCommand before it reaches the handler.
/// Checks registration ID, OTP format (6 digits), and phone number presence.
/// </summary>
public sealed class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.RegistrationId)
            .NotEmpty()
            .WithMessage("Registration ID is required.");

        RuleFor(x => x.Otp)
            .NotEmpty()
            .WithMessage("OTP is required.")
            .Matches(@"^\d{6}$")
            .WithMessage("OTP must be exactly 6 digits.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Matches(@"^\+(?:27|263|260|258|267|266|268)\d{8,9}$")
            .WithMessage("Invalid phone number format. Expected E.164 with Southern African country code.");
    }
}
