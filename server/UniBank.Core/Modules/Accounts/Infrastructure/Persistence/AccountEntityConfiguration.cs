using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Accounts.Domain.Entities;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Account aggregate root.
/// Maps to the "accounts" table in the tenant schema.
/// </summary>
public sealed class AccountEntityConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("phone");

        builder.Property(a => a.PhoneCountryCode)
            .IsRequired()
            .HasMaxLength(5)
            .HasColumnName("phone_country_code");

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(a => a.KycLevel)
            .HasColumnName("kyc_level");

        builder.Property(a => a.DailyLimit)
            .HasPrecision(18, 2)
            .HasColumnName("daily_limit");

        builder.Property(a => a.MonthlyLimit)
            .HasPrecision(18, 2)
            .HasColumnName("monthly_limit");

        builder.Property(a => a.Balance)
            .HasPrecision(18, 2)
            .HasColumnName("balance");

        builder.Property(a => a.AvailableBalance)
            .HasPrecision(18, 2)
            .HasColumnName("available_balance");

        builder.Property(a => a.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("currency");

        builder.Property(a => a.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(a => a.PinHash)
            .HasMaxLength(256)
            .HasColumnName("pin_hash");

        builder.Property(a => a.DeviceId)
            .HasMaxLength(256)
            .HasColumnName("device_id");

        builder.Property(a => a.FirstName)
            .HasMaxLength(100)
            .HasColumnName("first_name");

        builder.Property(a => a.LastName)
            .HasMaxLength(100)
            .HasColumnName("last_name");

        builder.Property(a => a.Email)
            .HasMaxLength(256)
            .HasColumnName("email");

        builder.Property(a => a.DateOfBirth)
            .HasMaxLength(10)
            .HasColumnName("date_of_birth");

        builder.Property(a => a.NationalId)
            .HasMaxLength(50)
            .HasColumnName("national_id");

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(a => a.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(a => a.LastLoginAt)
            .HasColumnName("last_login_at");

        // Unique constraint on phone number to prevent duplicates (final guard against race conditions)
        builder.HasIndex(a => a.PhoneNumber)
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_accounts_phone_unique");

        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("ix_accounts_tenant_id");

        // Ignore domain events collection from AggregateRoot - not mapped to database
        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Version);
    }
}
