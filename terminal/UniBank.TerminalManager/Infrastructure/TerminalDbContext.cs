using Microsoft.EntityFrameworkCore;
using UniBank.TerminalManager.Domain;

namespace UniBank.TerminalManager.Infrastructure;

/// <summary>
/// Separate EF Core DbContext for terminal data (STORY-046).
/// Uses schema isolation from the Core DbContext to keep terminal management
/// data independent of the main banking domain.
/// </summary>
public sealed class TerminalDbContext : DbContext
{
    public TerminalDbContext(DbContextOptions<TerminalDbContext> options) : base(options)
    {
    }

    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<TerminalUpdate> TerminalUpdates => Set<TerminalUpdate>();
    public DbSet<TerminalKeyInfo> TerminalKeyInfos => Set<TerminalKeyInfo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("terminal_mgmt");

        modelBuilder.Entity<Terminal>(entity =>
        {
            entity.ToTable("terminals");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.MerchantId).IsRequired().HasColumnName("merchant_id");
            entity.Property(t => t.TenantId).IsRequired().HasMaxLength(100).HasColumnName("tenant_id");
            entity.Property(t => t.SerialNumber).IsRequired().HasMaxLength(100).HasColumnName("serial_number");
            entity.Property(t => t.Model).IsRequired().HasMaxLength(100).HasColumnName("model");
            entity.Property(t => t.FirmwareVersion).IsRequired().HasMaxLength(50).HasColumnName("firmware_version");
            entity.Property(t => t.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
            entity.Property(t => t.Location).HasMaxLength(500).HasColumnName("location");
            entity.Property(t => t.MqttTopicPrefix).IsRequired().HasMaxLength(200).HasColumnName("mqtt_topic_prefix");
            entity.Property(t => t.IpAddress).HasMaxLength(45).HasColumnName("ip_address");
            entity.Property(t => t.LastHeartbeat).HasColumnName("last_heartbeat");
            entity.Property(t => t.LastKeyInjection).HasColumnName("last_key_injection");
            entity.Property(t => t.CreatedAt).HasColumnName("created_at");
            entity.Property(t => t.ActivatedAt).HasColumnName("activated_at");

            entity.HasIndex(t => t.SerialNumber).IsUnique().HasDatabaseName("ix_terminals_serial");
            entity.HasIndex(t => t.TenantId).HasDatabaseName("ix_terminals_tenant");
            entity.HasIndex(t => t.MerchantId).HasDatabaseName("ix_terminals_merchant");
            entity.HasIndex(t => t.Status).HasDatabaseName("ix_terminals_status");
        });

        modelBuilder.Entity<TerminalUpdate>(entity =>
        {
            entity.ToTable("terminal_updates");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.TerminalId).IsRequired().HasColumnName("terminal_id");
            entity.Property(u => u.UpdateType).IsRequired().HasMaxLength(30).HasColumnName("update_type");
            entity.Property(u => u.Version).IsRequired().HasMaxLength(50).HasColumnName("version");
            entity.Property(u => u.Status).IsRequired().HasMaxLength(30).HasColumnName("status");
            entity.Property(u => u.PushedAt).HasColumnName("pushed_at");
            entity.Property(u => u.AppliedAt).HasColumnName("applied_at");

            entity.HasIndex(u => u.TerminalId).HasDatabaseName("ix_terminal_updates_terminal");
            entity.HasIndex(u => new { u.TerminalId, u.Status }).HasDatabaseName("ix_terminal_updates_terminal_status");
        });

        modelBuilder.Entity<TerminalKeyInfo>(entity =>
        {
            entity.ToTable("terminal_key_infos");
            entity.HasKey(k => k.Id);

            entity.Property(k => k.TerminalId).IsRequired().HasColumnName("terminal_id");
            entity.Property(k => k.MasterKeyId).IsRequired().HasMaxLength(100).HasColumnName("master_key_id");
            entity.Property(k => k.ActiveSessionKeyId).HasMaxLength(100).HasColumnName("active_session_key_id");
            entity.Property(k => k.LastRotation).HasColumnName("last_rotation");
            entity.Property(k => k.NextRotation).HasColumnName("next_rotation");

            entity.HasIndex(k => k.TerminalId).IsUnique().HasDatabaseName("ix_terminal_key_infos_terminal");
        });

        base.OnModelCreating(modelBuilder);
    }
}
