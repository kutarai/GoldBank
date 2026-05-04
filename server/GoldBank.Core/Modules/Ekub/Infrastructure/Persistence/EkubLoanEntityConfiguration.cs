using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Core.Modules.Ekub.Infrastructure.Persistence;

public sealed class EkubLoanEntityConfiguration : IEntityTypeConfiguration<EkubLoan>
{
    public void Configure(EntityTypeBuilder<EkubLoan> builder)
    {
        builder.ToTable("ekub_loans");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.GroupId).IsRequired().HasColumnName("group_id");
        builder.Property(l => l.BorrowerCustomerId).IsRequired().HasColumnName("borrower_customer_id");
        builder.Property(l => l.Principal).HasPrecision(18, 2).HasColumnName("principal");
        builder.Property(l => l.InterestRatePercent).HasPrecision(6, 3).HasColumnName("interest_rate_percent");
        builder.Property(l => l.TermMonths).HasColumnName("term_months");
        builder.Property(l => l.TotalRepayable).HasPrecision(18, 2).HasColumnName("total_repayable");
        builder.Property(l => l.InstallmentAmount).HasPrecision(18, 2).HasColumnName("installment_amount");
        builder.Property(l => l.OutstandingBalance).HasPrecision(18, 2).HasColumnName("outstanding_balance");
        builder.Property(l => l.TotalInterestEarned).HasPrecision(18, 2).HasColumnName("total_interest_earned");
        builder.Property(l => l.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(l => l.Status)
            .IsRequired().HasConversion<string>().HasMaxLength(20).HasColumnName("status");
        builder.Property(l => l.Purpose).HasMaxLength(500).HasColumnName("purpose");
        builder.Property(l => l.TreasurerCustomerId).HasColumnName("treasurer_customer_id");
        builder.Property(l => l.DisbursedAt).HasColumnName("disbursed_at");
        builder.Property(l => l.ClosedAt).HasColumnName("closed_at");
        builder.Property(l => l.Notes).HasMaxLength(500).HasColumnName("notes");
        builder.Property(l => l.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");

        builder.HasMany(l => l.Votes)
            .WithOne(v => v.Loan!)
            .HasForeignKey(v => v.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(l => l.Repayments)
            .WithOne(r => r.Loan!)
            .HasForeignKey(r => r.LoanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.GroupId).HasDatabaseName("ix_ekub_loans_group");
        builder.HasIndex(l => l.BorrowerCustomerId).HasDatabaseName("ix_ekub_loans_borrower");
        builder.HasIndex(l => l.Status).HasDatabaseName("ix_ekub_loans_status");

        builder.Ignore(l => l.DomainEvents);
        builder.Ignore(l => l.Version);
    }
}

public sealed class EkubLoanVoteEntityConfiguration : IEntityTypeConfiguration<EkubLoanVote>
{
    public void Configure(EntityTypeBuilder<EkubLoanVote> builder)
    {
        builder.ToTable("ekub_loan_votes");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.LoanId).IsRequired().HasColumnName("loan_id");
        builder.Property(v => v.VoterCustomerId).IsRequired().HasColumnName("voter_customer_id");
        builder.Property(v => v.Approve).IsRequired().HasColumnName("approve");
        builder.Property(v => v.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        // One vote per (loan, voter) — allows update via application logic but
        // protects against duplicate inserts.
        builder.HasIndex(v => new { v.LoanId, v.VoterCustomerId })
            .IsUnique()
            .HasDatabaseName("ix_ekub_loan_votes_loan_voter_unique");

        builder.Ignore(v => v.DomainEvents);
        builder.Ignore(v => v.Version);
    }
}

public sealed class EkubLoanRepaymentEntityConfiguration : IEntityTypeConfiguration<EkubLoanRepayment>
{
    public void Configure(EntityTypeBuilder<EkubLoanRepayment> builder)
    {
        builder.ToTable("ekub_loan_repayments");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.LoanId).IsRequired().HasColumnName("loan_id");
        builder.Property(r => r.GroupId).IsRequired().HasColumnName("group_id");
        builder.Property(r => r.TreasurerCustomerId).IsRequired().HasColumnName("treasurer_customer_id");
        builder.Property(r => r.AmountPaid).HasPrecision(18, 2).HasColumnName("amount_paid");
        builder.Property(r => r.PrincipalPortion).HasPrecision(18, 2).HasColumnName("principal_portion");
        builder.Property(r => r.InterestPortion).HasPrecision(18, 2).HasColumnName("interest_portion");
        builder.Property(r => r.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(r => r.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(r => r.LoanId).HasDatabaseName("ix_ekub_loan_repayments_loan");
        builder.HasIndex(r => r.GroupId).HasDatabaseName("ix_ekub_loan_repayments_group");

        builder.Ignore(r => r.DomainEvents);
        builder.Ignore(r => r.Version);
    }
}
