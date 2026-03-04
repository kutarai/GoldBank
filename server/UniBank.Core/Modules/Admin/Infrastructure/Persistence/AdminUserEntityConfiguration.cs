using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Admin.Domain.Entities;

namespace UniBank.Core.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the AdminUser aggregate (STORY-055).
/// Maps to the "admin_users" table in the tenant schema.
/// </summary>
public sealed class AdminUserEntityConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_users");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Username)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("username");

        builder.Property(a => a.PasswordHash)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("password_hash");

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("email");

        builder.Property(a => a.FullName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("full_name");

        builder.Property(a => a.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasColumnName("role");

        builder.Property(a => a.TenantId)
            .HasMaxLength(50)
            .HasColumnName("tenant_id");

        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(a => a.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(a => a.Username)
            .IsUnique()
            .HasDatabaseName("ix_admin_users_username");

        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("ix_admin_users_email");

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Version);
    }
}
