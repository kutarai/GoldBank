using FluentValidation;
using GoldBank.Core.Modules.Merchants.Application.Commands;

namespace GoldBank.Core.Modules.Merchants.Application.Validators;

public sealed class RegisterMerchantCommandValidator : AbstractValidator<RegisterMerchantCommand>
{
    private static readonly string[] AllowedBusinessTypes =
        ["retail", "food_service", "transport", "telecom", "general_agent", "other"];

    public RegisterMerchantCommandValidator()
    {
        RuleFor(x => x.OwnerAccountId)
            .NotEmpty()
            .WithMessage("Owner account ID is required.");

        RuleFor(x => x.BusinessName)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Business name is required and must not exceed 200 characters.");

        RuleFor(x => x.BusinessType)
            .NotEmpty()
            .Must(bt => AllowedBusinessTypes.Contains(bt))
            .WithMessage("Business type must be one of: retail, food_service, transport, telecom, general_agent, other.");

        RuleFor(x => x.BusinessAddress)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Business address is required.");

        RuleFor(x => x.GpsLatitude)
            .InclusiveBetween(-90, 90)
            .When(x => x.GpsLatitude.HasValue)
            .WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.GpsLongitude)
            .InclusiveBetween(-180, 180)
            .When(x => x.GpsLongitude.HasValue)
            .WithMessage("Longitude must be between -180 and 180.");

        RuleFor(x => x.AgentTermsAccepted)
            .Equal(true)
            .When(x => x.IsAgent)
            .WithMessage("Agent terms must be accepted when registering as an agent.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required.");
    }
}
