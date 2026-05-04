using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Admin.Domain.Entities;

namespace GoldBank.Core.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the SystemConfig aggregate (STORY-060).
/// Maps to the "system_configs" table in the tenant schema.
/// </summary>
public sealed class SystemConfigEntityConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("system_configs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("key");

        builder.Property(s => s.ValueJson)
            .IsRequired()
            .HasColumnType("text")
            .HasColumnName("value_json");

        builder.Property(s => s.TenantId)
            .HasMaxLength(50)
            .HasColumnName("tenant_id");

        builder.Property(s => s.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(s => new { s.Key, s.TenantId })
            .IsUnique()
            .HasDatabaseName("ix_system_configs_key_tenant");

        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.Version);
    }
}
