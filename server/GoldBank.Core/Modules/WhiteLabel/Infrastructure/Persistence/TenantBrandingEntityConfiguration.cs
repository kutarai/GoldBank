using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.WhiteLabel.Domain.Entities;

namespace GoldBank.Core.Modules.WhiteLabel.Infrastructure.Persistence;

public sealed class TenantBrandingEntityConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_branding");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.TenantId).IsRequired().HasMaxLength(100).HasColumnName("tenant_id");
        builder.Property(b => b.AppName).IsRequired().HasMaxLength(200).HasColumnName("app_name");
        builder.Property(b => b.LogoUrl).HasMaxLength(500).HasColumnName("logo_url");
        builder.Property(b => b.PrimaryColor).IsRequired().HasMaxLength(20).HasColumnName("primary_color");
        builder.Property(b => b.SecondaryColor).IsRequired().HasMaxLength(20).HasColumnName("secondary_color");
        builder.Property(b => b.AccentColor).IsRequired().HasMaxLength(20).HasColumnName("accent_color");
        builder.Property(b => b.FaviconUrl).HasMaxLength(500).HasColumnName("favicon_url");
        builder.Property(b => b.SupportEmail).HasMaxLength(200).HasColumnName("support_email");
        builder.Property(b => b.SupportPhone).HasMaxLength(50).HasColumnName("support_phone");
        builder.Property(b => b.CustomCss).HasColumnName("custom_css");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(b => b.TenantId).IsUnique().HasDatabaseName("ix_tenant_branding_tenant");

        builder.Ignore(b => b.DomainEvents);
        builder.Ignore(b => b.Version);
    }
}
