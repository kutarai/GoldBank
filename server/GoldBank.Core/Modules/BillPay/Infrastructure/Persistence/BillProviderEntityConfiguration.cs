using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.BillPay.Domain.Entities;

namespace GoldBank.Core.Modules.BillPay.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for BillProvider (STORY-037).
/// Maps to the "bill_providers" table in the tenant schema.
/// </summary>
public sealed class BillProviderEntityConfiguration : IEntityTypeConfiguration<BillProvider>
{
    public void Configure(EntityTypeBuilder<BillProvider> builder)
    {
        builder.ToTable("bill_providers");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
        builder.Property(p => p.Code).IsRequired().HasMaxLength(50).HasColumnName("code");
        builder.Property(p => p.Category).IsRequired().HasMaxLength(50).HasColumnName("category");
        builder.Property(p => p.RequiresMeterNumber).HasColumnName("requires_meter_number");
        builder.Property(p => p.RequiresAccountNumber).HasColumnName("requires_account_number");
        builder.Property(p => p.MinAmount).HasPrecision(18, 2).HasColumnName("min_amount");
        builder.Property(p => p.MaxAmount).HasPrecision(18, 2).HasColumnName("max_amount");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(p => p.Status).IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(p => p.CountryCode).IsRequired().HasMaxLength(5).HasColumnName("country_code");
        builder.Property(p => p.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_bill_providers_code_unique");

        builder.HasIndex(p => p.Category)
            .HasDatabaseName("ix_bill_providers_category");

        builder.HasIndex(p => p.CountryCode)
            .HasDatabaseName("ix_bill_providers_country_code");

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Version);
    }
}
