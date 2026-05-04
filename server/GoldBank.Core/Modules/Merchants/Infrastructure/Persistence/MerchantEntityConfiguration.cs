using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Merchants.Domain.Entities;

namespace GoldBank.Core.Modules.Merchants.Infrastructure.Persistence;

public sealed class MerchantEntityConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MerchantCode).IsRequired().HasMaxLength(30).HasColumnName("merchant_code");
        builder.Property(m => m.OwnerAccountId).IsRequired().HasColumnName("owner_account_id");
        builder.Property(m => m.BusinessName).IsRequired().HasMaxLength(200).HasColumnName("business_name");
        builder.Property(m => m.BusinessType).IsRequired().HasMaxLength(50).HasColumnName("business_type");
        builder.Property(m => m.RegistrationNumber).HasMaxLength(100).HasColumnName("registration_number");
        builder.Property(m => m.TaxId).HasMaxLength(100).HasColumnName("tax_id");
        builder.Property(m => m.CategoryCode).HasMaxLength(50).HasColumnName("category_code");
        builder.Property(m => m.BusinessAddress).IsRequired().HasMaxLength(500).HasColumnName("business_address");
        builder.Property(m => m.GpsLatitude).HasPrecision(10, 7).HasColumnName("gps_latitude");
        builder.Property(m => m.GpsLongitude).HasPrecision(10, 7).HasColumnName("gps_longitude");
        builder.Property(m => m.GpsAccuracyMeters).HasPrecision(8, 2).HasColumnName("gps_accuracy_meters");
        builder.Property(m => m.IsAgent).HasColumnName("is_agent");
        builder.Property(m => m.AgentTermsAccepted).HasColumnName("agent_terms_accepted");
        builder.Property(m => m.AgentTermsAcceptedAt).HasColumnName("agent_terms_accepted_at");
        builder.Property(m => m.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
        builder.Property(m => m.KycStatus).IsRequired().HasMaxLength(30).HasColumnName("kyc_status");
        builder.Property(m => m.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(m => m.ActivatedAt).HasColumnName("activated_at");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => m.MerchantCode).IsUnique().HasDatabaseName("ix_merchants_code");
        builder.HasIndex(m => m.OwnerAccountId).HasDatabaseName("ix_merchants_owner");
        builder.HasIndex(m => m.Status).HasDatabaseName("ix_merchants_status");
        builder.HasIndex(m => m.TenantId).HasDatabaseName("ix_merchants_tenant");
        builder.HasIndex(m => new { m.BusinessName, m.TenantId }).IsUnique().HasDatabaseName("ix_merchants_name_tenant");

        builder.Ignore(m => m.DomainEvents);
        builder.Ignore(m => m.Version);
    }
}
