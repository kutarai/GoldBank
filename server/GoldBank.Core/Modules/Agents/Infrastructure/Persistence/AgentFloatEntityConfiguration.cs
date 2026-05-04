using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Agents.Domain.Entities;

namespace GoldBank.Core.Modules.Agents.Infrastructure.Persistence;

public sealed class AgentFloatEntityConfiguration : IEntityTypeConfiguration<AgentFloat>
{
    public void Configure(EntityTypeBuilder<AgentFloat> builder)
    {
        builder.ToTable("agent_floats");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.MerchantId).IsRequired().HasColumnName("merchant_id");
        builder.Property(f => f.FloatBalance).HasPrecision(18, 2).HasColumnName("float_balance");
        builder.Property(f => f.FloatLimit).HasPrecision(18, 2).HasColumnName("float_limit");
        builder.Property(f => f.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(f => f.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(f => f.DeletedAt).HasColumnName("deleted_at");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(f => f.MerchantId).IsUnique().HasDatabaseName("ix_agent_floats_merchant_id");

        builder.Ignore(f => f.DomainEvents);
        builder.Ignore(f => f.Version);
    }
}
