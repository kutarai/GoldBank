using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Transfers.Domain.Entities;

namespace GoldBank.Core.Modules.Transfers.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Transfer aggregate root (STORY-029, STORY-030).
/// Maps to the "transfers" table in the tenant schema with indexes on key query fields.
/// </summary>
public sealed class TransferEntityConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.SenderAccountId)
            .IsRequired()
            .HasColumnName("sender_account_id");

        builder.Property(t => t.RecipientAccountId)
            .HasColumnName("recipient_account_id");

        builder.Property(t => t.RecipientPhone)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("recipient_phone");

        builder.Property(t => t.RecipientName)
            .HasMaxLength(200)
            .HasColumnName("recipient_name");

        builder.Property(t => t.Type)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("type");

        builder.Property(t => t.SendAmount)
            .HasPrecision(18, 2)
            .HasColumnName("send_amount");

        builder.Property(t => t.SendCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("send_currency");

        builder.Property(t => t.ReceiveAmount)
            .HasPrecision(18, 2)
            .HasColumnName("receive_amount");

        builder.Property(t => t.ReceiveCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("receive_currency");

        builder.Property(t => t.Fee)
            .HasPrecision(18, 2)
            .HasColumnName("fee");

        builder.Property(t => t.ExchangeRate)
            .HasMaxLength(50)
            .HasColumnName("exchange_rate");

        builder.Property(t => t.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(t => t.Reference)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("reference");

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.Property(t => t.EstimatedDelivery)
            .HasColumnName("estimated_delivery");

        builder.Property(t => t.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(t => t.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(t => t.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(t => t.SenderAccountId)
            .HasDatabaseName("ix_transfers_sender_account_id");

        builder.HasIndex(t => t.RecipientAccountId)
            .HasDatabaseName("ix_transfers_recipient_account_id");

        builder.HasIndex(t => t.Reference)
            .IsUnique()
            .HasDatabaseName("ix_transfers_reference_unique");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_transfers_status");

        builder.HasIndex(t => new { t.SenderAccountId, t.CreatedAt })
            .HasDatabaseName("ix_transfers_sender_created");

        builder.Ignore(t => t.DomainEvents);
        builder.Ignore(t => t.Version);
    }
}
