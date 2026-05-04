using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Accounts.Domain.Entities;

namespace GoldBank.Core.Modules.Accounts.Infrastructure.Persistence;

public sealed class RefreshTokenEntityConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(t => t.Token).IsRequired().HasMaxLength(500).HasColumnName("token");
        builder.Property(t => t.DeviceId).IsRequired().HasMaxLength(256).HasColumnName("device_id");
        builder.Property(t => t.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at");
        builder.Property(t => t.RevokedAt).HasColumnName("revoked_at");
        builder.Property(t => t.ReplacedByToken).HasMaxLength(500).HasColumnName("replaced_by_token");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.Token).IsUnique().HasDatabaseName("ix_refresh_tokens_token");
        builder.HasIndex(t => t.AccountId).HasDatabaseName("ix_refresh_tokens_account_id");
    }
}
