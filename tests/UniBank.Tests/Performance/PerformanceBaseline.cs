namespace UniBank.Tests.Performance;

/// <summary>
/// Documents expected NFR (Non-Functional Requirements) performance baselines
/// as constants with assertions for validation (STORY-074).
/// These baselines define the minimum acceptable performance characteristics
/// for the UniBank platform before pilot deployment.
/// </summary>
public sealed class PerformanceBaseline
{
    // --- Latency Targets (p95 in milliseconds) ---

    /// <summary>Balance inquiry p95 latency target: 500ms.</summary>
    public const int BalanceInquiryP95Ms = 500;

    /// <summary>Payment transaction p95 latency target: 2000ms.</summary>
    public const int PaymentTransactionP95Ms = 2000;

    /// <summary>Registration p95 latency target: 3000ms.</summary>
    public const int RegistrationP95Ms = 3000;

    /// <summary>P2P transfer p95 latency target: 2000ms.</summary>
    public const int TransferP95Ms = 2000;

    /// <summary>KYC document upload p95 latency target: 5000ms.</summary>
    public const int KycUploadP95Ms = 5000;

    // --- Concurrency Targets ---

    /// <summary>Balance inquiry concurrent users target.</summary>
    public const int BalanceInquiryConcurrentUsers = 1000;

    /// <summary>Payment transaction concurrent users target.</summary>
    public const int PaymentTransactionConcurrentUsers = 500;

    /// <summary>Registration concurrent users target.</summary>
    public const int RegistrationConcurrentUsers = 200;

    /// <summary>Transfer concurrent users target.</summary>
    public const int TransferConcurrentUsers = 300;

    // --- Throughput Targets (transactions per second) ---

    /// <summary>Minimum transactions per second for balance inquiries.</summary>
    public const int BalanceInquiryTps = 500;

    /// <summary>Minimum transactions per second for payments.</summary>
    public const int PaymentTransactionTps = 100;

    /// <summary>Minimum transactions per second for registrations.</summary>
    public const int RegistrationTps = 50;

    /// <summary>Minimum transactions per second for transfers.</summary>
    public const int TransferTps = 100;

    // --- Availability Targets ---

    /// <summary>Target uptime percentage (99.9%).</summary>
    public const double UptimePercentage = 99.9;

    /// <summary>Maximum allowed error rate during load testing (1%).</summary>
    public const double MaxErrorRatePercentage = 1.0;

    // --- Resource Utilization Targets ---

    /// <summary>Maximum CPU utilization during peak load (80%).</summary>
    public const double MaxCpuUtilizationPercentage = 80.0;

    /// <summary>Maximum memory utilization during peak load (75%).</summary>
    public const double MaxMemoryUtilizationPercentage = 75.0;

    /// <summary>Maximum database connection pool usage (70%).</summary>
    public const double MaxDbConnectionPoolPercentage = 70.0;

    // --- Validation Tests ---

    [Fact]
    public void BalanceInquiry_LatencyTarget_IsReasonable()
    {
        Assert.True(BalanceInquiryP95Ms > 0, "Balance inquiry latency target must be positive.");
        Assert.True(BalanceInquiryP95Ms <= 1000, "Balance inquiry p95 should be under 1 second.");
    }

    [Fact]
    public void PaymentTransaction_LatencyTarget_IsReasonable()
    {
        Assert.True(PaymentTransactionP95Ms > 0, "Payment transaction latency target must be positive.");
        Assert.True(PaymentTransactionP95Ms <= 5000, "Payment transaction p95 should be under 5 seconds.");
    }

    [Fact]
    public void Transfer_LatencyTarget_IsReasonable()
    {
        Assert.True(TransferP95Ms > 0, "Transfer latency target must be positive.");
        Assert.True(TransferP95Ms <= 5000, "Transfer p95 should be under 5 seconds.");
    }

    [Fact]
    public void ConcurrencyTargets_AreWithinExpectedRange()
    {
        Assert.True(BalanceInquiryConcurrentUsers >= 1000,
            $"Balance inquiry concurrency should be at least 1000, was {BalanceInquiryConcurrentUsers}.");
        Assert.True(PaymentTransactionConcurrentUsers >= 500,
            $"Payment transaction concurrency should be at least 500, was {PaymentTransactionConcurrentUsers}.");
        Assert.True(RegistrationConcurrentUsers >= 200,
            $"Registration concurrency should be at least 200, was {RegistrationConcurrentUsers}.");
        Assert.True(TransferConcurrentUsers >= 300,
            $"Transfer concurrency should be at least 300, was {TransferConcurrentUsers}.");
    }

    [Fact]
    public void ErrorRate_Target_IsStrictEnough()
    {
        Assert.True(MaxErrorRatePercentage <= 1.0,
            $"Max error rate should be 1% or less, was {MaxErrorRatePercentage}%.");
    }

    [Fact]
    public void Uptime_Target_MeetsCommercialSla()
    {
        Assert.True(UptimePercentage >= 99.9,
            $"Uptime target should be at least 99.9%, was {UptimePercentage}%.");
    }

    [Fact]
    public void ResourceUtilization_Targets_AllowHeadroom()
    {
        Assert.True(MaxCpuUtilizationPercentage <= 85.0,
            "CPU utilization target should leave at least 15% headroom.");
        Assert.True(MaxMemoryUtilizationPercentage <= 80.0,
            "Memory utilization target should leave at least 20% headroom.");
        Assert.True(MaxDbConnectionPoolPercentage <= 75.0,
            "DB connection pool target should leave at least 25% headroom.");
    }
}
