namespace GoldBank.Core.Modules.WhiteLabel.Application.Commands;

/// <summary>
/// Command to update tenant branding configuration (STORY-068).
/// </summary>
public sealed record UpdateBrandingCommand(
    string TenantId,
    string AppName,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string? FaviconUrl,
    string? SupportEmail,
    string? SupportPhone,
    string? CustomCss);
