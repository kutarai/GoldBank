using Microsoft.EntityFrameworkCore;
using UniBank.SharedKernel.Messaging;

namespace UniBank.Core.Common.Persistence;

public class PublicDbContext : DbContext
{
    public PublicDbContext(DbContextOptions<PublicDbContext> options) : base(options) { }

    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<CacheEntry> CacheEntries => Set<CacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("bank");

        modelBuilder.Entity<TenantEntity>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.SchemaName).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(100);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.Property(e => e.Error).HasMaxLength(2000);
            entity.HasIndex(e => new { e.ProcessedAt, e.RetryCount, e.CreatedAt })
                .HasDatabaseName("ix_outbox_messages_pending");
        });

        modelBuilder.Entity<CacheEntry>(entity =>
        {
            entity.ToTable("cache_entries");
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(500);
            entity.Property(e => e.Value).IsRequired();
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("ix_cache_entries_expiry");
        });

        base.OnModelCreating(modelBuilder);
    }
}

public class TenantEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string SchemaName { get; set; } = default!;
    public string ConfigJson { get; set; } = "{}";
    public string BrandingJson { get; set; } = "{}";
    public string Status { get; set; } = "active";
    public int MaxUsers { get; set; } = 1000000;
    public string CountryCode { get; set; } = "ZWE";
    public string CurrencyCode { get; set; } = "ZWG";
    public string Timezone { get; set; } = "Africa/Harare";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BillProviderEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Config { get; set; } = "{}";
    public string[] Countries { get; set; } = ["ZWE"];
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SystemConfigEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public Guid? TenantId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CacheEntry
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
