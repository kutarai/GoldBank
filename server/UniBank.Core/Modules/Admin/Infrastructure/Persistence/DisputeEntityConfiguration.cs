using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Admin.Domain.Entities;

namespace UniBank.Core.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Dispute aggregate (STORY-061).
/// Maps to the "disputes" table in the tenant schema.
/// </summary>
public sealed class DisputeEntityConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("disputes");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.TransactionId)
            .IsRequired()
            .HasColumnName("transaction_id");

        builder.Property(d => d.AccountId)
            .IsRequired()
            .HasColumnName("account_id");

        builder.Property(d => d.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("type");

        builder.Property(d => d.Description)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("description");

        builder.Property(d => d.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("status");

        builder.Property(d => d.Resolution)
            .HasMaxLength(1000)
            .HasColumnName("resolution");

        builder.Property(d => d.RefundAmount)
            .HasPrecision(18, 2)
            .HasColumnName("refund_amount");

        builder.Property(d => d.RefundCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("refund_currency");

        builder.Property(d => d.AdminUserId)
            .HasColumnName("admin_user_id");

        builder.Property(d => d.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(d => d.TransactionId)
            .HasDatabaseName("ix_disputes_transaction_id");

        builder.HasIndex(d => d.AccountId)
            .HasDatabaseName("ix_disputes_account_id");

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("ix_disputes_status");

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.Version);
    }
}
