using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.FraudDetection.Domain.Entities;

namespace UniBank.Core.Modules.FraudDetection.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the FraudRule aggregate root (STORY-072).
/// Maps to the "fraud_rules" table in the tenant schema.
/// </summary>
public sealed class FraudRuleEntityConfiguration : IEntityTypeConfiguration<FraudRule>
{
    public void Configure(EntityTypeBuilder<FraudRule> builder)
    {
        builder.ToTable("fraud_rules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        builder.Property(r => r.RuleType)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("rule_type");

        builder.Property(r => r.Parameters)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnName("parameters");

        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(r => r.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_fraud_rules_tenant_id");

        builder.HasIndex(r => r.RuleType)
            .HasDatabaseName("ix_fraud_rules_rule_type");

        builder.HasIndex(r => new { r.TenantId, r.IsActive })
            .HasDatabaseName("ix_fraud_rules_tenant_active");

        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.Version);
    }
}
