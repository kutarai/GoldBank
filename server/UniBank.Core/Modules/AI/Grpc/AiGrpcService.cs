using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Application.Handlers;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.Protos.AI;
using ProtoIdDocumentFields = UniBank.Protos.AI.IdDocumentFields;
using ProtoPayslipFields = UniBank.Protos.AI.PayslipDocumentFields;
using ProtoProofOfAddressFields = UniBank.Protos.AI.ProofOfAddressDocumentFields;
using ProtoChequeFields = UniBank.Protos.AI.ChequeDocumentFields;
using ProtoBillFields = UniBank.Protos.AI.BillDocumentFields;
using ProtoReceiptFields = UniBank.Protos.AI.ReceiptDocumentFields;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.AI.Grpc;

public sealed class AiGrpcService : AIService.AIServiceBase
{
    private readonly VerifyIdentityHandler _verifyIdentityHandler;
    private readonly ExtractDocumentFieldsHandler _extractDocumentFieldsHandler;
    private readonly VerifyProofOfAddressHandler _verifyProofOfAddressHandler;
    private readonly ExtractChequeFieldsHandler _extractChequeFieldsHandler;
    private readonly ExtractBillFieldsHandler _extractBillFieldsHandler;
    private readonly ChatHandler _chatHandler;
    private readonly ExtractReceiptFieldsHandler _extractReceiptFieldsHandler;
    private readonly GetSpendingInsightsHandler _getSpendingInsightsHandler;
    private readonly CheckLoanEligibilityHandler _checkLoanEligibilityHandler;
    private readonly VerifyLoanDocumentsHandler _verifyLoanDocumentsHandler;
    private readonly TriageDisputeHandler _triageDisputeHandler;
    private readonly ExplainFraudAlertHandler _explainFraudAlertHandler;
    private readonly GetModelStatusHandler _getModelStatusHandler;
    private readonly OllamaClient _ollamaClient;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AiGrpcService> _logger;

