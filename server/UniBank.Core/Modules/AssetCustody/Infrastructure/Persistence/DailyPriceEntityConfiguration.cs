using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;

namespace UniBank.Core.Modules.AssetCustody.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the DailyPrice aggregate root.
/// Maps to the "daily_prices" table. Prices are global (no TenantId) and sourced from an API or entered manually.
/// </summary>
public sealed class DailyPriceEntityConfiguration : IEntityTypeConfiguration<DailyPrice>
{
    public void Configure(EntityTypeBuilder<DailyPrice> builder)
    {
        builder.ToTable("daily_prices");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AssetType)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("asset_type");

        builder.Property(p => p.PricePerGramUsd)
            .HasPrecision(18, 6)
            .HasColumnName("price_per_gram_usd");

        builder.Property(p => p.PricePerOzUsd)
            .HasPrecision(18, 6)
            .HasColumnName("price_per_oz_usd");

        builder.Property(p => p.Source)
            .IsRequired()
            .HasMaxLength(10)
            .HasColumnName("source");

        builder.Property(p => p.Date)
            .HasColumnName("date");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(p => new { p.AssetType, p.Date })
            .IsUnique()
            .HasDatabaseName("ix_daily_prices_asset_type_date_unique");

        builder.HasIndex(p => p.Date)
            .HasDatabaseName("ix_daily_prices_date");

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.Version);
    }
}
