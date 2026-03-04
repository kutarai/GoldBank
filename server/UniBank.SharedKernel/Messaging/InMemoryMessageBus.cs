using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UniBank.SharedKernel.Messaging;

/// <summary>
/// In-process message bus implementation using System.Threading.Channels for backpressure support.
/// Messages are dispatched to handlers resolved from the DI container.
/// This implementation is a lightweight stand-in for WolverineFx; it processes messages
/// asynchronously via a bounded channel to decouple producers from consumers.
/// </summary>
public sealed class InMemoryMessageBus : IMessageBus, IAsyncDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryMessageBus> _logger;
    private readonly Channel<Envelope> _channel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts = new();

    public InMemoryMessageBus(IServiceProvider serviceProvider, ILogger<InMemoryMessageBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Bounded channel with 10,000 capacity provides backpressure when the system is overloaded
        _channel = Channel.CreateBounded<Envelope>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _processingTask = Task.Run(ProcessMessagesAsync);
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = new Envelope(message, typeof(T), DispatchMode.Publish);
        await _channel.Writer.WriteAsync(envelope, cancellationToken);

        _logger.LogDebug("Published message of type {MessageType}", typeof(T).Name);
    }

    public async Task SendAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var envelope = new Envelope(message, typeof(T), DispatchMode.Send);
        await _channel.Writer.WriteAsync(envelope, cancellationToken);

        _logger.LogDebug("Sent message of type {MessageType}", typeof(T).Name);
    }

    private async Task ProcessMessagesAsync()
    {
        await foreach (var envelope in _channel.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                await DispatchAsync(envelope);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message of type {MessageType}", envelope.MessageType.Name);
            }
        }
    }

    private async Task DispatchAsync(Envelope envelope)
    {
        using var scope = _serviceProvider.CreateScope();

        var handlerType = typeof(IMessageHandler<>).MakeGenericType(envelope.MessageType);
        var handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

        if (handlers.Count == 0)
        {
            _logger.LogWarning("No handlers registered for message type {MessageType}", envelope.MessageType.Name);
            return;
        }

        if (envelope.Mode == DispatchMode.Send)
        {
            // Point-to-point: only the first registered handler processes the message
            var handler = handlers[0];
            var method = handlerType.GetMethod("HandleAsync");
            await (Task)method!.Invoke(handler, [envelope.Message, CancellationToken.None])!;
        }
        else
        {
            // Fan-out: all handlers process the message
            var method = handlerType.GetMethod("HandleAsync");
            var tasks = handlers.Select(handler =>
                (Task)method!.Invoke(handler, [envelope.Message, CancellationToken.None])!);
            await Task.WhenAll(tasks);
        }

        _logger.LogDebug(
            "Dispatched {MessageType} to {HandlerCount} handler(s) in {Mode} mode",
            envelope.MessageType.Name,
            envelope.Mode == DispatchMode.Send ? 1 : handlers.Count,
            envelope.Mode);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync();

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        _cts.Dispose();
    }

    private sealed record Envelope(object Message, Type MessageType, DispatchMode Mode);

    private enum DispatchMode
    {
        Publish,
        Send
    }
}
