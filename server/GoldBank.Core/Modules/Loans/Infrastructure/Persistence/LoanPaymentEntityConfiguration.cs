using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Loans.Domain.Entities;

namespace GoldBank.Core.Modules.Loans.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the LoanPayment entity.
/// Maps to the "loan_payments" table with indexes on loan and due date.
/// </summary>
public sealed class LoanPaymentEntityConfiguration : IEntityTypeConfiguration<LoanPayment>
{
    public void Configure(EntityTypeBuilder<LoanPayment> builder)
    {
        builder.ToTable("loan_payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.LoanId)
            .IsRequired()
            .HasColumnName("loan_id");

        builder.Property(p => p.PaymentNumber)
            .HasColumnName("payment_number");

        builder.Property(p => p.PrincipalAmount)
            .HasPrecision(18, 2)
            .HasColumnName("principal_amount");

        builder.Property(p => p.InterestAmount)
            .HasPrecision(18, 2)
            .HasColumnName("interest_amount");

        builder.Property(p => p.TotalPayment)
            .HasPrecision(18, 2)
            .HasColumnName("total_payment");

        builder.Property(p => p.RemainingBalance)
            .HasPrecision(18, 2)
            .HasColumnName("remaining_balance");

        builder.Property(p => p.DueDate)
            .HasColumnName("due_date");

        builder.Property(p => p.IsPaid)
            .HasColumnName("is_paid");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(p => p.LoanId)
            .HasDatabaseName("ix_loan_payments_loan_id");

        builder.HasIndex(p => new { p.LoanId, p.PaymentNumber })
            .IsUnique()
            .HasDatabaseName("ix_loan_payments_loan_number_unique");

        builder.HasIndex(p => new { p.DueDate, p.IsPaid })
            .HasDatabaseName("ix_loan_payments_due_paid");

        builder.Ignore(p => p.DomainEvents);
    }
}
