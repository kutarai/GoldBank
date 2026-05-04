using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.BranchCash.Domain.Entities;

namespace GoldBank.Core.Modules.BranchCash.Infrastructure.Persistence;

public sealed class CurrencyDenominationEntityConfiguration : IEntityTypeConfiguration<CurrencyDenomination>
{
    public void Configure(EntityTypeBuilder<CurrencyDenomination> builder)
    {
        builder.ToTable("currency_denominations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(c => c.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(c => c.FaceValue).IsRequired().HasColumnType("numeric(18,4)").HasColumnName("face_value");
        builder.Property(c => c.DenominationType).IsRequired().HasMaxLength(10).HasColumnName("denomination_type");
        builder.Property(c => c.DisplayOrder).IsRequired().HasColumnName("display_order");
        builder.Property(c => c.IsActive).IsRequired().HasColumnName("is_active");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.TenantId, c.Currency, c.FaceValue })
            .IsUnique()
            .HasDatabaseName("ux_currency_denominations_tenant_ccy_face");
        builder.HasIndex(c => new { c.TenantId, c.Currency, c.IsActive, c.DisplayOrder })
            .HasDatabaseName("ix_currency_denominations_lookup");

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Version);
    }
}
