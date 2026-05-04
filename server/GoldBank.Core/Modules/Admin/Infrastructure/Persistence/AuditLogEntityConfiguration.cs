using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Admin.Domain.Entities;

namespace GoldBank.Core.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the AuditLog aggregate (STORY-055).
/// Maps to the "audit_logs" table in the tenant schema.
/// </summary>
public sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AdminUserId)
            .IsRequired()
            .HasColumnName("admin_user_id");

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("action");

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("entity_type");

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("entity_id");

        builder.Property(a => a.Details)
            .HasColumnType("text")
            .HasColumnName("details");

        builder.Property(a => a.IpAddress)
            .HasMaxLength(45)
            .HasColumnName("ip_address");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(a => a.AdminUserId)
            .HasDatabaseName("ix_audit_logs_admin_user_id");

        builder.HasIndex(a => a.EntityType)
            .HasDatabaseName("ix_audit_logs_entity_type");

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("ix_audit_logs_created_at");

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Version);
    }
}
