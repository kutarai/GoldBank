using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.WhiteLabel.Domain.Entities;

/// <summary>
/// Tenant branding configuration aggregate root (STORY-068).
/// Stores customizable branding elements for white-label deployment.
/// </summary>
public sealed class TenantBranding : AggregateRoot
{
    public string TenantId { get; set; } = default!;
    public string AppName { get; set; } = "UniBank";
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#1a73e8";
    public string SecondaryColor { get; set; } = "#174ea6";
    public string AccentColor { get; set; } = "#fbbc04";
    public string? FaviconUrl { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? CustomCss { get; set; }
}
