# STORY-074: Performance Testing & NFR Validation

**Epic:** Cross-cutting
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 8

---

## User Story

As a team
I want performance tests validating all NFRs
So that we confirm the system meets requirements before launch

---

## Description

### Background

UniBank is preparing for pilot deployment with the first white-label institution in Southern Africa. Before launch, the team must have empirical evidence that the system meets all non-functional requirements (NFRs) defined during architecture. These NFRs are not aspirational targets but hard requirements: the pilot institution's contract likely specifies performance SLAs, and Southern African regulators expect financial systems to demonstrate reliability under load.

The on-premise deployment model adds additional constraints. Unlike cloud deployments where auto-scaling can mask performance issues, UniBank runs on fixed hardware. The performance tests must validate that the defined hardware specification supports the required workloads. If tests reveal bottlenecks, the team has limited time (within Sprint 8) to optimize before launch.

Key NFRs to validate:
- Payment transactions (NFC, QR, P2P): p95 latency < 2 seconds
- Non-payment APIs (balance inquiry, transaction history, registration, etc.): p95 latency < 500ms
- Concurrent user capacity: 1000 simultaneous users
- Zero functional failures under sustained load
- System stability over extended duration (no memory leaks, connection pool exhaustion)

### Scope

**In scope:**
- Load test suite using NBomber (.NET) for all critical user flows
- Eight test scenarios covering all transaction types and mixed workloads
- Performance metrics collection: latency percentiles (p50, p95, p99), throughput (RPS), error rates
- Resource utilization monitoring during tests: CPU, memory, DB connections, Redis connections
- Soak test (extended duration) to detect stability issues
- NFR validation matrix mapping each requirement to test results with pass/fail
- Performance results documentation in `docs/performance-report.md`
- Bottleneck identification and optimization recommendations
- Baseline establishment for ongoing performance monitoring

**Out of scope:**
- Chaos engineering / failure injection testing (future enhancement)
- Multi-region latency testing (on-premise single-site deployment)
- Client device performance testing (mobile app rendering performance)
- Network infrastructure testing (switches, routers, firewalls)
- Third-party integration performance (HSM, external APIs) — mock these in load tests
- Automated performance regression testing in CI/CD pipeline (future enhancement)

### Test Plan

**Environment Setup:**
1. Staging environment configured to mirror production hardware specification
2. Database seeded with realistic data volume:
   - 50,000 user accounts across 3 tenant schemas
   - 500,000 historical transactions
   - 200 merchant accounts
   - 100 agent accounts
3. External dependencies (HSM, KYC provider) replaced with performance-appropriate mocks that simulate realistic latency (50-100ms)
4. Monitoring stack active: Prometheus metrics collection, Grafana dashboards
5. PostgreSQL 18 configured with production tuning parameters
6. Redis configured with production persistence settings

**Test Scenarios:**

**Scenario 1: Registration Flow**
- Virtual users: 100 concurrent
- Flow: POST registration request with phone, name, ID number -> verify OTP -> set PIN -> account activated
- Expected: p95 < 500ms per step, 0% error rate
- Duration: 5 minutes sustained

**Scenario 2: Authentication**
- Virtual users: 1000 concurrent
- Flow: Login with phone + PIN -> receive JWT -> validate token
- Expected: p95 < 500ms, 0% error rate
- Duration: 5 minutes sustained
- Ramp-up: 0 to 1000 users over 60 seconds

**Scenario 3: NFC Payment**
- Virtual users: 500 concurrent
- Flow: Authenticate -> initiate NFC payment (gRPC) -> process -> confirm
- Expected: p95 < 2 seconds end-to-end, error rate < 0.1%
- Duration: 10 minutes sustained
- Includes: account balance check, transaction creation, ledger update, event publication

**Scenario 4: QR Code Payment**
- Virtual users: 500 concurrent
- Flow: Authenticate -> generate/scan QR -> initiate payment -> process -> confirm
- Expected: p95 < 2 seconds end-to-end, error rate < 0.1%
- Duration: 10 minutes sustained

**Scenario 5: Balance Inquiry**
- Virtual users: 1000 concurrent
- Flow: Authenticate -> request balance (gRPC unary call)
- Expected: p95 < 500ms, 0% error rate
- Duration: 5 minutes sustained

**Scenario 6: Transaction History (Streaming)**
- Virtual users: 500 concurrent
- Flow: Authenticate -> request transaction history (gRPC server streaming, 50 records)
- Expected: p95 < 500ms for first page, 0% error rate
- Duration: 5 minutes sustained
- Measures: time-to-first-record and time-to-complete-stream

**Scenario 7: P2P Transfer**
- Virtual users: 300 concurrent
- Flow: Authenticate -> lookup recipient -> initiate transfer -> process -> confirm both parties
- Expected: p95 < 2 seconds end-to-end, error rate < 0.1%
- Duration: 10 minutes sustained
- Includes: sender debit, recipient credit, double-entry ledger, event publication

