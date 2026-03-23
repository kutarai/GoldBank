using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.CardTransactions.Domain.Entities;

namespace UniBank.Core.Modules.CardTransactions.Infrastructure.Persistence;

/// <summary>
/// EF Core entity configuration for the CardTransaction aggregate root (EPIC-015).
/// Maps to the "card_transactions" table in the tenant schema with indexes on key query fields.
/// </summary>
public sealed class CardTransactionEntityConfiguration : IEntityTypeConfiguration<CardTransaction>
{
    public void Configure(EntityTypeBuilder<CardTransaction> builder)
    {
        builder.ToTable("card_transactions");
        builder.HasKey(ct => ct.Id);

        builder.Property(ct => ct.AccountId)
            .IsRequired()
            .HasColumnName("account_id");

        builder.Property(ct => ct.MerchantAccountId)
            .HasColumnName("merchant_account_id");

        builder.Property(ct => ct.MerchantId)
            .HasMaxLength(15)
            .HasColumnName("merchant_id");

        builder.Property(ct => ct.MerchantName)
            .HasMaxLength(200)
            .HasColumnName("merchant_name");

        builder.Property(ct => ct.TransactionType)
            .IsRequired()
            .HasMaxLength(30)
            .HasColumnName("transaction_type");

        builder.Property(ct => ct.Amount)
            .HasPrecision(18, 2)
            .HasColumnName("amount");

        builder.Property(ct => ct.Fee)
            .HasPrecision(18, 2)
            .HasColumnName("fee");

        builder.Property(ct => ct.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasColumnName("currency");

        builder.Property(ct => ct.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("status");

        builder.Property(ct => ct.ResponseCode)
            .HasMaxLength(4)
            .HasColumnName("response_code");

        builder.Property(ct => ct.AuthorizationCode)
            .HasMaxLength(12)
            .HasColumnName("authorization_code");

        builder.Property(ct => ct.Reference)
            .HasMaxLength(50)
            .HasColumnName("reference");

        builder.Property(ct => ct.RetrievalReference)
            .HasMaxLength(12)
            .HasColumnName("retrieval_reference");

        builder.Property(ct => ct.Stan)
            .HasMaxLength(12)
            .HasColumnName("stan");

        builder.Property(ct => ct.TerminalId)
            .HasMaxLength(16)
            .HasColumnName("terminal_id");

        builder.Property(ct => ct.ProcessingCode)
            .HasMaxLength(6)
            .HasColumnName("processing_code");

        builder.Property(ct => ct.SourceInstitution)
            .HasMaxLength(20)
            .HasColumnName("source_institution");

        builder.Property(ct => ct.AcquiringInstitution)
            .HasMaxLength(20)
            .HasColumnName("acquiring_institution");

        builder.Property(ct => ct.BalanceAfter)
            .HasPrecision(18, 2)
            .HasColumnName("balance_after");

        builder.Property(ct => ct.TenantId)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnName("tenant_id");

        builder.Property(ct => ct.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(ct => ct.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(ct => ct.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(ct => ct.DeletedAt)
            .HasColumnName("deleted_at");

        // Indexes
        builder.HasIndex(ct => new { ct.TenantId, ct.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_card_transactions_tenant_created");

        builder.HasIndex(ct => new { ct.AccountId, ct.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_card_transactions_account_created");

        builder.HasIndex(ct => ct.Reference)
            .HasDatabaseName("ix_card_transactions_reference");

        builder.HasIndex(ct => ct.RetrievalReference)
            .HasDatabaseName("ix_card_transactions_retrieval_ref");

        builder.HasIndex(ct => new { ct.Stan, ct.SourceInstitution })
            .HasDatabaseName("ix_card_transactions_stan_source");

        builder.Ignore(ct => ct.DomainEvents);
        builder.Ignore(ct => ct.Version);
    }
}
