using FluentValidation;
using GoldBank.Core.Modules.KYC.Application.Commands;

namespace GoldBank.Core.Modules.KYC.Application.Validators;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private static readonly string[] AllowedDocumentTypes = ["national_id", "passport", "drivers_license"];
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "application/pdf"];
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("Account ID is required.");

        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .Must(dt => AllowedDocumentTypes.Contains(dt))
            .WithMessage("Document type must be one of: national_id, passport, drivers_license.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(255)
            .WithMessage("File name is required and must not exceed 255 characters.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Content type must be one of: image/jpeg, image/png, application/pdf.");

        RuleFor(x => x.FileSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxFileSize)
            .WithMessage($"File size must be between 1 byte and {MaxFileSize / (1024 * 1024)} MB.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required.");
    }
}
