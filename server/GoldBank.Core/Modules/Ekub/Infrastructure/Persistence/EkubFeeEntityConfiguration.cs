using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Core.Modules.Ekub.Infrastructure.Persistence;

public sealed class EkubFeeEntityConfiguration : IEntityTypeConfiguration<EkubFee>
{
    public void Configure(EntityTypeBuilder<EkubFee> builder)
    {
        builder.ToTable("ekub_fees");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.GroupId).IsRequired().HasColumnName("group_id");
        builder.Property(f => f.Period)
            .IsRequired().HasMaxLength(7).HasColumnName("period");
        builder.Property(f => f.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(f => f.Currency)
            .IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(f => f.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        // Idempotent: at most one fee per (group, period)
        builder.HasIndex(f => new { f.GroupId, f.Period })
            .IsUnique()
            .HasDatabaseName("ix_ekub_fees_group_period_unique");

        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.Version);
    }
}
