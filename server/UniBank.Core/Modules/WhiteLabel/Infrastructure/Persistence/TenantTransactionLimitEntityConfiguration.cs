using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.WhiteLabel.Domain.Entities;

namespace UniBank.Core.Modules.WhiteLabel.Infrastructure.Persistence;

public sealed class TenantTransactionLimitEntityConfiguration : IEntityTypeConfiguration<TenantTransactionLimit>
{
    public void Configure(EntityTypeBuilder<TenantTransactionLimit> builder)
    {
        builder.ToTable("tenant_transaction_limits");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId).IsRequired().HasMaxLength(100).HasColumnName("tenant_id");
        builder.Property(l => l.TransactionType).IsRequired().HasMaxLength(50).HasColumnName("transaction_type");
        builder.Property(l => l.PerTransactionLimit).HasPrecision(18, 4).HasColumnName("per_transaction_limit");
        builder.Property(l => l.DailyLimit).HasPrecision(18, 4).HasColumnName("daily_limit");
        builder.Property(l => l.MonthlyLimit).HasPrecision(18, 4).HasColumnName("monthly_limit");
        builder.Property(l => l.Currency).IsRequired().HasMaxLength(10).HasColumnName("currency");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(l => new { l.TenantId, l.TransactionType })
            .IsUnique()
            .HasDatabaseName("ix_tenant_transaction_limits_tenant_type");

        builder.Ignore(l => l.DomainEvents);
        builder.Ignore(l => l.Version);
    }
}
