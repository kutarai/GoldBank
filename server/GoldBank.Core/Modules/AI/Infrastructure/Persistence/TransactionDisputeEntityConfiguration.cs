using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.AI.Domain.Entities;

namespace GoldBank.Core.Modules.AI.Infrastructure.Persistence;

public sealed class TransactionDisputeEntityConfiguration : IEntityTypeConfiguration<TransactionDispute>
{
    public void Configure(EntityTypeBuilder<TransactionDispute> builder)
    {
        builder.ToTable("transaction_disputes");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(d => d.TransactionId).IsRequired().HasColumnName("transaction_id");
        builder.Property(d => d.UserDescription).IsRequired().HasMaxLength(2000).HasColumnName("user_description");
        builder.Property(d => d.EvidenceImagePath).HasMaxLength(500).HasColumnName("evidence_image_path");
        builder.Property(d => d.DisputeType).IsRequired().HasMaxLength(50).HasColumnName("dispute_type");
        builder.Property(d => d.Priority).IsRequired().HasMaxLength(10).HasColumnName("priority");
        builder.Property(d => d.AiSummary).IsRequired().HasMaxLength(2000).HasColumnName("ai_summary");
        builder.Property(d => d.AiRecommendedAction).IsRequired().HasMaxLength(500).HasColumnName("ai_recommended_action");
        builder.Property(d => d.ClassificationConfidence).HasColumnName("classification_confidence");
        builder.Property(d => d.Status).IsRequired().HasMaxLength(20).HasColumnName("status");
        builder.Property(d => d.AssignedTeam).IsRequired().HasMaxLength(30).HasColumnName("assigned_team");
        builder.Property(d => d.Reference).IsRequired().HasMaxLength(30).HasColumnName("reference");
        builder.Property(d => d.ResolutionNotes).HasMaxLength(2000).HasColumnName("resolution_notes");
        builder.Property(d => d.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(d => d.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => d.AccountId).HasDatabaseName("ix_transaction_disputes_account_id");
        builder.HasIndex(d => d.TransactionId).HasDatabaseName("ix_transaction_disputes_transaction_id");
        builder.HasIndex(d => d.Reference).IsUnique().HasDatabaseName("ix_transaction_disputes_reference_unique");
        builder.HasIndex(d => d.Status).HasDatabaseName("ix_transaction_disputes_status");
        builder.HasIndex(d => new { d.AccountId, d.CreatedAt }).HasDatabaseName("ix_transaction_disputes_account_created");

        builder.Ignore(d => d.DomainEvents);
    }
}
