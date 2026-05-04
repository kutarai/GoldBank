using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Core.Modules.Ekub.Infrastructure.Persistence;

public sealed class EkubMembershipEntityConfiguration : IEntityTypeConfiguration<EkubMembership>
{
    public void Configure(EntityTypeBuilder<EkubMembership> builder)
    {
        builder.ToTable("ekub_memberships");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.GroupId).IsRequired().HasColumnName("group_id");
        builder.Property(m => m.CustomerId).IsRequired().HasColumnName("customer_id");
        builder.Property(m => m.Role)
            .IsRequired().HasConversion<string>().HasMaxLength(20).HasColumnName("role");
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at");
        builder.Property(m => m.LeftAt).HasColumnName("left_at");
        builder.Property(m => m.ExitReason).HasMaxLength(500).HasColumnName("exit_reason");
        builder.Property(m => m.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        // A customer holds at most one *active* membership in a given group.
        builder.HasIndex(m => new { m.GroupId, m.CustomerId })
            .IsUnique()
            .HasFilter("left_at IS NULL")
            .HasDatabaseName("ix_ekub_memberships_group_customer_active_unique");

        builder.HasIndex(m => m.CustomerId).HasDatabaseName("ix_ekub_memberships_customer");
        builder.HasIndex(m => m.GroupId).HasDatabaseName("ix_ekub_memberships_group");

        builder.Ignore(m => m.DomainEvents);
        builder.Ignore(m => m.Version);
    }
}
