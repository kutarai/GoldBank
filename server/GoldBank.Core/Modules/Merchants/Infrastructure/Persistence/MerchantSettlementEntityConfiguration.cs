using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Merchants.Domain.Entities;

namespace GoldBank.Core.Modules.Merchants.Infrastructure.Persistence;

public sealed class MerchantSettlementEntityConfiguration : IEntityTypeConfiguration<MerchantSettlement>
{
    public void Configure(EntityTypeBuilder<MerchantSettlement> builder)
    {
        builder.ToTable("merchant_settlements");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.MerchantId).IsRequired().HasColumnName("merchant_id");
        builder.Property(s => s.PeriodStart).IsRequired().HasColumnName("period_start");
        builder.Property(s => s.PeriodEnd).IsRequired().HasColumnName("period_end");
        builder.Property(s => s.TotalTransactions).IsRequired().HasColumnName("total_transactions");
        builder.Property(s => s.GrossAmount).IsRequired().HasPrecision(18, 2).HasColumnName("gross_amount");
        builder.Property(s => s.TotalFees).IsRequired().HasPrecision(18, 2).HasColumnName("total_fees");
        builder.Property(s => s.NetAmount).IsRequired().HasPrecision(18, 2).HasColumnName("net_amount");
        builder.Property(s => s.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(s => s.Status).IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(s => s.PaidAt).HasColumnName("paid_at");
        builder.Property(s => s.Reference).IsRequired().HasMaxLength(100).HasColumnName("reference");
        builder.Property(s => s.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(s => s.MerchantId).HasDatabaseName("ix_merchant_settlements_merchant");
        builder.HasIndex(s => new { s.MerchantId, s.PeriodStart, s.PeriodEnd, s.Currency })
            .IsUnique()
            .HasDatabaseName("ix_merchant_settlements_period");
        builder.HasIndex(s => s.Status).HasDatabaseName("ix_merchant_settlements_status");
        builder.HasIndex(s => s.TenantId).HasDatabaseName("ix_merchant_settlements_tenant");

        builder.HasQueryFilter(s => s.DeletedAt == null);

        builder.Ignore(s => s.DomainEvents);
        builder.Ignore(s => s.Version);
    }
}
