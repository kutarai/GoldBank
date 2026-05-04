using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.FraudDetection.Domain.Entities;

namespace GoldBank.Core.Modules.FraudDetection.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the FraudAlert aggregate root (STORY-072).
/// Maps to the "fraud_alerts" table in the tenant schema.
/// </summary>
public sealed class FraudAlertEntityConfiguration : IEntityTypeConfiguration<FraudAlert>
{
    public void Configure(EntityTypeBuilder<FraudAlert> builder)
    {
        builder.ToTable("fraud_alerts");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.AccountId)
            .IsRequired()
            .HasColumnName("account_id");

        builder.Property(f => f.TransactionId)
            .IsRequired()
            .HasColumnName("transaction_id");

        builder.Property(f => f.AlertType)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("alert_type");

        builder.Property(f => f.Severity)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("severity");

        builder.Property(f => f.Description)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnName("description");

        builder.Property(f => f.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(f => f.AdminNotes)
            .HasMaxLength(2000)
            .HasColumnName("admin_notes");

        builder.Property(f => f.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(f => f.ReviewedBy)
            .HasMaxLength(100)
            .HasColumnName("reviewed_by");

        builder.Property(f => f.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(f => f.ActivitiesJson)
            .HasColumnType("jsonb")
            .HasColumnName("activities_json");

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(f => f.AccountId)
            .HasDatabaseName("ix_fraud_alerts_account_id");

        builder.HasIndex(f => f.TransactionId)
            .HasDatabaseName("ix_fraud_alerts_transaction_id");

        builder.HasIndex(f => f.Status)
            .HasDatabaseName("ix_fraud_alerts_status");

        builder.HasIndex(f => f.Severity)
            .HasDatabaseName("ix_fraud_alerts_severity");

        builder.HasIndex(f => new { f.TenantId, f.CreatedAt })
            .HasDatabaseName("ix_fraud_alerts_tenant_created");

        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.Version);
    }
}
