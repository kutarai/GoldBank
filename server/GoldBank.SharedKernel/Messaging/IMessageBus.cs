namespace GoldBank.SharedKernel.Messaging;

/// <summary>
/// Lightweight in-process message bus abstraction.
/// This will be replaced by WolverineFx when .NET 10 support is available.
/// PublishAsync dispatches to all registered handlers (fan-out).
/// SendAsync dispatches to exactly one handler (point-to-point).
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes a message to all registered handlers for the message type (fan-out / event).
    /// </summary>
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sends a message to a single handler for the message type (point-to-point / command).
    /// </summary>
    Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}
