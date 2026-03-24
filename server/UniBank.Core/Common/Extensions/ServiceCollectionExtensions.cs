using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniBank.Core.Common.Caching;
using UniBank.Core.Common.Messaging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.Core.Modules.Accounts.Application.Handlers;
using UniBank.Core.Modules.Accounts.Application.Interfaces;
using UniBank.Core.Modules.Accounts.Application.Validators;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreModules(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=unibank;Username=unibank;Password=unibank_dev_password";

        services.AddDbContext<PublicDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddDbContext<UniBankDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, GrpcTenantProvider>();

        // Cache store (replaces Redis — backed by PostgreSQL cache_entries table)
        services.AddScoped<ICacheStore, PostgresCacheStore>();

        // STORY-007: In-process messaging (replaces WolverineFx until .NET 10 support is available)
        services.AddInProcessMessaging(typeof(AccountCreatedHandler).Assembly);

        // STORY-007: Transactional outbox for reliable message delivery
        services.AddScoped<IOutbox, PostgresOutbox>();
        services.AddHostedService<OutboxProcessor>();

        // STORY-009: Registration and OTP services
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<ISmsGateway, MockSmsGateway>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<PinHashingService>();
        services.AddScoped<Modules.Accounts.Infrastructure.Services.VirtualCardGenerator>();
        services.AddScoped<RegisterHandler>();
        services.AddScoped<VerifyOtpHandler>();

        // STORY-009: FluentValidation validators
        services.AddScoped<IValidator<RegisterCommand>, RegisterCommandValidator>();
        services.AddScoped<IValidator<VerifyOtpCommand>, VerifyOtpCommandValidator>();

        // STORY-010: PIN creation
        services.AddScoped<Modules.Accounts.Application.Handlers.CreatePINHandler>();

        // STORY-018: Authentication, token management, and lockout
        services.AddScoped<Modules.Accounts.Application.Handlers.AuthenticateHandler>();
        services.AddScoped<Modules.Accounts.Application.Handlers.RefreshTokenHandler>();
        services.AddScoped<Modules.Accounts.Application.Handlers.LogoutHandler>();
        services.AddScoped<Modules.Accounts.Infrastructure.Services.LockoutService>();
        services.AddScoped<Modules.Accounts.Infrastructure.Services.SessionService>();
        services.AddScoped<Modules.Accounts.Infrastructure.Services.TransactionAuthorizationService>();
        services.AddScoped<IValidator<Modules.Accounts.Application.Commands.AuthenticateCommand>,
            Modules.Accounts.Application.Validators.AuthenticateCommandValidator>();

        // STORY-014: Device transfer
        services.AddScoped<Modules.Accounts.Application.Handlers.DeviceTransferHandler>();
        services.AddScoped<IValidator<Modules.Accounts.Application.Commands.InitiateDeviceTransferCommand>,
            Modules.Accounts.Application.Validators.InitiateDeviceTransferValidator>();
        services.AddScoped<IValidator<Modules.Accounts.Application.Commands.CompleteDeviceTransferCommand>,
            Modules.Accounts.Application.Validators.CompleteDeviceTransferValidator>();

        // Profile, balance, and transaction queries
        services.AddScoped<Modules.Accounts.Application.Handlers.GetProfileHandler>();
        services.AddScoped<Modules.Accounts.Application.Handlers.UpdateProfileHandler>();
        services.AddScoped<Modules.Accounts.Application.Handlers.GetBalanceHandler>();
        services.AddScoped<Modules.Accounts.Application.Handlers.GetTransactionsHandler>();

        // Payments module (Sprint 3-4)
        services.AddScoped<Modules.Payments.Application.Handlers.TokenizeCardHandler>();
        services.AddScoped<Modules.Payments.Application.Handlers.NfcPaymentHandler>();
        services.AddScoped<Modules.Payments.Application.Handlers.ConfirmPaymentHandler>();
        services.AddScoped<Modules.Payments.Application.Handlers.GenerateQrHandler>();
        services.AddScoped<Modules.Payments.Application.Handlers.QrPaymentHandler>();
        services.AddScoped<Modules.Payments.Application.Handlers.PaymentNotificationHandler>();
        services.AddScoped<Modules.Payments.Infrastructure.Services.EmvQrCodeService>();

        // Transfers module (Sprint 4)
        services.AddScoped<Modules.Transfers.Application.Handlers.P2PTransferHandler>();
        services.AddScoped<Modules.Transfers.Application.Handlers.CrossBorderTransferHandler>();
        services.AddScoped<Modules.Transfers.Infrastructure.Services.ExchangeRateService>();

        // BillPay module (Sprint 4)
        services.AddScoped<Modules.BillPay.Application.Handlers.ListProvidersHandler>();
        services.AddScoped<Modules.BillPay.Application.Handlers.PayBillHandler>();
        services.AddScoped<Modules.BillPay.Application.Handlers.SaveBillerHandler>();
        services.AddScoped<Modules.BillPay.Application.Handlers.GetSavedBillersHandler>();

        // Agents module (Sprint 4)
        services.AddScoped<Modules.Agents.Application.Handlers.CashInHandler>();
        services.AddScoped<Modules.Agents.Application.Handlers.CashOutHandler>();
        services.AddScoped<Modules.Agents.Application.Handlers.GetFloatBalanceHandler>();
        services.AddScoped<Modules.Agents.Application.Handlers.GetCommissionReportHandler>();
        services.AddScoped<Modules.Agents.Application.Handlers.GetTransactionReceiptHandler>();
        services.AddScoped<Modules.Agents.Infrastructure.Services.CommissionEngine>();
        services.AddScoped<Modules.Agents.Infrastructure.Services.TariffEngine>();

        // KYC module (Sprint 3)
        services.AddScoped<Modules.KYC.Application.Handlers.UploadDocumentHandler>();
        services.AddScoped<Modules.KYC.Application.Handlers.UploadSelfieHandler>();
        services.AddScoped<Modules.KYC.Application.Handlers.ActivateAccountOnKycHandler>();
        services.AddScoped<IValidator<Modules.KYC.Application.Commands.UploadDocumentCommand>,
            Modules.KYC.Application.Validators.UploadDocumentCommandValidator>();
        services.AddScoped<IValidator<Modules.KYC.Application.Commands.UploadSelfieCommand>,
            Modules.KYC.Application.Validators.UploadSelfieCommandValidator>();
        services.AddScoped<Modules.KYC.Infrastructure.Services.DocumentEncryptionService>();
        services.AddScoped<Modules.KYC.Infrastructure.Services.DocumentStorageService>();
        services.AddScoped<Modules.KYC.Infrastructure.Services.PhotoComparisonService>();

        // Merchants module (Sprint 5-6)
        services.AddScoped<Modules.Merchants.Application.Handlers.RegisterMerchantHandler>();
        services.AddScoped<Modules.Merchants.Application.Handlers.GetMerchantProfileHandler>();
        services.AddScoped<Modules.Merchants.Application.Handlers.UpdateMerchantProfileHandler>();
        services.AddScoped<Modules.Merchants.Application.Handlers.GetSettlementHandler>();
        services.AddScoped<Modules.Merchants.Application.Handlers.GetMerchantTransactionsHandler>();
        services.AddScoped<Modules.Merchants.Application.Handlers.GetMerchantCommissionHandler>();
        services.AddScoped<IValidator<Modules.Merchants.Application.Commands.RegisterMerchantCommand>,
            Modules.Merchants.Application.Validators.RegisterMerchantCommandValidator>();
        services.AddScoped<Modules.Merchants.Infrastructure.Services.MerchantIdGenerator>();

        // Loans module
        services.AddScoped<Modules.Loans.Application.Handlers.ApplyForLoanHandler>();
        services.AddScoped<Modules.Loans.Application.Handlers.GetLoanHandler>();
        services.AddScoped<Modules.Loans.Application.Handlers.ListLoansHandler>();
        services.AddScoped<Modules.Loans.Application.Handlers.GetLoanScheduleHandler>();
        services.AddScoped<Modules.Loans.Infrastructure.Services.CreditScoringEngine>();

        // Sprint 7 - Admin module (STORY-055 to STORY-061)
        services.AddScoped<Modules.Admin.Application.Handlers.AuthenticateAdminHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.CreateAuditLogHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.SearchCustomersHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.ManageAccountHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.ManageMerchantHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.SearchTransactionsHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.ReviewKycHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.UpdateSystemConfigHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.CreateDisputeHandler>();
        services.AddScoped<Modules.Admin.Application.Handlers.ResolveDisputeHandler>();

        // Sprint 8 - Fraud Detection module (STORY-072)
        services.AddScoped<Modules.FraudDetection.Application.Handlers.GetFraudAlertsHandler>();
        services.AddScoped<Modules.FraudDetection.Application.Handlers.ReviewFraudAlertHandler>();
        services.AddScoped<Modules.FraudDetection.Application.Handlers.EvaluateTransactionHandler>();
        services.AddScoped<Modules.FraudDetection.Application.Services.FraudDetectionEngine>();

        // Sprint 6 - WhiteLabel module (STORY-068, STORY-069, STORY-070)
        services.AddScoped<Modules.WhiteLabel.Application.Handlers.GetBrandingHandler>();
        services.AddScoped<Modules.WhiteLabel.Application.Handlers.UpdateBrandingHandler>();
        services.AddScoped<Modules.WhiteLabel.Application.Handlers.GetFeeConfigHandler>();
        services.AddScoped<Modules.WhiteLabel.Application.Handlers.UpdateFeeConfigHandler>();
        services.AddScoped<Modules.WhiteLabel.Application.Handlers.VerifyTenantIsolationHandler>();
        services.AddScoped<Modules.WhiteLabel.Infrastructure.TenantIsolationMiddleware>();

        // Sprint 7 - Admin tenant access (STORY-071)
        services.AddScoped<Modules.Admin.Application.Handlers.TenantAdminAccessHandler>();
        services.AddScoped<Modules.Admin.Infrastructure.TenantAdminFilter>();

        // Sprint 8 - Security hardening (STORY-075)
        services.AddScoped<Modules.Security.Application.Services.SecurityAuditService>();
        // Note: RateLimitingMiddleware is convention-based middleware (takes RequestDelegate in ctor).
        // It must be added via app.UseMiddleware<>() in the HTTP pipeline, not registered as a DI service.
        // The Gateway uses RateLimitInterceptor for gRPC rate limiting instead.

        // Sprint 8 - Pilot health checks (STORY-076)
        services.AddScoped<Modules.Health.HealthCheckService>();

        // Sprint 11-14 - AI module (EPIC-017)
        services.Configure<Modules.AI.Infrastructure.Services.OllamaSettings>(
            configuration.GetSection(Modules.AI.Infrastructure.Services.OllamaSettings.SectionName));
        services.Configure<Modules.AI.Infrastructure.Services.FaceMatchingSettings>(
            configuration.GetSection(Modules.AI.Infrastructure.Services.FaceMatchingSettings.SectionName));
        services.AddHttpClient("Ollama", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                configuration.GetValue("Ollama:TimeoutSeconds", 120));
        });
        services.AddScoped<Modules.AI.Infrastructure.Services.OllamaClient>();
        services.AddSingleton<Modules.AI.Infrastructure.Services.FaceMatchingService>();
        services.AddScoped<Modules.AI.Infrastructure.Services.DocumentOcrService>();
        services.AddScoped<Modules.AI.Application.Handlers.VerifyIdentityHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.ExtractDocumentFieldsHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.VerifyProofOfAddressHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.GetModelStatusHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.ChatHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.ExtractChequeFieldsHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.ExtractBillFieldsHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.ExtractReceiptFieldsHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.GetSpendingInsightsHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.CheckLoanEligibilityHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.VerifyLoanDocumentsHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.TriageDisputeHandler>();
        services.AddScoped<Modules.AI.Application.Handlers.ExplainFraudAlertHandler>();

        // Sprint 9 - Card Transactions module (STORY-077 through STORY-083)
        services.AddScoped<Modules.CardTransactions.Application.Handlers.ProcessPurchaseHandler>();
        services.AddScoped<Modules.CardTransactions.Application.Handlers.ProcessDepositHandler>();
        services.AddScoped<Modules.CardTransactions.Application.Handlers.BalanceEnquiryHandler>();
        services.AddScoped<Modules.CardTransactions.Application.Handlers.StatementEnquiryHandler>();
        services.AddScoped<Modules.CardTransactions.Application.Validators.CardTransactionValidator>();

        // Sprint 22 - Asset Custody module (EPIC-020, STORY-137)
        // AssetGrpcService depends only on UniBankDbContext, ITenantProvider, and ILogger —
        // all of which are already registered above. No additional handler registrations needed
        // for this story; handlers for OCR (STORY-138) and valuation workflow (STORY-143) follow.

        // STORY-138: Receipt OCR handler
        // STORY-140: Daily price feed and asset valuation services
        services.AddScoped<Modules.AssetCustody.Application.Handlers.ExtractReceiptHandler>();
        services.AddScoped<Modules.AssetCustody.Infrastructure.Services.PriceFeedService>();
        services.AddScoped<Modules.AssetCustody.Infrastructure.Services.AssetValuationService>();

        // STORY-144: Certificate verification (automated + on-demand)
        // STORY-145: Asset release request and admin approval workflow
        services.AddScoped<Modules.AssetCustody.Infrastructure.Services.CertificateVerificationService>();
        services.AddScoped<Modules.AssetCustody.Application.Handlers.AssetReleaseHandler>();

        return services;
    }
}
