using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Payments.Domain.Entities;

namespace GoldBank.Core.Modules.Payments.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the PaymentToken aggregate root (STORY-022).
/// Maps to the "payment_tokens" table in the tenant schema.
/// </summary>
public sealed class PaymentTokenEntityConfiguration : IEntityTypeConfiguration<PaymentToken>
{
    public void Configure(EntityTypeBuilder<PaymentToken> builder)
    {
        builder.ToTable("payment_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.AccountId)
            .IsRequired()
            .HasColumnName("account_id");

        builder.Property(t => t.Token)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnName("token");

        builder.Property(t => t.TokenReference)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnName("token_reference");

        builder.Property(t => t.CardPanLast4)
            .IsRequired()
            .HasMaxLength(4)
            .HasColumnName("card_pan_last4");

        builder.Property(t => t.DeviceId)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("device_id");

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(t => t.ExpiresAt)
            .IsRequired()
            .HasColumnName("expires_at");

        builder.Property(t => t.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.HasIndex(t => t.AccountId)
            .HasDatabaseName("ix_payment_tokens_account_id");

        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_payment_tokens_token_unique");

        builder.HasIndex(t => t.TokenReference)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_payment_tokens_reference_unique");

        builder.HasIndex(t => new { t.AccountId, t.DeviceId })
            .HasDatabaseName("ix_payment_tokens_account_device");

        builder.Ignore(t => t.DomainEvents);
        builder.Ignore(t => t.Version);
    }
}
