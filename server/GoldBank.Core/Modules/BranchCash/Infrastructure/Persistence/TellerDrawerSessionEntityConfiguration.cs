using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.BranchCash.Domain.Entities;

namespace GoldBank.Core.Modules.BranchCash.Infrastructure.Persistence;

public sealed class TellerDrawerSessionEntityConfiguration : IEntityTypeConfiguration<TellerDrawerSession>
{
    public void Configure(EntityTypeBuilder<TellerDrawerSession> builder)
    {
        builder.ToTable("teller_drawer_sessions");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TellerId).IsRequired().HasColumnName("teller_id");
        builder.Property(d => d.BranchId).IsRequired().HasColumnName("branch_id");
        builder.Property(d => d.BusinessDate).IsRequired().HasColumnName("business_date");
        builder.Property(d => d.Status).IsRequired().HasMaxLength(20).HasColumnName("status");

        builder.Property(d => d.OpeningFloatJson).IsRequired().HasColumnType("jsonb").HasColumnName("opening_float_json");
        builder.Property(d => d.ClosingBalanceJson).HasColumnType("jsonb").HasColumnName("closing_balance_json");
        builder.Property(d => d.ExpectedClosingJson).HasColumnType("jsonb").HasColumnName("expected_closing_json");
        builder.Property(d => d.VarianceJson).HasColumnType("jsonb").HasColumnName("variance_json");

        builder.Property(d => d.OpenedAt).IsRequired().HasColumnName("opened_at");
        builder.Property(d => d.ClosedAt).HasColumnName("closed_at");
        builder.Property(d => d.ClosedBySupervisorId).HasColumnName("closed_by_supervisor_id");
        builder.Property(d => d.EodReportPath).HasMaxLength(500).HasColumnName("eod_report_path");

        builder.Property(d => d.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(d => new { d.TellerId, d.BusinessDate }).HasDatabaseName("ix_teller_drawer_sessions_teller_date");
        builder.HasIndex(d => d.BranchId).HasDatabaseName("ix_teller_drawer_sessions_branch_id");
        builder.HasIndex(d => d.Status).HasDatabaseName("ix_teller_drawer_sessions_status");

        builder.Ignore(d => d.DomainEvents);
        builder.Ignore(d => d.Version);
    }
}
