using FluentValidation;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.KYC.Application.Commands;
using GoldBank.Core.Modules.KYC.Application.Handlers;
using GoldBank.Core.Modules.KYC.Domain.Entities;
using GoldBank.Protos.KYC;
using GoldBank.SharedKernel.MultiTenancy;

namespace GoldBank.Core.Modules.KYC.Grpc;

/// <summary>
/// gRPC service for KYC operations (STORY-011, STORY-012).
/// Handles document upload, selfie upload, and status queries.
/// </summary>
public sealed class KycGrpcService : KycService.KycServiceBase
{
    private readonly UploadDocumentHandler _uploadHandler;
    private readonly UploadSelfieHandler _selfieHandler;
    private readonly IValidator<UploadDocumentCommand> _uploadValidator;
    private readonly IValidator<UploadSelfieCommand> _selfieValidator;
    private readonly GoldBankDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<KycGrpcService> _logger;

    public KycGrpcService(
        UploadDocumentHandler uploadHandler,
        UploadSelfieHandler selfieHandler,
        IValidator<UploadDocumentCommand> uploadValidator,
        IValidator<UploadSelfieCommand> selfieValidator,
        GoldBankDbContext dbContext,
        ITenantProvider tenantProvider,
        ILogger<KycGrpcService> logger)
    {
        _uploadHandler = uploadHandler;
        _selfieHandler = selfieHandler;
        _uploadValidator = uploadValidator;
        _selfieValidator = selfieValidator;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public override async Task<UploadDocumentResponse> UploadDocument(
        IAsyncStreamReader<UploadDocumentRequest> requestStream,
        ServerCallContext context)
    {
        DocumentMetadata? metadata = null;
        using var dataStream = new MemoryStream();

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            switch (request.PayloadCase)
            {
                case UploadDocumentRequest.PayloadOneofCase.Metadata:
                    metadata = request.Metadata;
                    break;
                case UploadDocumentRequest.PayloadOneofCase.Chunk:
                    await dataStream.WriteAsync(
                        request.Chunk.Memory, context.CancellationToken);
                    break;
            }
        }

        if (metadata is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Document metadata is required."));

        var tenantId = _tenantProvider.GetTenantId();
        var command = new UploadDocumentCommand(
            AccountId: Guid.Parse(metadata.AccountId),
            DocumentType: metadata.DocumentType,
            FileName: metadata.FileName,
            ContentType: metadata.ContentType,
            FileSize: metadata.FileSize,
            FileData: dataStream.ToArray(),
            TenantId: tenantId);

        var validation = await _uploadValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _uploadHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new UploadDocumentResponse
        {
            Success = true,
            DocumentId = result.Value.DocumentId,
            Status = result.Value.Status,
            Message = "Document uploaded successfully."
        };
    }

    public override async Task<UploadSelfieResponse> UploadSelfie(
        IAsyncStreamReader<UploadSelfieRequest> requestStream,
        ServerCallContext context)
    {
        SelfieMetadata? metadata = null;
        using var dataStream = new MemoryStream();

        await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            switch (request.PayloadCase)
            {
                case UploadSelfieRequest.PayloadOneofCase.Metadata:
                    metadata = request.Metadata;
                    break;
                case UploadSelfieRequest.PayloadOneofCase.Chunk:
                    await dataStream.WriteAsync(
                        request.Chunk.Memory, context.CancellationToken);
                    break;
            }
        }

        if (metadata is null)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Selfie metadata is required."));

        var tenantId = _tenantProvider.GetTenantId();
        var command = new UploadSelfieCommand(
            AccountId: Guid.Parse(metadata.AccountId),
            ContentType: metadata.ContentType,
            FileSize: metadata.FileSize,
            FileData: dataStream.ToArray(),
            LivenessToken: metadata.LivenessToken,
            TenantId: tenantId);

        var validation = await _selfieValidator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            var errorMessage = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            throw new RpcException(new Status(StatusCode.InvalidArgument, errorMessage));
        }

        var result = await _selfieHandler.HandleAsync(command, context.CancellationToken);

        if (result.IsFailure)
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));

        return new UploadSelfieResponse
        {
            Success = true,
            SelfieDocumentId = result.Value.SelfieDocumentId,
            MatchConfidence = result.Value.MatchConfidence,
            Status = result.Value.Status,
            Message = result.Value.Status == "approved"
                ? "Selfie verified successfully."
                : "Selfie uploaded. Pending manual review."
        };
    }

    public override async Task<GetKycStatusResponse> GetKycStatus(
        GetKycStatusRequest request, ServerCallContext context)
    {
        var accountId = Guid.Parse(request.AccountId);

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, context.CancellationToken);

        if (account is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Account not found."));

        var documents = await _dbContext.Set<KycDocument>()
            .Where(d => d.AccountId == accountId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var overallStatus = DetermineOverallKycStatus(documents);

        var response = new GetKycStatusResponse
        {
            AccountId = request.AccountId,
            KycLevel = account.KycLevel,
            OverallStatus = overallStatus
        };

        foreach (var doc in documents)
        {
            response.Documents.Add(new KycDocumentSummary
            {
                DocumentId = doc.Id.ToString(),
                DocumentType = doc.DocumentType,
                Status = doc.Status,
                UploadedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc))
            });
        }

        return response;
    }

    public override async Task<GetDocumentStatusResponse> GetDocumentStatus(
        GetDocumentStatusRequest request, ServerCallContext context)
    {
        var docId = Guid.Parse(request.DocumentId);
        var document = await _dbContext.Set<KycDocument>()
            .FirstOrDefaultAsync(d => d.Id == docId, context.CancellationToken);

        if (document is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Document not found."));

        return new GetDocumentStatusResponse
        {
            DocumentId = document.Id.ToString(),
            DocumentType = document.DocumentType,
            Status = document.Status,
            Message = $"Document is {document.Status}.",
            UploadedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc)),
            VerifiedAt = document.VerifiedAt.HasValue
                ? Timestamp.FromDateTime(DateTime.SpecifyKind(document.VerifiedAt.Value, DateTimeKind.Utc))
                : null
        };
    }

    private static string DetermineOverallKycStatus(List<KycDocument> documents)
    {
        if (documents.Count == 0) return "pending";

        var hasIdDoc = documents.Any(d => d.DocumentType == "national_id" && d.Status == "approved");
        var hasSelfie = documents.Any(d => d.DocumentType == "selfie" && d.Status == "approved");

        if (hasIdDoc && hasSelfie) return "approved";

        var anyRejected = documents.Any(d => d.Status == "rejected");
        if (anyRejected) return "rejected";

        return "pending";
    }
}
