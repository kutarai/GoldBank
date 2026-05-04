using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Loans.Domain.Entities;

namespace GoldBank.Core.Modules.Loans.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the Loan aggregate root.
/// Maps to the "loans" table with indexes on key query fields.
/// </summary>
public sealed class LoanEntityConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("loans");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.AccountId)
            .IsRequired()
            .HasColumnName("account_id");

        builder.Property(l => l.Principal)
            .HasPrecision(18, 2)
            .HasColumnName("principal");

        builder.Property(l => l.OutstandingBalance)
            .HasPrecision(18, 2)
            .HasColumnName("outstanding_balance");

        builder.Property(l => l.InterestRate)
            .HasPrecision(8, 4)
            .HasColumnName("interest_rate");

        builder.Property(l => l.TenureMonths)
            .HasColumnName("tenure_months");

        builder.Property(l => l.MonthlyPayment)
            .HasPrecision(18, 2)
            .HasColumnName("monthly_payment");

        builder.Property(l => l.Purpose)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("purpose");

        builder.Property(l => l.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(l => l.CreditScore)
            .HasColumnName("credit_score");

        builder.Property(l => l.PaymentsMade)
            .HasColumnName("payments_made");

        builder.Property(l => l.Reference)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("reference");

        builder.Property(l => l.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("currency");

        builder.Property(l => l.TenantId)
            .IsRequired()
            .HasColumnName("tenant_id");

        builder.Property(l => l.DisbursedAt)
            .HasColumnName("disbursed_at");

        builder.Property(l => l.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(l => l.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(l => l.CollateralAssetIds)
            .HasColumnName("collateral_asset_ids")
            .HasColumnType("text")
            .HasConversion(
                v => v.Count == 0 ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v)
                    ? new List<Guid>()
                    : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>(),
                new ValueComparer<List<Guid>>(
                    (a, b) => (a ?? new List<Guid>()).SequenceEqual(b ?? new List<Guid>()),
                    v => v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x.GetHashCode())),
                    v => v.ToList()))
            .IsRequired(false);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasMany(l => l.Payments)
            .WithOne(p => p.Loan)
            .HasForeignKey(p => p.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.AccountId)
            .HasDatabaseName("ix_loans_account_id");

        builder.HasIndex(l => l.Reference)
            .IsUnique()
            .HasDatabaseName("ix_loans_reference_unique");

        builder.HasIndex(l => l.Status)
            .HasDatabaseName("ix_loans_status");

        builder.HasIndex(l => new { l.AccountId, l.CreatedAt })
            .HasDatabaseName("ix_loans_account_created");

        builder.Ignore(l => l.DomainEvents);
        builder.Ignore(l => l.Version);
    }
}
