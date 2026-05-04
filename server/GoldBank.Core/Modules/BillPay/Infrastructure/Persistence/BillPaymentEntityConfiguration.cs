using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.BillPay.Domain.Entities;

namespace GoldBank.Core.Modules.BillPay.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for BillPayment (STORY-038).
/// Maps to the "bill_payments" table in the tenant schema.
/// </summary>
public sealed class BillPaymentEntityConfiguration : IEntityTypeConfiguration<BillPayment>
{
    public void Configure(EntityTypeBuilder<BillPayment> builder)
    {
        builder.ToTable("bill_payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(p => p.ProviderId).IsRequired().HasColumnName("provider_id");
        builder.Property(p => p.BillingReference).IsRequired().HasMaxLength(100).HasColumnName("billing_reference");
        builder.Property(p => p.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(p => p.Fee).HasPrecision(18, 2).HasColumnName("fee");
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(p => p.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
        builder.Property(p => p.Reference).IsRequired().HasMaxLength(100).HasColumnName("reference");
        builder.Property(p => p.Token).HasMaxLength(50).HasColumnName("token");
        builder.Property(p => p.CompletedAt).HasColumnName("completed_at");
        builder.Property(p => p.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(p => p.AccountId)
            .HasDatabaseName("ix_bill_payments_account_id");

        builder.HasIndex(p => p.Reference)
            .IsUnique()
            .HasDatabaseName("ix_bill_payments_reference_unique");

        builder.HasIndex(p => p.ProviderId)
            .HasDatabaseName("ix_bill_payments_provider_id");

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Version);
    }
}