**Scenario 8: Mixed Workload (Realistic)**
- Virtual users: 1000 concurrent total
- Distribution matching expected real-world usage:
  - 30% balance inquiries (300 users)
  - 20% transaction history (200 users)
  - 15% NFC payments (150 users)
  - 10% QR payments (100 users)
  - 10% P2P transfers (100 users)
  - 5% registrations (50 users)
  - 5% authentications (50 users)
  - 5% admin portal operations (50 users)
- Expected: all p95 targets met simultaneously, error rate < 0.1%
- Duration: 30 minutes sustained
- Ramp-up: 0 to 1000 users over 120 seconds

**Scenario 9: Soak Test (Stability)**
- Virtual users: 500 concurrent (mixed workload at 50% capacity)
- Duration: 4 hours
- Expected: no degradation over time, no memory leaks, no connection pool exhaustion
- Metrics: track latency trend over time, memory usage trend, active DB connections trend
- Pass criteria: p95 latency at hour 4 within 10% of p95 at hour 1

**Results Documentation Format:**
```
| Scenario | VUsers | p50 (ms) | p95 (ms) | p99 (ms) | RPS | Error % | Target | Pass/Fail |
|----------|--------|----------|----------|----------|-----|---------|--------|-----------|
```

---

## Acceptance Criteria

- [ ] Load test suite implemented using NBomber with all 9 scenarios defined above
- [ ] Load test achieves 1000 concurrent virtual users in mixed workload scenario without system failure
- [ ] Payment transactions (NFC, QR, P2P) achieve p95 latency < 2 seconds under load
- [ ] Non-payment APIs (balance, transaction history, registration, authentication) achieve p95 latency < 500ms under load
- [ ] Zero functional failures (HTTP 5xx or gRPC error codes) under sustained load across all scenarios
- [ ] Error rate < 0.1% across all payment scenarios under load
- [ ] Soak test (4 hours) shows no latency degradation, memory leaks, or connection pool exhaustion
- [ ] Results documented in `docs/performance-report.md` with NFR validation matrix showing pass/fail for each requirement
- [ ] Resource utilization (CPU, memory, DB connections) documented at peak load
- [ ] Any bottlenecks identified are documented with remediation recommendations or fixes applied

---

## Technical Notes

### Components

- **Load Test Project:** `tests/PerformanceTests/UniBank.LoadTests/`
  - `Program.cs` — NBomber test runner, scenario registration
  - `Scenarios/RegistrationScenario.cs` — registration flow simulation
  - `Scenarios/AuthenticationScenario.cs` — login flow simulation
  - `Scenarios/NfcPaymentScenario.cs` — NFC payment simulation
  - `Scenarios/QrPaymentScenario.cs` — QR payment simulation
  - `Scenarios/BalanceInquiryScenario.cs` — balance check simulation
  - `Scenarios/TransactionHistoryScenario.cs` — streaming history simulation
  - `Scenarios/P2PTransferScenario.cs` — peer-to-peer transfer simulation
  - `Scenarios/MixedWorkloadScenario.cs` — realistic combined workload
  - `Scenarios/SoakTestScenario.cs` — extended duration stability test
  - `Infrastructure/GrpcClientFactory.cs` — pooled gRPC channel management
  - `Infrastructure/TestDataSeeder.cs` — database seeding for load tests
  - `Infrastructure/MetricsCollector.cs` — custom metrics collection
  - `Config/scenarios.json` — externalized scenario configuration (durations, user counts, ramp-up)

### NBomber Configuration

```csharp
// Example: NFC Payment Scenario
var nfcPaymentScenario = Scenario.Create("nfc_payment", async context =>
{
    // Step 1: Authenticate
    var authResponse = await grpcClient.AuthenticateAsync(new AuthRequest
    {
        Phone = testData.GetRandomPhone(),
        Pin = testData.DefaultPin
    });

    // Step 2: Initiate NFC Payment
    var paymentResponse = await grpcClient.InitiateNfcPaymentAsync(new NfcPaymentRequest
    {
        AccountId = authResponse.AccountId,
        MerchantId = testData.GetRandomMerchant(),
        Amount = testData.GetRandomPaymentAmount(),
        CardToken = testData.GetRandomCardToken()
    }, headers: new Metadata { { "Authorization", $"Bearer {authResponse.Token}" } });

    return paymentResponse.Success
        ? Response.Ok(statusCode: "200")
        : Response.Fail(statusCode: "500");
})
.WithLoadSimulations(
    Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(10))
);
```

### API / gRPC Endpoints

The load tests exercise the following gRPC endpoints (already implemented in prior sprints):

| Endpoint | Scenario | NFR Target |
|---|---|---|
| `AuthService.Authenticate` | Auth, all scenarios | p95 < 500ms |
| `AuthService.Register` | Registration | p95 < 500ms |
| `PaymentService.InitiateNfcPayment` | NFC Payment | p95 < 2s |
| `PaymentService.InitiateQrPayment` | QR Payment | p95 < 2s |
| `PaymentService.TransferP2P` | P2P Transfer | p95 < 2s |
| `AccountService.GetBalance` | Balance Inquiry | p95 < 500ms |
| `AccountService.GetTransactionHistory` | Tx History (streaming) | p95 < 500ms |

### Database Changes

