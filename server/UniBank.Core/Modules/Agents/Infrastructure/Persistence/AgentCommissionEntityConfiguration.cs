using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Agents.Domain.Entities;

namespace UniBank.Core.Modules.Agents.Infrastructure.Persistence;

public sealed class AgentCommissionEntityConfiguration : IEntityTypeConfiguration<AgentCommission>
{
    public void Configure(EntityTypeBuilder<AgentCommission> builder)
    {
        builder.ToTable("agent_commissions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.MerchantId).IsRequired().HasColumnName("merchant_id");
        builder.Property(c => c.TransactionId).IsRequired().HasColumnName("transaction_id");
        builder.Property(c => c.TransactionType).IsRequired().HasMaxLength(20).HasColumnName("transaction_type");
        builder.Property(c => c.TransactionAmount).HasPrecision(18, 2).HasColumnName("transaction_amount");
        builder.Property(c => c.CommissionRate).HasPrecision(8, 4).HasColumnName("commission_rate");
        builder.Property(c => c.CommissionAmount).HasPrecision(18, 2).HasColumnName("commission_amount");
        builder.Property(c => c.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(c => c.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.MerchantId).HasDatabaseName("ix_agent_commissions_merchant_id");
        builder.HasIndex(c => c.TransactionId).HasDatabaseName("ix_agent_commissions_transaction_id");

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Version);
    }
}
