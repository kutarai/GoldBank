using Microsoft.EntityFrameworkCore;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Data;

public class SwitchDbContext : DbContext
{
    public SwitchDbContext(DbContextOptions<SwitchDbContext> options) : base(options)
    {
    }

    public DbSet<TerminalEntity> Terminals => Set<TerminalEntity>();
    public DbSet<MerchantEntity> Merchants => Set<MerchantEntity>();
    public DbSet<TransactionLogEntity> TransactionLogs => Set<TransactionLogEntity>();
    public DbSet<QrPaymentEntity> QrPayments => Set<QrPaymentEntity>();
    public DbSet<MobileMoneyPaymentEntity> MobileMoneyPayments => Set<MobileMoneyPaymentEntity>();
    public DbSet<GatewayEntity> Gateways => Set<GatewayEntity>();
    public DbSet<GatewayBinRouteEntity> GatewayBinRoutes => Set<GatewayBinRouteEntity>();
    public DbSet<GatewayAuditLogEntity> GatewayAuditLogs => Set<GatewayAuditLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TerminalEntity>(entity =>
        {
            entity.HasIndex(e => e.TerminalId).IsUnique();
            entity.Property(e => e.TerminalId).HasMaxLength(20);
            entity.Property(e => e.MerchantId).HasMaxLength(20);
        });

        modelBuilder.Entity<MerchantEntity>(entity =>
        {
            entity.HasIndex(e => e.MerchantId).IsUnique();
            entity.Property(e => e.MerchantId).HasMaxLength(20);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<TransactionLogEntity>(entity =>
        {
            entity.HasIndex(e => e.ExchangeId).IsUnique();
            entity.HasIndex(e => e.RequestTimestamp);
            entity.HasIndex(e => e.TerminalId);
            entity.HasIndex(e => e.ResponseCode);
            entity.Property(e => e.PanLastFour).HasMaxLength(4);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.AuthorisationCode).HasMaxLength(10);
        });

        modelBuilder.Entity<QrPaymentEntity>(entity =>
        {
            entity.HasIndex(e => e.PaymentReference).IsUnique();
            entity.HasIndex(e => e.TerminalId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.PaymentReference).HasMaxLength(50);
            entity.Property(e => e.TerminalId).HasMaxLength(20);
            entity.Property(e => e.MerchantId).HasMaxLength(20);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.AuthorizationCode).HasMaxLength(10);
        });

        modelBuilder.Entity<GatewayEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Host).HasMaxLength(255);
            entity.Property(e => e.AcquiringInstitutionId).HasMaxLength(20);
            entity.Property(e => e.NetworkId).HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<GatewayBinRouteEntity>(entity =>
        {
            entity.HasIndex(e => new { e.GatewayId, e.BinPrefix }).IsUnique();
            entity.Property(e => e.BinPrefix).HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(100);
            entity.HasOne(e => e.Gateway)
                .WithMany(g => g.BinRoutes)
                .HasForeignKey(e => e.GatewayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GatewayAuditLogEntity>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.GatewayId);
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.Property(e => e.PerformedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<MobileMoneyPaymentEntity>(entity =>
        {
            entity.HasIndex(e => e.PaymentReference).IsUnique();
            entity.HasIndex(e => e.TerminalId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.PaymentReference).HasMaxLength(50);
            entity.Property(e => e.TerminalId).HasMaxLength(20);
            entity.Property(e => e.MerchantId).HasMaxLength(20);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.MobileNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.AuthorizationCode).HasMaxLength(10);
        });
    }
}