No schema changes. However, test data seeding requires:

```sql
-- Performance test data seeding (run against staging)
-- 50,000 accounts distributed across 3 tenant schemas
-- Each account has: wallet balance, KYC completed, active status
-- 500,000 historical transactions for realistic query patterns
-- 200 merchant accounts with various categories
-- Transaction distribution matches expected production patterns
```

Seed script: `tests/PerformanceTests/UniBank.LoadTests/seed-test-data.sql`

### Monitoring During Tests

Metrics to collect via Prometheus/Grafana during each test run:

| Metric | Source | Alert Threshold |
|---|---|---|
| Request latency (p50, p95, p99) | NBomber + application metrics | Scenario-specific |
| Requests per second (RPS) | NBomber | N/A (informational) |
| Error rate (%) | NBomber | > 0.1% |
| CPU utilization (%) | Node exporter | > 80% sustained |
| Memory utilization (%) | Node exporter | > 85% sustained |
| PostgreSQL active connections | pg_stat_activity | > 80% of max_connections |
| PostgreSQL query duration (p95) | pg_stat_statements | > 100ms |
| Redis memory usage | Redis INFO | > 75% of maxmemory |
| Redis connected clients | Redis INFO | > 80% of maxclients |
| gRPC active streams | Application metrics | > 2000 concurrent |
| Wolverine message queue depth | Wolverine metrics | > 1000 pending |
| .NET GC pause time | dotnet-counters | > 200ms |
| .NET thread pool queue length | dotnet-counters | > 100 |

### Security Considerations

- **Test Data Isolation:** Load tests must run against staging environment only. Test data must not contain real customer PII. Use synthetic data generators.
- **Credential Management:** Test user credentials must be generated and stored securely, not hardcoded in test source code. Use environment variables or a test configuration file excluded from version control.
- **Staging Access Control:** Ensure staging environment is network-isolated from production. Load test traffic should not accidentally target production endpoints.
- **Results Data:** Performance reports may reveal system capacity information. Mark as internal/confidential and restrict access.

### Edge Cases

- **Cold Start vs. Warm Cache:** Run each scenario twice: once cold (fresh restart) and once warm (after cache population). Document both results; NFR targets apply to warm performance.
- **Database Connection Pool Exhaustion:** Monitor `pg_stat_activity` during peak load. If connections approach `max_connections`, document the finding and recommend connection pool tuning.
- **gRPC Channel Saturation:** NBomber's gRPC client factory must use connection pooling. A single gRPC channel may bottleneck; use multiple channels per scenario.
- **Clock Synchronization:** Load test client and server must have synchronized clocks (NTP) to ensure latency measurements are accurate.
- **Garbage Collection Pauses:** .NET GC pauses under high allocation rates can cause latency spikes. Monitor GC metrics and document if GC pauses contribute to p99 latency.
- **Network Saturation:** If load test client and server are on the same network, measure bandwidth utilization to ensure the network is not the bottleneck rather than the application.
- **Test Interference:** Ensure no other processes (backups, log rotation, monitoring scrapes) run during performance tests that could skew results.
- **Flaky Results:** Run each scenario at least 3 times and use the median result to account for variability. Document the variance.

---

## Dependencies

**Prerequisite Stories:**
- All functional stories from Sprints 1-7 must be complete and deployed to staging
- STORY-071: Per-Tenant Admin Portal Access — admin portal operations included in mixed workload
- STORY-072: Fraud Detection Alerts — fraud detection handler active during payment tests (validates it doesn't degrade performance)

**Blocked Stories:**
- STORY-076: Pilot Deployment Preparation — performance validation is a go/no-go criterion for pilot launch

**External Dependencies:**
- Staging environment provisioned and configured to mirror production hardware
- Monitoring stack (Prometheus + Grafana) deployed and collecting metrics
- Network access from load test runner to staging environment gRPC endpoints
- Sufficient test data seeded in staging database

---

## Definition of Done

- [ ] Code implemented and committed
  - [ ] NBomber test project with all 9 scenarios
  - [ ] Test data seeding scripts
  - [ ] Scenario configuration externalized for easy parameter adjustment
- [ ] Unit tests written and passing (>=80% coverage)
  - [ ] Test data generators produce valid synthetic data
  - [ ] Scenario configurations load correctly
- [ ] Integration tests passing
  - [ ] Each scenario can execute a single iteration successfully against staging
  - [ ] Metrics collection pipeline captures all required metrics
- [ ] Code reviewed and approved
- [ ] Documentation updated
  - [ ] `docs/performance-report.md` contains full results for all 9 scenarios
  - [ ] NFR validation matrix with pass/fail for each requirement
  - [ ] Resource utilization summary at peak load
  - [ ] Bottleneck analysis and recommendations (if any)
  - [ ] Test environment specification documented
- [ ] Acceptance criteria validated
  - [ ] All p95 latency targets met
  - [ ] 1000 concurrent user target achieved
  - [ ] Zero failures under load confirmed
  - [ ] Soak test stability confirmed
- [ ] Deployed to staging
- [ ] Performance report reviewed and approved by tech lead

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
