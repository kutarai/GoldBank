using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Core.Modules.Ekub.Infrastructure.Persistence;

public sealed class EkubGroupEntityConfiguration : IEntityTypeConfiguration<EkubGroup>
{
    public void Configure(EntityTypeBuilder<EkubGroup> builder)
    {
        builder.ToTable("ekub_groups");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .IsRequired().HasMaxLength(100).HasColumnName("name");
        builder.Property(g => g.Description)
            .HasMaxLength(500).HasColumnName("description");
        builder.Property(g => g.Currency)
            .IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(g => g.MonthlyContribution)
            .HasPrecision(18, 2).HasColumnName("monthly_contribution");
        builder.Property(g => g.LoanInterestRatePercent)
            .HasPrecision(6, 3).HasColumnName("loan_interest_rate_percent");
        builder.Property(g => g.ApplyInterestOnContributions)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("apply_interest_on_contributions");
        builder.Property(g => g.Status)
            .IsRequired().HasConversion<string>().HasMaxLength(20).HasColumnName("status");
        builder.Property(g => g.ChairmanCustomerId)
            .IsRequired().HasColumnName("chairman_customer_id");
        builder.Property(g => g.TenantId)
            .IsRequired().HasColumnName("tenant_id");
        builder.Property(g => g.ActivatedAt).HasColumnName("activated_at");
        builder.Property(g => g.ClosedAt).HasColumnName("closed_at");
        builder.Property(g => g.LastFeeAppliedAt).HasColumnName("last_fee_applied_at");
        builder.Property(g => g.CreatedAt).HasColumnName("created_at");
        builder.Property(g => g.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany(g => g.Memberships)
            .WithOne(m => m.Group!)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.TenantId).HasDatabaseName("ix_ekub_groups_tenant_id");
        builder.HasIndex(g => g.Status).HasDatabaseName("ix_ekub_groups_status");
        builder.HasIndex(g => g.ChairmanCustomerId).HasDatabaseName("ix_ekub_groups_chairman");

        builder.Ignore(g => g.DomainEvents);
        builder.Ignore(g => g.Version);
    }
}
