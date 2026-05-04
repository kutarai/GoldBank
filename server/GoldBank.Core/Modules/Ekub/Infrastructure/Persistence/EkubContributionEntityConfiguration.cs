using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Core.Modules.Ekub.Infrastructure.Persistence;

public sealed class EkubContributionEntityConfiguration : IEntityTypeConfiguration<EkubContribution>
{
    public void Configure(EntityTypeBuilder<EkubContribution> builder)
    {
        builder.ToTable("ekub_contributions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.GroupId).IsRequired().HasColumnName("group_id");
        builder.Property(c => c.CustomerId).IsRequired().HasColumnName("customer_id");
        builder.Property(c => c.MembershipId).IsRequired().HasColumnName("membership_id");
        builder.Property(c => c.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(c => c.Currency)
            .IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(c => c.Period)
            .IsRequired().HasMaxLength(7).HasColumnName("period");
        builder.Property(c => c.Status)
            .IsRequired().HasConversion<string>().HasMaxLength(20).HasColumnName("status");
        builder.Property(c => c.ConfirmedByCustomerId).HasColumnName("confirmed_by_customer_id");
        builder.Property(c => c.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(c => c.Notes).HasMaxLength(500).HasColumnName("notes");
        builder.Property(c => c.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.GroupId, c.Period })
            .HasDatabaseName("ix_ekub_contributions_group_period");
        builder.HasIndex(c => new { c.CustomerId, c.Status })
            .HasDatabaseName("ix_ekub_contributions_customer_status");
        builder.HasIndex(c => c.MembershipId)
            .HasDatabaseName("ix_ekub_contributions_membership");

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Version);
    }
}
