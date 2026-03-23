using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.AI.Domain.Entities;

namespace UniBank.Core.Modules.AI.Infrastructure.Persistence;

public sealed class AiInteractionEntityConfiguration : IEntityTypeConfiguration<AiInteraction>
{
    public void Configure(EntityTypeBuilder<AiInteraction> builder)
    {
        builder.ToTable("ai_interactions");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountId).HasColumnName("account_id");
        builder.Property(a => a.InteractionType).IsRequired().HasMaxLength(50).HasColumnName("interaction_type");
        builder.Property(a => a.RequestSummary).IsRequired().HasMaxLength(500).HasColumnName("request_summary");
        builder.Property(a => a.ResponseSummary).IsRequired().HasMaxLength(2000).HasColumnName("response_summary");
        builder.Property(a => a.ModelUsed).IsRequired().HasMaxLength(50).HasColumnName("model_used");
        builder.Property(a => a.InferenceTimeMs).HasColumnName("inference_time_ms");
        builder.Property(a => a.Success).HasColumnName("success");
        builder.Property(a => a.ErrorMessage).HasMaxLength(1000).HasColumnName("error_message");
        builder.Property(a => a.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(a => a.AccountId).HasDatabaseName("ix_ai_interactions_account_id");
        builder.HasIndex(a => a.InteractionType).HasDatabaseName("ix_ai_interactions_type");
        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("ix_ai_interactions_created");

        builder.Ignore(a => a.DomainEvents);
    }
}
