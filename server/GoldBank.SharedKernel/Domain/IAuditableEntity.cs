namespace GoldBank.SharedKernel.Domain;

public interface IAuditableEntity
{
    string CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    string? ModifiedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
}
