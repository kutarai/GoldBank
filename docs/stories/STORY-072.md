# STORY-072: Fraud Detection Alerts

**Epic:** EPIC-014 Security & Authentication
**Priority:** Should Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 8

---

## User Story

As a compliance team
I want alerts on suspicious transaction patterns
So that potential fraud is detected early

---

## Description

### Background

GoldBank serves the unbanked population in Southern Africa through mobile wallets, NFC payments, QR payments, and P2P transfers. This demographic is particularly vulnerable to fraud: agents may exploit customers, stolen phones can lead to unauthorized transactions, and new accounts may be used for money laundering. Early fraud detection is a regulatory requirement and a business necessity for deploying institutions.

Southern African regulators (e.g., SARB in South Africa, Bank of Zambia) require financial institutions to implement transaction monitoring for anti-money laundering (AML) and counter-terrorism financing (CTF) compliance. This story implements a rules-based fraud detection engine that evaluates transactions in real-time, raises alerts for the compliance team, and can auto-suspend suspicious accounts.

Since this is the launch sprint, the fraud detection system must be operational before the pilot deployment. Even a rules-based approach (as opposed to ML-based) provides significant protection and satisfies regulatory requirements for initial compliance. The rules must be configurable per tenant, as different deploying institutions may have different risk profiles and regulatory requirements.

### Scope

**In scope:**
- Rules engine evaluating transactions in real-time via Wolverine event handlers
- Five default fraud detection rules (velocity, amount anomaly, geographic anomaly, new account risk, failed attempts)
- Per-tenant configurable rule thresholds and actions
- Fraud alert creation, storage, and lifecycle management
- Auto-suspension of accounts when rules dictate
- Admin portal page for viewing, filtering, and managing fraud alerts
- Push notification to compliance admins on high-severity alerts
- Account investigation view linked from fraud alerts

**Out of scope:**
- Machine learning-based fraud detection (future enhancement)
- Real-time transaction blocking before completion (rules evaluate post-completion for v1)
- External fraud database integration (e.g., shared fraud lists across institutions)
- Customer-facing fraud notifications (handled by existing notification system once account is suspended)
- Chargeback or dispute management workflow
- SAR (Suspicious Activity Report) generation for regulators (future enhancement)

### User Flow

**Transaction Evaluation (Automated):**
1. A transaction completes (any type: NFC payment, QR payment, P2P transfer, cash-in, cash-out)
2. `TransactionCompleted` event published via Wolverine
3. `FraudDetectionHandler` receives the event
4. Handler loads active fraud rules for the transaction's tenant from cache (Redis) or database
5. Each enabled rule evaluates the transaction against its criteria:
   - Velocity rule: query recent transaction count for account within time window
   - Amount anomaly: compare transaction amount to account's rolling average
   - Geographic anomaly: compare transaction location to recent transaction locations
   - New account rule: check account age vs. transaction value
   - Failed attempts: query recent failed payment count for account
6. If any rule triggers:
   a. Create `fraud_alerts` record with severity, rule details, transaction reference
   b. Execute configured action (alert only, suspend account, block account)
   c. Publish `FraudAlertRaised` event via Wolverine
   d. If severity is high or critical, push notification to compliance admin(s)
7. Processing completes; transaction is not reversed (only future transactions may be blocked)

**Compliance Team Alert Review:**
1. Compliance admin logs into admin portal
2. Navigates to Security > Fraud Alerts
3. Sees queue of alerts sorted by severity (critical first) and recency
4. Filters by: severity, rule type, status (new/investigating/resolved/false_positive), date range
5. Clicks on an alert to see details: transaction info, account info, rule that triggered, historical context
6. Clicks "Investigate Account" to view full account activity, recent transactions, KYC status
7. Takes action: mark as false positive, confirm fraud (escalate), keep investigating
8. If confirmed fraud: can manually suspend/block account, initiate recovery process
9. If false positive: marks as such; system tracks false positive rate per rule for tuning

