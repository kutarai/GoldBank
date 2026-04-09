using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.KYC.Domain.Entities;

namespace UniBank.Core.Modules.KYC.Infrastructure.Persistence;

public sealed class KycDocumentEntityConfiguration : IEntityTypeConfiguration<KycDocument>
{
    public void Configure(EntityTypeBuilder<KycDocument> builder)
    {
        builder.ToTable("kyc_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(d => d.DocumentType).IsRequired().HasMaxLength(50).HasColumnName("document_type");
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(255).HasColumnName("file_name");
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(50).HasColumnName("content_type");
        builder.Property(d => d.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(d => d.FilePath).IsRequired().HasMaxLength(500).HasColumnName("file_path");
        builder.Property(d => d.EncryptionKeyRef).IsRequired().HasMaxLength(255).HasColumnName("encryption_key_ref");
        builder.Property(d => d.ChecksumSha256).IsRequired().HasMaxLength(64).HasColumnName("checksum_sha256");
        builder.Property(d => d.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
        builder.Property(d => d.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(d => d.FileData).HasColumnType("bytea").HasColumnName("file_data");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.VerifiedAt).HasColumnName("verified_at");

        builder.HasIndex(d => d.AccountId).HasDatabaseName("ix_kyc_documents_account_id");
        builder.HasIndex(d => d.Status).HasDatabaseName("ix_kyc_documents_status");

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.Version);
    }
}
