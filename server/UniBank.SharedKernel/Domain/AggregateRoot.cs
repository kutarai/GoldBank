namespace UniBank.SharedKernel.Domain;

public abstract class AggregateRoot : BaseEntity
{
    public int Version { get; protected set; }
}
