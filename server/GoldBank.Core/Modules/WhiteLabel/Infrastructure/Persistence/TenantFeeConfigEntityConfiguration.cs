using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.WhiteLabel.Domain.Entities;

namespace GoldBank.Core.Modules.WhiteLabel.Infrastructure.Persistence;

public sealed class TenantFeeConfigEntityConfiguration : IEntityTypeConfiguration<TenantFeeConfig>
{
    public void Configure(EntityTypeBuilder<TenantFeeConfig> builder)
    {
        builder.ToTable("tenant_fee_configs");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.TenantId).IsRequired().HasMaxLength(100).HasColumnName("tenant_id");
        builder.Property(f => f.TransactionType).IsRequired().HasMaxLength(50).HasColumnName("transaction_type");
        builder.Property(f => f.FeeType).IsRequired().HasMaxLength(30).HasColumnName("fee_type");
        builder.Property(f => f.Amount).HasPrecision(18, 4).HasColumnName("amount");
        builder.Property(f => f.Percentage).HasPrecision(10, 4).HasColumnName("percentage");
        builder.Property(f => f.MinFee).HasPrecision(18, 4).HasColumnName("min_fee");
        builder.Property(f => f.MaxFee).HasPrecision(18, 4).HasColumnName("max_fee");
        builder.Property(f => f.Currency).IsRequired().HasMaxLength(10).HasColumnName("currency");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(f => new { f.TenantId, f.TransactionType })
            .IsUnique()
            .HasDatabaseName("ix_tenant_fee_configs_tenant_type");

        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.Version);
    }
}