**Rule Configuration (Tenant Admin / Compliance):**
1. Tenant admin navigates to Security > Fraud Rules
2. Sees list of rules with current thresholds, enabled/disabled status
3. Adjusts thresholds (e.g., velocity rule from 10 to 15 transactions per hour)
4. Changes action (e.g., from "alert" to "suspend" for amount anomaly)
5. Enables/disables rules per tenant business requirements
6. Saves configuration; cache invalidated; new thresholds effective immediately

---

## Acceptance Criteria

- [ ] Rules-based detection engine evaluates every completed transaction against active fraud rules for the transaction's tenant
- [ ] Five default rules are implemented and enabled for all tenants:
  - [ ] Velocity: >10 transactions in 1 hour from same account triggers alert
  - [ ] Amount anomaly: single transaction >5x the account's 30-day average triggers alert
  - [ ] Geographic anomaly: transactions from locations >100km apart within 30 minutes triggers alert
  - [ ] New account risk: transaction >50% of daily limit within 24 hours of account activation triggers alert
  - [ ] Failed attempts: >5 failed payment attempts in 30 minutes triggers alert
- [ ] Alerts are created in the admin portal with severity levels (critical, high, medium, low)
- [ ] Suspicious accounts are auto-suspended when a rule's configured action is "suspend"
- [ ] Alert rules are configurable per tenant (thresholds, actions, enabled/disabled)
- [ ] Admin portal displays a fraud alerts queue with severity and status filtering
- [ ] Each alert links to an account investigation view showing transaction history and account details
- [ ] Compliance admins receive push notifications for high and critical severity alerts
- [ ] False positive marking is supported and tracked per rule for future tuning
- [ ] Rule configuration changes take effect within 60 seconds (cache invalidation)
- [ ] Fraud detection processing does not add more than 200ms latency to transaction completion events

---

## Technical Notes

### Components

- **Payments Module (Core Banking):** `src/Modules/CoreBanking/Payments/`
  - `Events/TransactionCompleted.cs` — existing event, consumed by fraud handler
  - `FraudDetection/FraudDetectionHandler.cs` — Wolverine handler, evaluates rules
  - `FraudDetection/Rules/IFraudRule.cs` — rule interface
  - `FraudDetection/Rules/VelocityRule.cs` — >N transactions in time window
  - `FraudDetection/Rules/AmountAnomalyRule.cs` — transaction vs. rolling average
  - `FraudDetection/Rules/GeographicAnomalyRule.cs` — distance-based anomaly
  - `FraudDetection/Rules/NewAccountRiskRule.cs` — high-value on new account
  - `FraudDetection/Rules/FailedAttemptsRule.cs` — excessive failed attempts
  - `FraudDetection/FraudRuleEngine.cs` — orchestrates rule evaluation
  - `FraudDetection/FraudAlertService.cs` — alert creation and lifecycle
- **Admin Portal:** `src/AdminPortal/`
  - `Pages/Security/FraudAlerts.razor` — alert queue with filtering
  - `Pages/Security/FraudAlertDetail.razor` — individual alert investigation
  - `Pages/Security/FraudRuleConfiguration.razor` — rule threshold management
  - `Pages/Security/AccountInvestigation.razor` — deep-dive into account activity
- **Notification Service:** `src/Services/Notifications/`
  - `FraudAlertNotificationHandler.cs` — push notification to compliance admins
- **Infrastructure:**
  - `Caching/FraudRuleCacheService.cs` — Redis cache for tenant fraud rules
  - `Events/FraudAlertRaised.cs` — Wolverine event for downstream consumers

### API / gRPC Endpoints

Internal Wolverine message handlers (not external API):

| Handler | Message | Description |
|---|---|---|
| `FraudDetectionHandler` | `TransactionCompleted` | Evaluates transaction against fraud rules |
| `FraudAlertNotificationHandler` | `FraudAlertRaised` | Sends push notification to compliance admins |
| `AccountSuspensionHandler` | `FraudAlertRaised` (action=suspend) | Auto-suspends account |

Admin portal backing services (internal to Blazor Server):

