using Microsoft.Extensions.Logging;

namespace GoldBank.HSM.Services;

/// <summary>
/// Thread-safe circuit breaker for HSM operations.
/// States: Closed (normal), Open (rejecting), HalfOpen (probe).
/// Opens after <see cref="FailureThreshold"/> consecutive failures.
/// Transitions to HalfOpen after <see cref="OpenDuration"/> has elapsed,
/// allowing a single probe request through.
/// </summary>
public sealed class HsmCircuitBreaker
{
    /// <summary>Number of consecutive failures before the circuit opens.</summary>
    private const int FailureThreshold = 3;

    /// <summary>Duration the circuit stays open before transitioning to half-open.</summary>
    private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(30);

    private readonly ILogger<HsmCircuitBreaker> _logger;

    // State is stored as an int to allow Interlocked operations.
    // 0 = Closed, 1 = Open, 2 = HalfOpen
    private int _state;
    private int _consecutiveFailures;
    private long _openedAtTicks;

    public HsmCircuitBreaker(ILogger<HsmCircuitBreaker> logger)
    {
        _logger = logger;
    }

    public CircuitState State => (CircuitState)Volatile.Read(ref _state);

    /// <summary>
    /// Executes the given operation through the circuit breaker.
    /// If the circuit is open, throws <see cref="CircuitBreakerOpenException"/>.
    /// If the circuit is half-open, allows one probe request.
    /// On success, resets the failure counter and closes the circuit.
    /// On failure, increments the counter and may open the circuit.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        var currentState = State;

        if (currentState == CircuitState.Open)
        {
            // Check if enough time has passed to transition to half-open
            var openedAt = new DateTime(Volatile.Read(ref _openedAtTicks), DateTimeKind.Utc);
            if (DateTime.UtcNow - openedAt >= OpenDuration)
            {
                // Attempt to transition to HalfOpen; only one thread should succeed
                if (Interlocked.CompareExchange(ref _state, (int)CircuitState.HalfOpen, (int)CircuitState.Open)
                    == (int)CircuitState.Open)
                {
                    _logger.LogWarning(
                        "Circuit breaker transitioning to HalfOpen for probe request. Operation={Operation}",
                        operationName);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Circuit breaker is Open. Rejecting operation={Operation}",
                    operationName);
                throw new CircuitBreakerOpenException(
                    $"Circuit breaker is open. Operation '{operationName}' rejected. Retry after {OpenDuration.TotalSeconds}s.");
            }
        }

        try
        {
            var result = await operation().ConfigureAwait(false);
            OnSuccess(operationName);
            return result;
        }
        catch (CircuitBreakerOpenException)
        {
            throw; // Re-throw circuit breaker exceptions without recording failure
        }
        catch (Exception ex)
        {
            OnFailure(operationName, ex);
            throw;
        }
    }

    /// <summary>
    /// Records a successful operation: resets failure count and closes the circuit.
    /// </summary>
    private void OnSuccess(string operationName)
    {
        var previousState = State;

        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _state, (int)CircuitState.Closed);

        if (previousState != CircuitState.Closed)
        {
            _logger.LogInformation(
                "Circuit breaker closed after successful probe. Operation={Operation}, PreviousState={PreviousState}",
                operationName, previousState);
        }
    }

    /// <summary>
    /// Records a failed operation: increments the failure counter.
    /// If the threshold is reached, opens the circuit.
    /// </summary>
    private void OnFailure(string operationName, Exception exception)
    {
        var failures = Interlocked.Increment(ref _consecutiveFailures);

        _logger.LogError(
            exception,
            "HSM operation failed. Operation={Operation}, ConsecutiveFailures={Failures}, Threshold={Threshold}",
            operationName, failures, FailureThreshold);

        if (failures >= FailureThreshold)
        {
            Interlocked.Exchange(ref _state, (int)CircuitState.Open);
            Interlocked.Exchange(ref _openedAtTicks, DateTime.UtcNow.Ticks);

            _logger.LogError(
                "Circuit breaker OPENED after {Failures} consecutive failures. Operation={Operation}. "
                + "Will retry in {RetrySeconds}s.",
                failures, operationName, OpenDuration.TotalSeconds);
        }

        // If we were HalfOpen and the probe failed, go back to Open
        if (State == CircuitState.HalfOpen)
        {
            Interlocked.Exchange(ref _state, (int)CircuitState.Open);
            Interlocked.Exchange(ref _openedAtTicks, DateTime.UtcNow.Ticks);

            _logger.LogWarning(
                "HalfOpen probe failed. Circuit breaker re-opened. Operation={Operation}",
                operationName);
        }
    }
}

public enum CircuitState
{
    Closed = 0,
    Open = 1,
    HalfOpen = 2
}

public sealed class CircuitBreakerOpenException : Exception
{
    public CircuitBreakerOpenException(string message) : base(message)
    {
    }

    public CircuitBreakerOpenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CircuitBreakerOpenException()
    {
    }
}
