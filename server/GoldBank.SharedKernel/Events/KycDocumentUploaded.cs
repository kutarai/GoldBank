namespace GoldBank.SharedKernel.Events;

using GoldBank.SharedKernel.Domain;

public sealed record KycDocumentUploaded(
    Guid AccountId,
    Guid DocumentId,
    string DocumentType) : DomainEvent;
