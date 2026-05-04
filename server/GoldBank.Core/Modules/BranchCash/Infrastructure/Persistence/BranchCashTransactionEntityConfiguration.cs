using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.BranchCash.Domain.Entities;

namespace GoldBank.Core.Modules.BranchCash.Infrastructure.Persistence;

public sealed class BranchCashTransactionEntityConfiguration : IEntityTypeConfiguration<BranchCashTransaction>
{
    public void Configure(EntityTypeBuilder<BranchCashTransaction> builder)
    {
        builder.ToTable("branch_cash_transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TransactionId).IsRequired().HasColumnName("transaction_id");
        builder.Property(t => t.DrawerSessionId).IsRequired().HasColumnName("drawer_session_id");
        builder.Property(t => t.TellerId).IsRequired().HasColumnName("teller_id");
        builder.Property(t => t.BranchId).IsRequired().HasColumnName("branch_id");
        builder.Property(t => t.AccountId).IsRequired().HasColumnName("account_id");

        builder.Property(t => t.Direction).IsRequired().HasMaxLength(20).HasColumnName("direction");
        builder.Property(t => t.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(t => t.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(t => t.DepositorName).IsRequired().HasMaxLength(200).HasColumnName("depositor_name");

        builder.Property(t => t.DenominationBreakdownJson).IsRequired().HasColumnType("jsonb").HasColumnName("denomination_breakdown_json");

        builder.Property(t => t.IdentityVerified).HasColumnName("identity_verified");
        builder.Property(t => t.SupervisorApproverId).HasColumnName("supervisor_approver_id");
        builder.Property(t => t.SupervisorApprovedAt).HasColumnName("supervisor_approved_at");

        builder.Property(t => t.ReceiptPdfPath).HasMaxLength(500).HasColumnName("receipt_pdf_path");
        builder.Property(t => t.ReversedByTransactionId).HasColumnName("reversed_by_transaction_id");
        builder.Property(t => t.ReversedAt).HasColumnName("reversed_at");

        builder.Property(t => t.Status).IsRequired().HasMaxLength(40).HasColumnName("status");
        builder.Property(t => t.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => new { t.AccountId, t.CreatedAt }).HasDatabaseName("ix_branch_cash_transactions_account_created");
        builder.HasIndex(t => t.DrawerSessionId).HasDatabaseName("ix_branch_cash_transactions_drawer");
        builder.HasIndex(t => t.TransactionId).HasDatabaseName("ix_branch_cash_transactions_transaction_id");
        builder.HasIndex(t => t.Status).HasDatabaseName("ix_branch_cash_transactions_status");

        builder.Ignore(t => t.DomainEvents);
        builder.Ignore(t => t.Version);
    }
}
