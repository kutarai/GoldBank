using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Accounts.Domain.Entities;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Persistence;

public sealed class TransactionEntityConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(t => t.Type).IsRequired().HasMaxLength(50).HasColumnName("type");
        builder.Property(t => t.Amount).HasPrecision(18, 2).HasColumnName("amount");
        builder.Property(t => t.Fee).HasPrecision(18, 2).HasColumnName("fee");
        builder.Property(t => t.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
        builder.Property(t => t.Reference).HasMaxLength(100).HasColumnName("reference");
        builder.Property(t => t.Description).HasMaxLength(500).HasColumnName("description");
        builder.Property(t => t.CounterpartyName).HasMaxLength(200).HasColumnName("counterparty_name");
        builder.Property(t => t.CounterpartyPhone).HasMaxLength(20).HasColumnName("counterparty_phone");
        builder.Property(t => t.BalanceAfter).HasPrecision(18, 2).HasColumnName("balance_after");
        builder.Property(t => t.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        builder.Property(t => t.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(t => t.AccountId).HasDatabaseName("ix_transactions_account_id");
        builder.HasIndex(t => t.CreatedAt).HasDatabaseName("ix_transactions_created_at");
        builder.HasIndex(t => new { t.AccountId, t.CreatedAt }).HasDatabaseName("ix_transactions_account_created");
    }
}
