using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Payments.Domain.Entities;

namespace GoldBank.Core.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Payment aggregate root (STORY-023 through STORY-027).
/// Maps to the "payments" table in the tenant schema with indexes on key query fields.
/// </summary>
public sealed class PaymentEntityConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PayerAccountId)
            .IsRequired()
            .HasColumnName("payer_account_id");

        builder.Property(p => p.MerchantAccountId)
            .IsRequired()
            .HasColumnName("merchant_account_id");

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2)
            .HasColumnName("amount");

        builder.Property(p => p.Fee)
            .HasPrecision(18, 2)
            .HasColumnName("fee");

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("currency");

        builder.Property(p => p.Type)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("type");

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(p => p.Reference)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("reference");

        builder.Property(p => p.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(p => p.NfcData)
            .HasMaxLength(2000)
            .HasColumnName("nfc_data");

        builder.Property(p => p.QrCodeData)
            .HasMaxLength(4000)
            .HasColumnName("qr_code_data");

        builder.Property(p => p.TerminalId)
            .HasMaxLength(50)
            .HasColumnName("terminal_id");

        builder.Property(p => p.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(p => p.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(p => p.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(p => p.PayerAccountId)
            .HasDatabaseName("ix_payments_payer_account_id");

        builder.HasIndex(p => p.MerchantAccountId)
            .HasDatabaseName("ix_payments_merchant_account_id");

        builder.HasIndex(p => p.Reference)
            .IsUnique()
            .HasDatabaseName("ix_payments_reference_unique");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("ix_payments_status");

        builder.HasIndex(p => new { p.PayerAccountId, p.CreatedAt })
            .HasDatabaseName("ix_payments_payer_created");

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Version);
    }
}
