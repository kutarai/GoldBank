namespace UniBank.SharedKernel.Events;

using UniBank.SharedKernel.Domain;

public sealed record KycDocumentUploaded(
    Guid AccountId,
    Guid DocumentId,
    string DocumentType) : DomainEvent;
