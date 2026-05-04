using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.AssetCustody.Domain.Entities;

namespace GoldBank.Core.Modules.AssetCustody.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the AssetValuation aggregate root.
/// Maps to the "asset_valuations" table. Valuations are immutable audit records — no soft delete.
/// </summary>
public sealed class AssetValuationEntityConfiguration : IEntityTypeConfiguration<AssetValuation>
{
    public void Configure(EntityTypeBuilder<AssetValuation> builder)
    {
        builder.ToTable("asset_valuations");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.AssetId)
            .IsRequired()
            .HasColumnName("asset_id");

        builder.Property(v => v.ValuationAmount)
            .HasPrecision(18, 2)
            .HasColumnName("valuation_amount");

        builder.Property(v => v.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("currency");

        builder.Property(v => v.ValuerName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("valuer_name");

        builder.Property(v => v.ValuerLicense)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("valuer_license");

        builder.Property(v => v.ReportImagePath)
            .HasMaxLength(1000)
            .HasColumnName("report_image_path");

        builder.Property(v => v.Notes)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnName("notes");

        builder.Property(v => v.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(v => v.AssetId)
            .HasDatabaseName("ix_asset_valuations_asset_id");

        builder.HasIndex(v => new { v.AssetId, v.CreatedAt })
            .HasDatabaseName("ix_asset_valuations_asset_created");

        builder.Ignore(v => v.DomainEvents);
        builder.Ignore(v => v.Version);
    }
}
