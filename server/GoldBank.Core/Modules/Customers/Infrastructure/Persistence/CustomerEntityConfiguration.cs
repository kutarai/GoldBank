using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Customers.Domain.Entities;

namespace GoldBank.Core.Modules.Customers.Infrastructure.Persistence;

public sealed class CustomerEntityConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("phone");

        builder.Property(c => c.PhoneCountryCode)
            .IsRequired()
            .HasMaxLength(5)
            .HasColumnName("phone_country_code");

        builder.Property(c => c.FirstName)
            .HasMaxLength(100)
            .HasColumnName("first_name");

        builder.Property(c => c.LastName)
            .HasMaxLength(100)
            .HasColumnName("last_name");

        builder.Property(c => c.Email)
            .HasMaxLength(256)
            .HasColumnName("email");

        builder.Property(c => c.DateOfBirth)
            .HasMaxLength(10)
            .HasColumnName("date_of_birth");

        builder.Property(c => c.NationalId)
            .HasMaxLength(50)
            .HasColumnName("national_id");

        builder.Property(c => c.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        // One customer per phone within a tenant (active rows only)
        builder.HasIndex(c => new { c.TenantId, c.PhoneNumber })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ix_customers_tenant_phone_unique");

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("ix_customers_tenant_id");

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Version);
    }
}