| Service Method | Description | Auth Policy |
|---|---|---|
| `FraudAlerts.List` | Query fraud alerts with filtering | TenantScoped + (tenant_admin or tenant_operations) |
| `FraudAlerts.GetDetail` | Get alert details with transaction/account context | TenantScoped + (tenant_admin or tenant_operations) |
| `FraudAlerts.UpdateStatus` | Change alert status (investigating, resolved, false_positive) | TenantScoped + (tenant_admin or tenant_operations) |
| `FraudRules.List` | List fraud rules for tenant | TenantScoped + tenant_admin |
| `FraudRules.Update` | Update rule thresholds/actions | TenantScoped + tenant_admin |
| `AccountInvestigation.GetProfile` | Get account with full activity for investigation | TenantScoped + (tenant_admin or tenant_operations) |

### Database Changes

**Table: `fraud_rules`** (in tenant schema)

```sql
CREATE TABLE {tenant_schema}.fraud_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    rule_type VARCHAR(50) NOT NULL CHECK (rule_type IN ('velocity', 'amount_anomaly', 'geographic_anomaly', 'new_account_risk', 'failed_attempts')),
    display_name VARCHAR(255) NOT NULL,
    description TEXT,
    threshold JSONB NOT NULL,
    -- Example threshold structures:
    -- velocity: {"max_transactions": 10, "time_window_minutes": 60}
    -- amount_anomaly: {"multiplier": 5.0, "lookback_days": 30, "min_transactions": 5}
    -- geographic_anomaly: {"max_distance_km": 100, "time_window_minutes": 30}
    -- new_account_risk: {"account_age_hours": 24, "amount_pct_of_daily_limit": 50}
    -- failed_attempts: {"max_attempts": 5, "time_window_minutes": 30}
    action VARCHAR(20) NOT NULL DEFAULT 'alert' CHECK (action IN ('alert', 'suspend', 'block')),
    severity VARCHAR(20) NOT NULL DEFAULT 'medium' CHECK (severity IN ('critical', 'high', 'medium', 'low')),
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, rule_type)
);
```

**Table: `fraud_alerts`** (in tenant schema)

```sql
CREATE TABLE {tenant_schema}.fraud_alerts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    account_id UUID NOT NULL,
    rule_id UUID NOT NULL REFERENCES {tenant_schema}.fraud_rules(id),
    rule_type VARCHAR(50) NOT NULL,
    transaction_id UUID,
    severity VARCHAR(20) NOT NULL CHECK (severity IN ('critical', 'high', 'medium', 'low')),
    status VARCHAR(30) NOT NULL DEFAULT 'new' CHECK (status IN ('new', 'investigating', 'resolved_fraud', 'resolved_false_positive', 'dismissed')),
    details JSONB NOT NULL,
    -- details contains rule-specific context:
    -- velocity: {"transaction_count": 12, "time_window_minutes": 60, "threshold": 10}
    -- amount_anomaly: {"transaction_amount": 5000, "average_amount": 800, "multiplier": 6.25}
    action_taken VARCHAR(20),
    assigned_to UUID,
    resolved_at TIMESTAMPTZ,
    resolved_by UUID,
    resolution_notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_fraud_alerts_tenant_status ON {tenant_schema}.fraud_alerts(tenant_id, status, created_at DESC);
CREATE INDEX idx_fraud_alerts_account ON {tenant_schema}.fraud_alerts(account_id, created_at DESC);
CREATE INDEX idx_fraud_alerts_severity ON {tenant_schema}.fraud_alerts(severity, status, created_at DESC);
```

**Table: `fraud_alert_actions`** (in tenant schema, audit trail for alert lifecycle)

```sql
CREATE TABLE {tenant_schema}.fraud_alert_actions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_id UUID NOT NULL REFERENCES {tenant_schema}.fraud_alerts(id),
    action VARCHAR(50) NOT NULL,
    -- e.g., 'status_changed', 'assigned', 'account_suspended', 'note_added'
    previous_value VARCHAR(100),
    new_value VARCHAR(100),
    performed_by UUID NOT NULL,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_fraud_alert_actions_alert ON {tenant_schema}.fraud_alert_actions(alert_id, created_at);
```

### Security Considerations