    public AiGrpcService(
        VerifyIdentityHandler verifyIdentityHandler,
        ExtractDocumentFieldsHandler extractDocumentFieldsHandler,
        VerifyProofOfAddressHandler verifyProofOfAddressHandler,
        ExtractChequeFieldsHandler extractChequeFieldsHandler,
        ExtractBillFieldsHandler extractBillFieldsHandler,
        ChatHandler chatHandler,
        ExtractReceiptFieldsHandler extractReceiptFieldsHandler,
        GetSpendingInsightsHandler getSpendingInsightsHandler,
        CheckLoanEligibilityHandler checkLoanEligibilityHandler,
        VerifyLoanDocumentsHandler verifyLoanDocumentsHandler,
        TriageDisputeHandler triageDisputeHandler,
        ExplainFraudAlertHandler explainFraudAlertHandler,
        GetModelStatusHandler getModelStatusHandler,
        OllamaClient ollamaClient,
        ITenantProvider tenantProvider,
        ILogger<AiGrpcService> logger)
    {
        _verifyIdentityHandler = verifyIdentityHandler;
        _extractDocumentFieldsHandler = extractDocumentFieldsHandler;
        _verifyProofOfAddressHandler = verifyProofOfAddressHandler;
        _extractChequeFieldsHandler = extractChequeFieldsHandler;
        _extractBillFieldsHandler = extractBillFieldsHandler;
        _chatHandler = chatHandler;
        _extractReceiptFieldsHandler = extractReceiptFieldsHandler;
        _getSpendingInsightsHandler = getSpendingInsightsHandler;
        _checkLoanEligibilityHandler = checkLoanEligibilityHandler;
        _verifyLoanDocumentsHandler = verifyLoanDocumentsHandler;
        _triageDisputeHandler = triageDisputeHandler;
        _explainFraudAlertHandler = explainFraudAlertHandler;
        _getModelStatusHandler = getModelStatusHandler;
        _ollamaClient = ollamaClient;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override async Task<VerifyIdentityResponse> VerifyIdentity(
        VerifyIdentityRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));
        if (request.SelfieImage.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "selfie_image is required."));
        if (request.IdDocumentImage.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "id_document_image is required."));

        var result = await _verifyIdentityHandler.HandleAsync(new VerifyIdentityCommand(
            accountId, request.SelfieImage.ToByteArray(), request.IdDocumentImage.ToByteArray(),
            _tenantProvider.GetTenantId()), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        var v = result.Value;
        var response = new VerifyIdentityResponse
        {
            Success = v.Decision != "rejected",
            Message = v.Decision == "auto_approved" ? "Identity verified successfully."
                : v.Decision == "manual_review" ? "Submitted for manual review."
                : v.RejectionReason ?? "Verification failed.",
            Decision = MapDecision(v.Decision),
            FaceMatchScore = v.FaceMatchScore,
            FaceMatchConfidence = v.FaceMatchConfidence,
            NameMatch = v.NameMatch == true ? FieldMatch.Match : v.NameMatch == false ? FieldMatch.Mismatch : FieldMatch.NotAvailable,
            IdNumberMatch = v.IdNumberMatch == true ? FieldMatch.Match : v.IdNumberMatch == false ? FieldMatch.Mismatch : FieldMatch.NotAvailable,
            DobMatch = v.DobMatch == true ? FieldMatch.Match : v.DobMatch == false ? FieldMatch.Mismatch : FieldMatch.NotAvailable,
            RejectionReason = v.RejectionReason ?? "",
        };

        if (v.ExtractedFields is not null)
        {
            response.ExtractedFields = new ProtoIdDocumentFields
            {
                FullName = v.ExtractedFields.FullName ?? "",
                IdNumber = v.ExtractedFields.IdNumber ?? "",
                DateOfBirth = v.ExtractedFields.DateOfBirth ?? "",
                Nationality = v.ExtractedFields.Nationality ?? "",
                Gender = v.ExtractedFields.Gender ?? "",
                ExpiryDate = v.ExtractedFields.ExpiryDate ?? "",
                DocumentType = v.ExtractedFields.DocumentType ?? "",
            };
        }

        return response;
    }

    public override async Task<ExtractDocumentFieldsResponse> ExtractDocumentFields(
        ExtractDocumentFieldsRequest request, ServerCallContext context)
    {
        if (request.DocumentImage.IsEmpty)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "document_image is required."));

        var docType = request.DocumentType switch
        {
            DocumentType.NationalId => "national_id",
            DocumentType.BiometricCard => "biometric_card",
            DocumentType.Passport => "passport",
            DocumentType.Cheque => "cheque",
            DocumentType.UtilityBill => "bill",
            DocumentType.Receipt => "receipt",
            DocumentType.Payslip => "payslip",
            DocumentType.BankStatement => "payslip",
            DocumentType.LeaseAgreement => "proof_of_address",
            _ => throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid document type.")),
        };

        var result = await _extractDocumentFieldsHandler.HandleAsync(new ExtractDocumentFieldsCommand(
            request.DocumentImage.ToByteArray(), docType, _tenantProvider.GetTenantId()),
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var response = new ExtractDocumentFieldsResponse { Success = true, Message = "Extraction successful." };
        var v = result.Value;

        if (v.IdFields is not null)
            response.IdFields = new ProtoIdDocumentFields
            {
                FullName = v.IdFields.FullName ?? "", IdNumber = v.IdFields.IdNumber ?? "",
                DateOfBirth = v.IdFields.DateOfBirth ?? "", Nationality = v.IdFields.Nationality ?? "",
                Gender = v.IdFields.Gender ?? "", DocumentType = v.IdFields.DocumentType ?? "",
            };

        return response;
    }

    public override async Task<ExtractChequeFieldsResponse> ExtractChequeFields(
        ExtractChequeFieldsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _extractChequeFieldsHandler.HandleAsync(new ExtractChequeFieldsCommand(
            accountId, request.ChequeImage.ToByteArray(), _tenantProvider.GetTenantId()),
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var v = result.Value;
        return new ExtractChequeFieldsResponse
        {
            Success = true, Message = "Cheque extracted.",
            AmountConsistent = v.AmountConsistent,
            Fields = new ProtoChequeFields
            {
                ChequeNumber = v.Fields.ChequeNumber ?? "", AmountFigures = v.Fields.AmountFigures ?? "",
                AmountWords = v.Fields.AmountWords ?? "", Payee = v.Fields.Payee ?? "",
                Drawer = v.Fields.Drawer ?? "", BankName = v.Fields.BankName ?? "",
                BranchCode = v.Fields.BranchCode ?? "", Date = v.Fields.Date ?? "",
                Currency = v.Fields.Currency ?? "",
            }
        };
    }

    public override async Task<ExtractBillFieldsResponse> ExtractBillFields(
        ExtractBillFieldsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _extractBillFieldsHandler.HandleAsync(new ExtractBillFieldsCommand(
            accountId, request.BillImage.ToByteArray(), _tenantProvider.GetTenantId()),
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var v = result.Value;
        return new ExtractBillFieldsResponse
        {
            Success = true, Message = "Bill extracted.",
            MatchedProviderId = v.MatchedProviderId ?? "",
            Fields = new ProtoBillFields
            {
                Provider = v.Fields.Provider ?? "", AccountNumber = v.Fields.AccountNumber ?? "",
                AmountDue = v.Fields.AmountDue ?? "", DueDate = v.Fields.DueDate ?? "",
                Reference = v.Fields.Reference ?? "", Currency = v.Fields.Currency ?? "",
            }
        };
    }

    public override async Task Chat(
        ChatRequest request, IServerStreamWriter<ChatResponse> responseStream, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "message is required."));

        var tenantId = _tenantProvider.GetTenantId();
        var accountContext = await _chatHandler.BuildContextAsync(accountId, context.CancellationToken);
        var systemPrompt = _chatHandler.GetSystemPromptWithContext(accountContext);

        var history = request.History
            .Select(h => new OllamaChatMessage { Role = h.Role, Content = h.Content })
            .ToList();

        await foreach (var token in _ollamaClient.ChatStreamAsync(
            systemPrompt, history, request.Message,
            temperature: 0.3, cancellationToken: context.CancellationToken))
        {
            await responseStream.WriteAsync(new ChatResponse { Token = token, Done = false });
        }

        await responseStream.WriteAsync(new ChatResponse { Token = "", Done = true });
    }

    public override async Task<ExtractReceiptFieldsResponse> ExtractReceiptFields(
        ExtractReceiptFieldsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        Guid.TryParse(request.TransactionId, out var txnId);

        var result = await _extractReceiptFieldsHandler.HandleAsync(new ExtractReceiptFieldsCommand(
            accountId, txnId, request.ReceiptImage.ToByteArray(), _tenantProvider.GetTenantId()),
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var v = result.Value;
        return new ExtractReceiptFieldsResponse
        {
            Success = true, Message = "Receipt extracted.",
            TransactionMatched = v.TransactionMatched,
            Fields = new ProtoReceiptFields
            {
                MerchantName = v.Fields.MerchantName ?? "", Date = v.Fields.Date ?? "",
                TotalAmount = v.Fields.TotalAmount ?? "", Currency = v.Fields.Currency ?? "",
                Category = v.Fields.Category ?? "",
            }
        };
    }

    public override async Task<GetSpendingInsightsResponse> GetSpendingInsights(
        GetSpendingInsightsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _getSpendingInsightsHandler.HandleAsync(new GetSpendingInsightsCommand(
            accountId, _tenantProvider.GetTenantId()), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var response = new GetSpendingInsightsResponse
        {
            Success = true,
            GeneratedAt = Timestamp.FromDateTime(DateTime.UtcNow),
        };
        response.Insights.AddRange(result.Value);
        return response;
    }

    public override async Task<CheckLoanEligibilityResponse> CheckLoanEligibility(
        CheckLoanEligibilityRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (!decimal.TryParse(request.DesiredAmount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid desired_amount is required."));

        var result = await _checkLoanEligibilityHandler.HandleAsync(new CheckLoanEligibilityCommand(
            accountId, amount, request.Currency, request.TenureMonths, request.Purpose,
            _tenantProvider.GetTenantId()), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        var v = result.Value;
        return new CheckLoanEligibilityResponse
        {
            Success = true,
            Likelihood = v.Likelihood switch
            {
                "high" => LoanEligibility.High,
                "medium" => LoanEligibility.Medium,
                "low" => LoanEligibility.Low,
                _ => LoanEligibility.Medium,
            },
            EstimatedRateMin = v.EstimatedRateMin,
            EstimatedRateMax = v.EstimatedRateMax,
            Assessment = v.Assessment,
            Disclaimer = v.Disclaimer,
        };
    }

    public override async Task<VerifyLoanDocumentsResponse> VerifyLoanDocuments(
        VerifyLoanDocumentsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (!decimal.TryParse(request.DeclaredIncome, out var declaredIncome))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid declared_income is required."));

        Guid.TryParse(request.LoanApplicationId, out var loanAppId);

        var result = await _verifyLoanDocumentsHandler.HandleAsync(new VerifyLoanDocumentsCommand(
            accountId, loanAppId, request.DocumentImage.ToByteArray(),
            request.DocumentType.ToString(), declaredIncome, _tenantProvider.GetTenantId()),
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        var v = result.Value;
        var response = new VerifyLoanDocumentsResponse
        {
            Success = true,
            Message = v.FlaggedForReview ? "Document flagged for review." : "Document verified.",
            ExtractedIncome = v.ExtractedIncome ?? "",
            DeclaredIncome = v.DeclaredIncome.ToString("F2"),
            VariancePercentage = v.VariancePercentage,
            NameMatch = v.NameMatch ? FieldMatch.Match : FieldMatch.Mismatch,
            IncomeMatch = v.IncomeMatch ? FieldMatch.Match : FieldMatch.Mismatch,
            FlaggedForReview = v.FlaggedForReview,
        };

        if (v.ExtractedFields is not null)
        {
            response.ExtractedFields = new ProtoPayslipFields
            {
                Employer = v.ExtractedFields.Employer ?? "",
                EmployeeName = v.ExtractedFields.EmployeeName ?? "",
                GrossSalary = v.ExtractedFields.GrossSalary ?? "",
                NetSalary = v.ExtractedFields.NetSalary ?? "",
                Currency = v.ExtractedFields.Currency ?? "",
                PayPeriod = v.ExtractedFields.PayPeriod ?? "",
            };
        }

        return response;
    }

    public override async Task<TriageDisputeResponse> TriageDispute(
        TriageDisputeRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));
        if (!Guid.TryParse(request.TransactionId, out var txnId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid transaction_id is required."));
        if (string.IsNullOrWhiteSpace(request.Description))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "description is required."));

        var result = await _triageDisputeHandler.HandleAsync(new TriageDisputeCommand(
            accountId, txnId, request.Description,
            request.EvidenceImage.IsEmpty ? null : request.EvidenceImage.ToByteArray(),
            _tenantProvider.GetTenantId()), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var v = result.Value;
        return new TriageDisputeResponse
        {
            Success = true,
            Message = $"Dispute registered. Reference: {v.Reference}",
            Reference = v.Reference,
            Classification = MapDisputeClassification(v.Classification),
            Priority = MapDisputePriority(v.Priority),
            AssignedTeam = v.AssignedTeam,
            Summary = v.Summary,
            Confidence = v.Confidence,
            ExpectedResolution = v.ExpectedResolution,
        };
    }

    public override async Task<ExplainFraudAlertResponse> ExplainFraudAlert(
        ExplainFraudAlertRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));
        if (!Guid.TryParse(request.TransactionId, out var txnId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid transaction_id is required."));

        var result = await _explainFraudAlertHandler.HandleAsync(new ExplainFraudAlertCommand(
            accountId, txnId, request.FraudRulesTriggered, request.RiskScore,
            _tenantProvider.GetTenantId()), context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        var v = result.Value;
        var response = new ExplainFraudAlertResponse
        {
            Success = true,
            Explanation = v.Explanation,
        };
        response.SuggestedActions.AddRange(v.SuggestedActions);
        return response;
    }

    public override async Task<VerifyProofOfAddressResponse> VerifyProofOfAddress(
        VerifyProofOfAddressRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var result = await _verifyProofOfAddressHandler.HandleAsync(new VerifyProofOfAddressCommand(
            accountId, request.DocumentImage.ToByteArray(), _tenantProvider.GetTenantId()),
            context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, result.Error.Message));

        var v = result.Value;
        return new VerifyProofOfAddressResponse
        {
            Success = true,
            Message = v.Decision == "auto_approved" ? "Address verified." : "Submitted for review.",
            Decision = MapDecision(v.Decision),
            NameMatch = v.NameMatch ? FieldMatch.Match : FieldMatch.Mismatch,
            DocumentDateValid = v.DocumentDateValid,
            NewKycLevel = v.NewKycLevel,
            ExtractedFields = new ProtoProofOfAddressFields
            {
                Name = v.ExtractedFields.Name ?? "",
                Address = v.ExtractedFields.Address ?? "",
                DocumentDate = v.ExtractedFields.DocumentDate ?? "",
                DocumentType = v.ExtractedFields.DocumentType ?? "",
            }
        };
    }

    public override async Task<GetModelStatusResponse> GetModelStatus(
        GetModelStatusRequest request, ServerCallContext context)
    {
        var result = await _getModelStatusHandler.HandleAsync(
            new GetModelStatusCommand(), context.CancellationToken);

        var v = result.Value;
        return new GetModelStatusResponse
        {
            OllamaHealthy = v.OllamaHealthy,
            VisionModel = v.VisionModel,
            VisionModelSize = v.VisionModelSize,
            FaceModelLoaded = v.FaceModelLoaded,
            FaceModelName = v.FaceModelName,
        };
    }

    private static VerificationDecision MapDecision(string decision) => decision switch
    {
        "auto_approved" => VerificationDecision.AutoApproved,
        "manual_review" => VerificationDecision.ManualReview,
        "rejected" => VerificationDecision.Rejected,
        _ => VerificationDecision.Unspecified,
    };

    private static DisputeClassification MapDisputeClassification(string type) => type switch
    {
        "unauthorized_transaction" => DisputeClassification.UnauthorizedTransaction,
        "duplicate_charge" => DisputeClassification.DuplicateCharge,
        "wrong_amount" => DisputeClassification.WrongAmount,
        "service_not_received" => DisputeClassification.ServiceNotReceived,
        "atm_failed_dispensing" => DisputeClassification.AtmFailedDispensing,
        "card_fraud" => DisputeClassification.CardFraud,
        _ => DisputeClassification.Unspecified,
    };

    private static DisputePriority MapDisputePriority(string priority) => priority switch
    {
        "high" => DisputePriority.High,
        "medium" => DisputePriority.Medium,
        "low" => DisputePriority.Low,
        _ => DisputePriority.Unspecified,
    };
}
