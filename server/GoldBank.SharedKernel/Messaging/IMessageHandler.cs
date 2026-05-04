namespace GoldBank.SharedKernel.Messaging;

/// <summary>
/// Defines a handler for a specific message type.
/// Implementations are auto-discovered and registered by <see cref="MessageBusExtensions"/>.
/// This interface aligns with the Wolverine handler convention for future migration.
/// </summary>
/// <typeparam name="T">The message type to handle.</typeparam>
public interface IMessageHandler<in T> where T : class
{
    Task HandleAsync(T message, CancellationToken cancellationToken = default);
}
