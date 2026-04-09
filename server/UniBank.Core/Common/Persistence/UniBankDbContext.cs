using Microsoft.EntityFrameworkCore;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Accounts.Infrastructure.Persistence;
using UniBank.Core.Modules.Agents.Domain.Entities;
using UniBank.Core.Modules.Agents.Infrastructure.Persistence;
using UniBank.Core.Modules.BillPay.Domain.Entities;
using UniBank.Core.Modules.BillPay.Infrastructure.Persistence;
using UniBank.Core.Modules.KYC.Domain.Entities;
using UniBank.Core.Modules.KYC.Infrastructure.Persistence;
using UniBank.Core.Modules.Merchants.Domain.Entities;
using UniBank.Core.Modules.Merchants.Infrastructure.Persistence;
using UniBank.Core.Modules.Payments.Domain.Entities;
using UniBank.Core.Modules.Payments.Infrastructure.Persistence;
using UniBank.Core.Modules.Transfers.Domain.Entities;
using UniBank.Core.Modules.Transfers.Infrastructure.Persistence;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.Core.Modules.Admin.Infrastructure.Persistence;
using UniBank.Core.Modules.FraudDetection.Domain.Entities;
using UniBank.Core.Modules.FraudDetection.Infrastructure.Persistence;
using UniBank.Core.Modules.Loans.Domain.Entities;
using UniBank.Core.Modules.Loans.Infrastructure.Persistence;
using UniBank.Core.Modules.CardTransactions.Domain.Entities;
using UniBank.Core.Modules.CardTransactions.Infrastructure.Persistence;
using UniBank.Core.Modules.WhiteLabel.Domain.Entities;
using UniBank.Core.Modules.WhiteLabel.Infrastructure.Persistence;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Persistence;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;
using UniBank.Core.Modules.AssetCustody.Infrastructure.Persistence;
using UniBank.Core.Modules.BranchCash.Domain.Entities;
using UniBank.Core.Modules.BranchCash.Infrastructure.Persistence;
using UniBank.SharedKernel.Domain;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Common.Persistence;

public class UniBankDbContext : DbContext
{
    private readonly string _tenantSchema;

    public UniBankDbContext(DbContextOptions<UniBankDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        var tenant = tenantProvider.GetTenantInfo();
        _tenantSchema = tenant.SchemaName;
    }

    public UniBankDbContext(DbContextOptions<UniBankDbContext> options, string tenantSchema)
        : base(options)
    {
        _tenantSchema = tenantSchema;
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<DeviceTransferRequest> DeviceTransferRequests => Set<DeviceTransferRequest>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantDocument> MerchantDocuments => Set<MerchantDocument>();
    public DbSet<MerchantSettlement> MerchantSettlements => Set<MerchantSettlement>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentToken> PaymentTokens => Set<PaymentToken>();
    public DbSet<AgentFloat> AgentFloats => Set<AgentFloat>();
    public DbSet<AgentCommission> AgentCommissions => Set<AgentCommission>();
    public DbSet<Transfer> TransferRecords => Set<Transfer>();
    public DbSet<BillProvider> BillProviders => Set<BillProvider>();
    public DbSet<SavedBiller> SavedBillers => Set<SavedBiller>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();

    // Sprint 7 - Admin module (STORY-055 to STORY-061)
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<Dispute> Disputes => Set<Dispute>();

    // Sprint 19 - Branch management (EPIC-019)
    public DbSet<Branch> Branches => Set<Branch>();

    // Sprint 8 - Fraud Detection (STORY-072)
    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();
    public DbSet<FraudRule> FraudRules => Set<FraudRule>();

    // Loans module
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();

    // Sprint 9 - Card Transactions module (EPIC-015)
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();

    // Sprint 11-14 - AI module (EPIC-017)
    public DbSet<KycVerification> KycVerifications => Set<KycVerification>();
    public DbSet<AiInteraction> AiInteractions => Set<AiInteraction>();
    public DbSet<TransactionDispute> TransactionDisputes => Set<TransactionDispute>();

    // Sprint 6 - WhiteLabel module (STORY-068, STORY-070)
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<TenantFeeConfig> TenantFeeConfigs => Set<TenantFeeConfig>();
    public DbSet<TenantTransactionLimit> TenantTransactionLimits => Set<TenantTransactionLimit>();

    // Sprint 22 - Asset Custody module (EPIC-020, STORY-136)
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<DepositHouse> DepositHouses => Set<DepositHouse>();
    public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
    public DbSet<DailyPrice> DailyPrices => Set<DailyPrice>();

    // BranchCash module (STORY-148)
    public DbSet<TellerDrawerSession> TellerDrawerSessions => Set<TellerDrawerSession>();
    public DbSet<BranchCashTransaction> BranchCashTransactions => Set<BranchCashTransaction>();
    public DbSet<CurrencyDenomination> CurrencyDenominations => Set<CurrencyDenomination>();
    public DbSet<Vault> Vaults => Set<Vault>();
    public DbSet<VaultDenominationStock> VaultDenominationStock => Set<VaultDenominationStock>();
    public DbSet<VaultMovement> VaultMovements => Set<VaultMovement>();
    public DbSet<VaultSpotCheck> VaultSpotChecks => Set<VaultSpotCheck>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_tenantSchema);

