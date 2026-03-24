using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;

namespace UniBank.Core.Modules.AssetCustody.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Asset aggregate root.
/// Maps to the "assets" table with soft-delete support and indexes on key query fields.
/// </summary>
public sealed class AssetEntityConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountId)
            .IsRequired()
            .HasColumnName("account_id");

        builder.Property(a => a.DepositHouseId)
            .IsRequired()
            .HasColumnName("deposit_house_id");

        builder.Property(a => a.ReceiptNumber)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("receipt_number");

        builder.Property(a => a.AssetType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("asset_type");

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(a => a.Quantity)
            .HasPrecision(18, 6)
            .HasColumnName("quantity");

        builder.Property(a => a.Unit)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("unit");

        builder.Property(a => a.WeightGrams)
            .HasPrecision(18, 6)
            .HasColumnName("weight_grams");

        builder.Property(a => a.Purity)
            .HasPrecision(8, 6)
            .HasColumnName("purity");

        builder.Property(a => a.ReceiptImagePath)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("receipt_image_path");

        builder.Property(a => a.ReceiptDate)
            .HasColumnName("receipt_date");

        builder.Property(a => a.LastValuationAmount)
            .HasPrecision(18, 2)
            .HasColumnName("last_valuation_amount");

        builder.Property(a => a.LastValuationDate)
            .HasColumnName("last_valuation_date");

        builder.Property(a => a.LastVerificationDate)
            .HasColumnName("last_verification_date");

        builder.Property(a => a.VerificationStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("verification_status");

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("status");

        builder.Property(a => a.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        // ISoftDeletable columns
        builder.Property(a => a.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(a => a.DeletedBy)
            .HasMaxLength(256)
            .HasColumnName("deleted_by");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasOne(a => a.DepositHouse)
            .WithMany(d => d.Assets)
            .HasForeignKey(a => a.DepositHouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Valuations)
            .WithOne(v => v.Asset)
            .HasForeignKey(v => v.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.AccountId)
            .HasDatabaseName("ix_assets_account_id");

        builder.HasIndex(a => a.DepositHouseId)
            .HasDatabaseName("ix_assets_deposit_house_id");

        builder.HasIndex(a => new { a.DepositHouseId, a.ReceiptNumber })
            .IsUnique()
            .HasDatabaseName("ix_assets_deposit_house_receipt_unique");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("ix_assets_status");

        builder.HasIndex(a => new { a.AccountId, a.Status })
            .HasDatabaseName("ix_assets_account_status");

        builder.HasIndex(a => a.IsDeleted)
            .HasDatabaseName("ix_assets_is_deleted");

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Version);
    }
}
