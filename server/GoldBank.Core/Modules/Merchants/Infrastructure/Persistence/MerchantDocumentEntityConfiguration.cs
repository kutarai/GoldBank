using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Merchants.Domain.Entities;

namespace GoldBank.Core.Modules.Merchants.Infrastructure.Persistence;

public sealed class MerchantDocumentEntityConfiguration : IEntityTypeConfiguration<MerchantDocument>
{
    public void Configure(EntityTypeBuilder<MerchantDocument> builder)
    {
        builder.ToTable("merchant_documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.MerchantId).IsRequired().HasColumnName("merchant_id");
        builder.Property(d => d.DocumentType).IsRequired().HasMaxLength(50).HasColumnName("document_type");
        builder.Property(d => d.FilePath).IsRequired().HasMaxLength(500).HasColumnName("file_path");
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(255).HasColumnName("file_name");
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(50).HasColumnName("content_type");
        builder.Property(d => d.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(d => d.EncryptionKeyRef).IsRequired().HasMaxLength(255).HasColumnName("encryption_key_ref");
        builder.Property(d => d.ChecksumSha256).IsRequired().HasMaxLength(64).HasColumnName("checksum_sha256");
        builder.Property(d => d.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
        builder.Property(d => d.UploadedAt).HasColumnName("uploaded_at");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => d.MerchantId).HasDatabaseName("ix_merchant_documents_merchant_id");
    }
}
