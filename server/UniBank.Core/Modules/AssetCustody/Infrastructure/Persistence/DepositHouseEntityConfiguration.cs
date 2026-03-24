using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;

namespace UniBank.Core.Modules.AssetCustody.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the DepositHouse aggregate root.
/// Maps to the "deposit_houses" table with a unique index on license number per tenant.
/// </summary>
public sealed class DepositHouseEntityConfiguration : IEntityTypeConfiguration<DepositHouse>
{
    public void Configure(EntityTypeBuilder<DepositHouse> builder)
    {
        builder.ToTable("deposit_houses");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        builder.Property(d => d.Address)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.Property(d => d.City)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("city");

        builder.Property(d => d.ContactPhone)
            .IsRequired()
            .HasMaxLength(30)
            .HasColumnName("contact_phone");

        builder.Property(d => d.ContactEmail)
            .IsRequired()
            .HasMaxLength(254)
            .HasColumnName("contact_email");

        builder.Property(d => d.LicenseNumber)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("license_number");

        builder.Property(d => d.ApiEndpoint)
            .HasMaxLength(2000)
            .HasColumnName("api_endpoint");

        builder.Property(d => d.TrustStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("trust_status");

        builder.Property(d => d.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        builder.Property(d => d.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(d => new { d.TenantId, d.LicenseNumber })
            .IsUnique()
            .HasDatabaseName("ix_deposit_houses_tenant_license_unique");

        builder.HasIndex(d => d.TenantId)
            .HasDatabaseName("ix_deposit_houses_tenant_id");

        builder.HasIndex(d => d.IsActive)
            .HasDatabaseName("ix_deposit_houses_is_active");

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.Version);
    }
}