- **PII in Alert Details:** Fraud alert details may contain transaction amounts, account IDs, and location data. Ensure PII masking in logs. Alert detail views in admin portal should be accessible only to authorized compliance roles.
- **Auto-Suspension Auditability:** Every auto-suspension must be logged with the triggering rule, threshold, and transaction details. Customers must be able to appeal suspensions, which requires clear audit trails.
- **Rate Limiting on Rule Changes:** Prevent rapid toggling of rules by implementing rate limiting on fraud rule configuration changes (max 10 changes per hour per tenant).
- **Cache Invalidation Security:** Fraud rule cache keys must be tenant-scoped in Redis to prevent one tenant's rule changes from affecting another.
- **No Information Leakage:** Fraud detection results must not be exposed to end customers or merchants. A suspended account should show a generic "account restricted" message, not "fraud detected."
- **Compliance Data Retention:** Fraud alerts and their action history must be retained for the regulatory minimum period (typically 5-7 years in Southern African jurisdictions) even if the account is subsequently closed.
- **Rule Threshold Minimums:** System should enforce minimum thresholds to prevent rules from being effectively disabled (e.g., velocity rule cannot be set to >10000 transactions per hour).

### Edge Cases

- **Insufficient History for Amount Anomaly:** New accounts with fewer than the minimum required transactions (e.g., <5) for average calculation should use a default average based on account tier/type rather than skipping the rule.
- **Burst of Legitimate Transactions:** A merchant processing many small payments quickly (e.g., transport operator) may trigger velocity rules. Merchant accounts may need different rule profiles or exemptions.
- **Time Zone Handling for Geographic Rules:** All timestamps must be in UTC. Geographic anomaly must calculate based on actual time difference, not clock time.
- **Transaction Completed Event Replay:** If Wolverine replays `TransactionCompleted` events (e.g., after recovery), the fraud handler must be idempotent. Use transaction_id + rule_type as a deduplication key to avoid duplicate alerts.
- **Concurrent Rule Evaluation:** Multiple rules may trigger for the same transaction. All triggered rules should create individual alerts, but only the most severe action should be applied to the account (e.g., if one rule says "alert" and another says "suspend", suspend wins).
- **Alert Storm:** A single compromised account may trigger dozens of alerts. Implement alert deduplication: if an account already has an active (new/investigating) alert for the same rule_type, do not create another; instead, update the existing alert's details with the new transaction.
- **Self-Healing:** If an account was auto-suspended but the alert is later marked as false_positive, the system should prompt (but not automatically execute) account reactivation.
- **Rule Engine Performance:** For high-volume tenants, rule evaluation must not become a bottleneck. Use Redis for recent transaction counters (INCR with TTL) rather than querying the database for velocity checks.

---

## Dependencies

**Prerequisite Stories:**
- STORY-058: Transaction History & Statements — provides the transaction data and events that the fraud detection engine evaluates

**Blocked Stories:**
- STORY-075: Security Audit & Hardening — fraud detection is part of the security posture reviewed during the audit
- STORY-076: Pilot Deployment Preparation — fraud detection must be operational before pilot launch

**External Dependencies:**
- Redis instance for fraud rule caching and velocity counters
- Wolverine message bus operational for event-driven processing
- Admin portal (STORY-071) for fraud alert management UI

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
  - [ ] Each fraud rule evaluates correctly against known test scenarios
  - [ ] Rule engine correctly applies most severe action when multiple rules trigger
  - [ ] Alert deduplication prevents duplicate alerts for same account + rule_type
  - [ ] Cache invalidation propagates rule changes within 60 seconds
- [ ] Integration tests passing
  - [ ] TransactionCompleted event triggers fraud evaluation end-to-end
  - [ ] Alert created in database with correct severity and details
  - [ ] Account auto-suspended when rule action is "suspend"
  - [ ] Admin portal displays alerts with correct filtering and sorting
  - [ ] Rule threshold changes reflected in subsequent evaluations
- [ ] Code reviewed and approved
- [ ] Documentation updated
  - [ ] Fraud rule configuration guide for tenant administrators
  - [ ] Alert severity definitions and response procedures
  - [ ] Default rule thresholds and rationale documented
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Performance validated: fraud evaluation adds <200ms to event processing

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
