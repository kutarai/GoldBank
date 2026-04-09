using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.AI.Domain.Entities;

namespace UniBank.Core.Modules.AI.Infrastructure.Persistence;

public sealed class KycVerificationEntityConfiguration : IEntityTypeConfiguration<KycVerification>
{
    public void Configure(EntityTypeBuilder<KycVerification> builder)
    {
        builder.ToTable("kyc_verifications");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(k => k.SelfieImagePath).IsRequired().HasMaxLength(500).HasColumnName("selfie_image_path");
        builder.Property(k => k.IdDocumentImagePath).IsRequired().HasMaxLength(500).HasColumnName("id_document_image_path");
        builder.Property(k => k.SelfieImageData).HasColumnType("bytea").HasColumnName("selfie_image_data");
        builder.Property(k => k.IdDocumentImageData).HasColumnType("bytea").HasColumnName("id_document_image_data");
        builder.Property(k => k.FaceMatchScore).HasColumnName("face_match_score");
        builder.Property(k => k.FaceMatchDecision).IsRequired().HasMaxLength(20).HasColumnName("face_match_decision");
        builder.Property(k => k.ExtractedFullName).HasMaxLength(200).HasColumnName("extracted_full_name");
        builder.Property(k => k.ExtractedIdNumber).HasMaxLength(50).HasColumnName("extracted_id_number");
        builder.Property(k => k.ExtractedDateOfBirth).HasColumnName("extracted_date_of_birth");
        builder.Property(k => k.ExtractedNationality).HasMaxLength(50).HasColumnName("extracted_nationality");
        builder.Property(k => k.ExtractedGender).HasMaxLength(10).HasColumnName("extracted_gender");
        builder.Property(k => k.NameMatch).HasColumnName("name_match");
        builder.Property(k => k.IdNumberMatch).HasColumnName("id_number_match");
        builder.Property(k => k.DobMatch).HasColumnName("dob_match");
        builder.Property(k => k.OverallDecision).IsRequired().HasMaxLength(20).HasColumnName("overall_decision");
        builder.Property(k => k.RejectionReason).HasMaxLength(500).HasColumnName("rejection_reason");
        builder.Property(k => k.ReviewedBy).HasMaxLength(100).HasColumnName("reviewed_by");
        builder.Property(k => k.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(k => k.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(k => k.DeletedAt).HasColumnName("deleted_at");
        builder.Property(k => k.CreatedAt).HasColumnName("created_at");
        builder.Property(k => k.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(k => k.AccountId).HasDatabaseName("ix_kyc_verifications_account_id");
        builder.HasIndex(k => k.OverallDecision).HasDatabaseName("ix_kyc_verifications_decision");
        builder.HasIndex(k => new { k.AccountId, k.CreatedAt }).HasDatabaseName("ix_kyc_verifications_account_created");

        builder.Ignore(k => k.DomainEvents);
    }
}
