using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.BillPay.Domain.Entities;

namespace UniBank.Core.Modules.BillPay.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for SavedBiller (STORY-039).
/// Maps to the "saved_billers" table in the tenant schema.
/// </summary>
public sealed class SavedBillerEntityConfiguration : IEntityTypeConfiguration<SavedBiller>
{
    public void Configure(EntityTypeBuilder<SavedBiller> builder)
    {
        builder.ToTable("saved_billers");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(b => b.ProviderId).IsRequired().HasColumnName("provider_id");
        builder.Property(b => b.BillingReference).IsRequired().HasMaxLength(100).HasColumnName("billing_reference");
        builder.Property(b => b.Nickname).IsRequired().HasMaxLength(100).HasColumnName("nickname");
        builder.Property(b => b.LastPaidAt).HasColumnName("last_paid_at");
        builder.Property(b => b.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(b => b.AccountId)
            .HasDatabaseName("ix_saved_billers_account_id");

        builder.HasIndex(b => new { b.AccountId, b.ProviderId, b.BillingReference })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_saved_billers_account_provider_ref_unique");

        builder.Ignore(b => b.DomainEvents);
        builder.Ignore(b => b.Version);
    }
}