        // Exclude DomainEvent (owned by BaseEntity in-memory, not persisted)
        modelBuilder.Ignore<DomainEvent>();

        // Accounts module
        modelBuilder.ApplyConfiguration(new AccountEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceTransferRequestEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionEntityConfiguration());

        // KYC module
        modelBuilder.ApplyConfiguration(new KycDocumentEntityConfiguration());

        // Merchants module
        modelBuilder.ApplyConfiguration(new MerchantEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MerchantDocumentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MerchantSettlementEntityConfiguration());

        // Payments module
        modelBuilder.ApplyConfiguration(new PaymentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentTokenEntityConfiguration());

        // Agents module
        modelBuilder.ApplyConfiguration(new AgentFloatEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AgentCommissionEntityConfiguration());

        // Transfers module
        modelBuilder.ApplyConfiguration(new TransferEntityConfiguration());

        // BillPay module
        modelBuilder.ApplyConfiguration(new BillProviderEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SavedBillerEntityConfiguration());
        modelBuilder.ApplyConfiguration(new BillPaymentEntityConfiguration());

        // Loans module
        modelBuilder.ApplyConfiguration(new LoanEntityConfiguration());
        modelBuilder.ApplyConfiguration(new LoanPaymentEntityConfiguration());

        // Sprint 7 - Admin module (STORY-055 to STORY-061)
        modelBuilder.ApplyConfiguration(new AdminUserEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());
        modelBuilder.ApplyConfiguration(new SystemConfigEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DisputeEntityConfiguration());

        // Sprint 19 - Branch management (EPIC-019)
        modelBuilder.ApplyConfiguration(new BranchEntityConfiguration());

        // Sprint 8 - Fraud Detection module (STORY-072)
        modelBuilder.ApplyConfiguration(new FraudAlertEntityConfiguration());
        modelBuilder.ApplyConfiguration(new FraudRuleEntityConfiguration());

        // Sprint 9 - Card Transactions module (EPIC-015)
        modelBuilder.ApplyConfiguration(new CardTransactionEntityConfiguration());

        // Sprint 11-14 - AI module (EPIC-017)
        modelBuilder.ApplyConfiguration(new KycVerificationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AiInteractionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TransactionDisputeEntityConfiguration());

        // Sprint 6 - WhiteLabel module (STORY-068, STORY-070)
        modelBuilder.ApplyConfiguration(new TenantBrandingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TenantFeeConfigEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TenantTransactionLimitEntityConfiguration());

        // Sprint 22 - Asset Custody module (EPIC-020, STORY-136)
        modelBuilder.ApplyConfiguration(new AssetEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DepositHouseEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AssetValuationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DailyPriceEntityConfiguration());

        // BranchCash module (STORY-148)
        modelBuilder.ApplyConfiguration(new TellerDrawerSessionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new BranchCashTransactionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CurrencyDenominationEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VaultEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VaultDenominationStockEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VaultMovementEntityConfiguration());
        modelBuilder.ApplyConfiguration(new VaultSpotCheckEntityConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
