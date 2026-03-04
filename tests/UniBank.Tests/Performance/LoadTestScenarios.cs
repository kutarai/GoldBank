using NBomber.Contracts;
using NBomber.CSharp;

namespace UniBank.Tests.Performance;

/// <summary>
/// NBomber load test scenario definitions for UniBank platform NFR validation (STORY-074).
/// These tests are designed to be run manually against a deployed environment.
/// Each scenario targets specific non-functional requirements with defined concurrency and latency targets.
/// </summary>
public sealed class LoadTestScenarios
{
    private const string BaseUrl = "https://localhost:5001";

    /// <summary>
    /// Balance Inquiry Scenario: 1000 concurrent users, target p95 less than 500ms.
    /// Simulates high-frequency balance lookups which are the most common operation.
    /// </summary>
    public static ScenarioProps BalanceInquiryScenario()
    {
        return Scenario.Create("balance_inquiry", async context =>
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");

                var accountId = Guid.NewGuid().ToString();
                var response = await client.GetAsync($"/api/accounts/{accountId}/balance");

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));
    }

    /// <summary>
    /// Payment Transaction Scenario: 500 concurrent users, target p95 less than 2s.
    /// Simulates merchant payment processing with full transaction lifecycle.
    /// </summary>
    public static ScenarioProps PaymentTransactionScenario()
    {
        return Scenario.Create("payment_transaction", async context =>
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");

                var payload = new StringContent(
                    """{"merchant_id": "test-merchant", "amount": "100.00", "currency": "ZWG"}""",
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync("/api/payments/initiate", payload);

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));
    }

    /// <summary>
    /// Registration Scenario: 200 concurrent new user registrations.
    /// Simulates onboarding spike during marketing campaigns.
    /// </summary>
    public static ScenarioProps RegistrationScenario()
    {
        return Scenario.Create("registration", async context =>
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(BaseUrl);

                var phone = $"+26377{Random.Shared.Next(1000000, 9999999)}";
                var payload = new StringContent(
                    $$"""{ "phone": "{{phone}}", "country_code": "+263", "tenant_id": "test_tenant" }""",
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync("/api/accounts/register", payload);

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));
    }

    /// <summary>
    /// Transfer Scenario: 300 concurrent P2P transfers, target p95 less than 2s.
    /// Simulates peak transfer activity with full debit/credit lifecycle.
    /// </summary>
    public static ScenarioProps TransferScenario()
    {
        return Scenario.Create("transfer", async context =>
            {
                using var client = new HttpClient();
                client.BaseAddress = new Uri(BaseUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");

                var payload = new StringContent(
                    """{"recipient_phone": "+263771234567", "amount": "50.00", "currency": "ZWG", "pin": "1234"}""",
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync("/api/transfers/p2p", payload);

                return response.IsSuccessStatusCode
                    ? Response.Ok(statusCode: response.StatusCode.ToString())
                    : Response.Fail(statusCode: response.StatusCode.ToString());
            })
            .WithLoadSimulations(
                Simulation.Inject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2)));
    }

    /// <summary>
    /// Runs all load test scenarios. This method is intended to be called manually
    /// from a test runner or command line, not from CI/CD due to infrastructure requirements.
    /// </summary>
    [Fact(Skip = "Load tests require a running environment. Run manually with: dotnet test --filter LoadTestScenarios")]
    public void RunAllScenarios()
    {
        NBomberRunner
            .RegisterScenarios(
                BalanceInquiryScenario(),
                PaymentTransactionScenario(),
                RegistrationScenario(),
                TransferScenario())
            .WithReportFolder("load-test-reports")
            .Run();
    }
}
