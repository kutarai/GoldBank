using FluentValidation;
using GoldBank.Core.Modules.KYC.Application.Commands;

namespace GoldBank.Core.Modules.KYC.Application.Validators;

public sealed class UploadSelfieCommandValidator : AbstractValidator<UploadSelfieCommand>
{
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png"];
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public UploadSelfieCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithMessage("Account ID is required.");
        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Selfie must be JPEG or PNG image.");
        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage("File cannot be empty.")
            .LessThanOrEqualTo(MaxFileSize).WithMessage("Selfie must be under 5MB.");
        RuleFor(x => x.FileData).NotEmpty().WithMessage("File data is required.");
        RuleFor(x => x.TenantId).NotEmpty().WithMessage("Tenant ID is required.");
    }
}
