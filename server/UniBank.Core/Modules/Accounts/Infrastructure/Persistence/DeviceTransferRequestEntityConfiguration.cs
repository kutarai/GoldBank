using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniBank.Core.Modules.Accounts.Domain.Entities;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Persistence;

public sealed class DeviceTransferRequestEntityConfiguration : IEntityTypeConfiguration<DeviceTransferRequest>
{
    public void Configure(EntityTypeBuilder<DeviceTransferRequest> builder)
    {
        builder.ToTable("device_transfer_requests");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.AccountId).IsRequired().HasColumnName("account_id");
        builder.Property(d => d.TransferReference).IsRequired().HasMaxLength(100).HasColumnName("transfer_reference");
        builder.Property(d => d.OldDeviceId).IsRequired().HasMaxLength(256).HasColumnName("old_device_id");
        builder.Property(d => d.NewDeviceId).IsRequired().HasMaxLength(256).HasColumnName("new_device_id");
        builder.Property(d => d.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
        builder.Property(d => d.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(d => d.ExpiresAt).HasColumnName("expires_at");
        builder.Property(d => d.CompletedAt).HasColumnName("completed_at");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => d.TransferReference).IsUnique().HasDatabaseName("ix_device_transfers_ref");
        builder.HasIndex(d => d.AccountId).HasDatabaseName("ix_device_transfers_account_id");

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.Version);
    }
}
