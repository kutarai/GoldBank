using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Admin.Domain.Entities;

namespace UniBank.Core.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Branch entity (EPIC-019).
/// Maps to the "branches" table with a unique index on (Code, TenantId).
/// </summary>
public sealed class BranchEntityConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id");

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        builder.Property(b => b.Code)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("code");

        builder.Property(b => b.Address)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.Property(b => b.City)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("city");

        builder.Property(b => b.Phone)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("phone");

        builder.Property(b => b.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasColumnName("is_active");

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(b => b.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(b => new { b.Code, b.TenantId })
            .IsUnique()
            .HasDatabaseName("ix_branches_code_tenant_id");
    }
}
