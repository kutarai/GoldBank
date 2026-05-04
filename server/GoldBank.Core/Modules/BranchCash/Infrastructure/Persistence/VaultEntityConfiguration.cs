using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.BranchCash.Domain.Entities;

namespace GoldBank.Core.Modules.BranchCash.Infrastructure.Persistence;

public sealed class VaultEntityConfiguration : IEntityTypeConfiguration<Vault>
{
    public void Configure(EntityTypeBuilder<Vault> b)
    {
        b.ToTable("vaults");
        b.HasKey(v => v.Id);
        b.Property(v => v.BranchId).IsRequired().HasColumnName("branch_id");
        b.Property(v => v.Name).IsRequired().HasMaxLength(100).HasColumnName("name");
        b.Property(v => v.VaultManagerId).HasColumnName("vault_manager_id");
        b.Property(v => v.SpotCheckCron).IsRequired().HasMaxLength(100).HasColumnName("spot_check_cron");
        b.Property(v => v.LastSpotCheckAt).HasColumnName("last_spot_check_at");
        b.Property(v => v.LastSpotCheckResult).IsRequired().HasMaxLength(20).HasColumnName("last_spot_check_result");
        b.Property(v => v.IsActive).HasColumnName("is_active");
        b.Property(v => v.TenantId).IsRequired().HasColumnName("tenant_id");
        b.Property(v => v.CreatedAt).HasColumnName("created_at");
        b.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(v => v.BranchId).IsUnique().HasDatabaseName("ux_vaults_branch_id");
        b.Ignore(v => v.DomainEvents);
        b.Ignore(v => v.Version);
    }
}

public sealed class VaultDenominationStockEntityConfiguration : IEntityTypeConfiguration<VaultDenominationStock>
{
    public void Configure(EntityTypeBuilder<VaultDenominationStock> b)
    {
        b.ToTable("vault_denomination_stock");
        b.HasKey(s => s.Id);
        b.Property(s => s.VaultId).IsRequired().HasColumnName("vault_id");
        b.Property(s => s.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        b.Property(s => s.DenominationId).IsRequired().HasColumnName("denomination_id");
        b.Property(s => s.Count).IsRequired().HasColumnName("count");
        b.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        b.Property(s => s.CreatedAt).HasColumnName("created_at");
        b.HasIndex(s => new { s.VaultId, s.DenominationId }).IsUnique().HasDatabaseName("ux_vault_stock_vault_denom");
        b.HasIndex(s => new { s.VaultId, s.Currency }).HasDatabaseName("ix_vault_stock_vault_currency");
        b.Ignore(s => s.DomainEvents);
        b.Ignore(s => s.Version);
    }
}

public sealed class VaultMovementEntityConfiguration : IEntityTypeConfiguration<VaultMovement>
{
    public void Configure(EntityTypeBuilder<VaultMovement> b)
    {
        b.ToTable("vault_movements");
        b.HasKey(m => m.Id);
        b.Property(m => m.VaultId).IsRequired().HasColumnName("vault_id");
        b.Property(m => m.Type).IsRequired().HasMaxLength(30).HasColumnName("type");
        b.Property(m => m.Direction).IsRequired().HasMaxLength(5).HasColumnName("direction");
        b.Property(m => m.Currency).IsRequired().HasMaxLength(3).HasColumnName("currency");
        b.Property(m => m.TotalAmount).IsRequired().HasColumnType("numeric(18,2)").HasColumnName("total_amount");
        b.Property(m => m.DenominationBreakdownJson).IsRequired().HasColumnType("jsonb").HasColumnName("denomination_breakdown_json");
        b.Property(m => m.TellerId).HasColumnName("teller_id");
        b.Property(m => m.DrawerSessionId).HasColumnName("drawer_session_id");
        b.Property(m => m.PerformedBy).IsRequired().HasColumnName("performed_by");
        b.Property(m => m.WitnessId).HasColumnName("witness_id");
        b.Property(m => m.Reference).HasMaxLength(30).HasColumnName("reference");
        b.Property(m => m.Notes).HasMaxLength(1000).HasColumnName("notes");
        b.Property(m => m.ReceiptPdfPath).HasMaxLength(500).HasColumnName("receipt_pdf_path");
        b.Property(m => m.TenantId).IsRequired().HasColumnName("tenant_id");
        b.Property(m => m.CreatedAt).HasColumnName("created_at");
        b.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(m => m.CreatedAt).HasDatabaseName("ix_vault_movements_created_at");
        b.HasIndex(m => new { m.VaultId, m.Currency, m.CreatedAt }).HasDatabaseName("ix_vault_movements_vault_ccy_date");
        b.HasIndex(m => new { m.TellerId, m.CreatedAt }).HasDatabaseName("ix_vault_movements_teller_date");
        b.HasIndex(m => m.DrawerSessionId).HasDatabaseName("ix_vault_movements_drawer_session");
        b.Ignore(m => m.DomainEvents);
        b.Ignore(m => m.Version);
    }
}

public sealed class VaultSpotCheckEntityConfiguration : IEntityTypeConfiguration<VaultSpotCheck>
{
    public void Configure(EntityTypeBuilder<VaultSpotCheck> b)
    {
        b.ToTable("vault_spot_checks");
        b.HasKey(s => s.Id);
        b.Property(s => s.VaultId).IsRequired().HasColumnName("vault_id");
        b.Property(s => s.PerformedBy).IsRequired().HasColumnName("performed_by");
        b.Property(s => s.WitnessId).IsRequired().HasColumnName("witness_id");
        b.Property(s => s.ExpectedJson).IsRequired().HasColumnType("jsonb").HasColumnName("expected_json");
        b.Property(s => s.ActualJson).IsRequired().HasColumnType("jsonb").HasColumnName("actual_json");
        b.Property(s => s.VarianceJson).IsRequired().HasColumnType("jsonb").HasColumnName("variance_json");
        b.Property(s => s.HasVariance).IsRequired().HasColumnName("has_variance");
        b.Property(s => s.AdjustmentMovementId).HasColumnName("adjustment_movement_id");
        b.Property(s => s.ReportPdfPath).HasMaxLength(500).HasColumnName("report_pdf_path");
        b.Property(s => s.TenantId).IsRequired().HasColumnName("tenant_id");
        b.Property(s => s.CreatedAt).HasColumnName("created_at");
        b.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(s => new { s.VaultId, s.CreatedAt }).HasDatabaseName("ix_vault_spot_checks_vault_date");
        b.Ignore(s => s.DomainEvents);
        b.Ignore(s => s.Version);
    }
}
