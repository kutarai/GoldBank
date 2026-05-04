# STORY-057: Merchant/Agent Management (Admin)

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As an operations admin
I want to approve, manage, and configure merchants and agents
So that the network is properly managed and only verified merchants operate on the platform

---

## Description

### Background
GoldBank's merchant and agent network is central to its strategy of serving the unbanked population in Southern Africa. Agents provide cash-in/cash-out services in physical locations, while merchants accept digital payments. Operations staff must be able to review and approve merchant applications, manage active merchants, configure per-merchant fee overrides and limits, and monitor agent performance. A well-managed merchant/agent network directly impacts financial inclusion reach and platform revenue.

### Scope
**In scope:**
- Merchant application approval/rejection queue
- Merchant list view with search and filters
- Merchant detail view with configuration
- Per-merchant fee and limit overrides (over tenant defaults)
- Agent-specific management: enable/disable agent flag, set float limits
- Merchant dashboard: counts by status, agent count, geographic distribution
- All management actions audit-logged

**Out of scope:**
- Merchant self-registration (handled via mobile/web onboarding flow)
- Terminal provisioning and management (separate story)
- Merchant settlement processing
- Merchant-facing portal

### User Flow
1. Operations admin navigates to "Merchants" in the sidebar
2. Dashboard shows merchant summary: total count, pending approvals, active, suspended, agent count
3. Admin clicks "Pending Approvals" to view the approval queue
4. Queue shows merchant applications sorted by submission date (FIFO)
5. Admin clicks on a pending application to review
6. Review page shows: business details, owner info, KYC documents, business registration documents, proposed location
7. Admin reviews documents and either approves, rejects (with reason), or requests additional information
8. For approved merchants, admin can configure: fee overrides, transaction limits, agent eligibility
9. From the merchant list, admin can view any merchant's detail page
10. Detail page shows: profile, configuration, transaction summary, status history
11. Admin can suspend, deactivate, or reactivate merchants with a mandatory reason
12. For agents, admin can enable/disable the agent flag and set float limits

---

## Acceptance Criteria

- [ ] Operations admin can view a paginated list of all merchants with search (by name, registration number)
- [ ] Pending merchant applications are displayed in a FIFO queue with submission date
- [ ] Admin can approve a merchant application with optional configuration overrides
- [ ] Admin can reject a merchant application with a mandatory reason
- [ ] Admin can request additional information from the applicant (triggers notification)
- [ ] Approved merchants appear in the active merchant list immediately
- [ ] Admin can view full merchant details: business info, owner info, KYC status, location, transaction summary
- [ ] Admin can configure per-merchant fee overrides that take precedence over tenant defaults
- [ ] Admin can configure per-merchant transaction limits
- [ ] Admin can enable/disable the agent flag on a merchant
- [ ] Admin can set float limits for agents (maximum float balance)
- [ ] Admin can suspend a merchant (blocks all transactions) with mandatory reason
- [ ] Admin can deactivate a merchant (permanent removal from network) with mandatory reason
- [ ] Admin can reactivate a suspended merchant
- [ ] Merchant dashboard displays: count by status, agent count, geographic distribution map
- [ ] All management actions are audit-logged with admin_user_id, action, reason, timestamp
- [ ] `tenant_admin` sees only merchants within their tenant

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Merchants/MerchantDashboard.razor` -- summary dashboard with metrics and charts
  - `Pages/Merchants/MerchantList.razor` -- searchable, filterable merchant list
  - `Pages/Merchants/MerchantDetail.razor` -- full merchant detail and configuration
  - `Pages/Merchants/MerchantApproval.razor` -- pending approval queue
  - `Pages/Merchants/MerchantReview.razor` -- individual application review page
- **Components:**
  - `Components/Merchants/MerchantSearchBar.razor` -- search and filter controls
  - `Components/Merchants/MerchantConfigForm.razor` -- fee/limit configuration form
  - `Components/Merchants/AgentConfigPanel.razor` -- agent-specific settings
  - `Components/Merchants/MerchantStatusBadge.razor` -- visual status indicator
  - `Components/Merchants/GeoDistributionMap.razor` -- geographic distribution visualisation
  - `Components/Merchants/ApprovalActionPanel.razor` -- approve/reject/request-info buttons with reason input

### API / gRPC Endpoints
```protobuf
service AdminMerchantService {
  rpc GetMerchantDashboard (GetMerchantDashboardRequest) returns (MerchantDashboardResponse);
  rpc ListMerchants (ListMerchantsRequest) returns (ListMerchantsResponse);
  rpc GetMerchantDetail (GetMerchantDetailRequest) returns (MerchantDetailResponse);
  rpc GetPendingApprovals (GetPendingApprovalsRequest) returns (PendingApprovalsResponse);
  rpc ReviewMerchantApplication (ReviewMerchantApplicationRequest) returns (ReviewMerchantApplicationResponse);
  rpc ManageMerchant (ManageMerchantRequest) returns (ManageMerchantResponse);
  rpc UpdateMerchantConfig (UpdateMerchantConfigRequest) returns (MerchantConfigResponse);
  rpc UpdateAgentConfig (UpdateAgentConfigRequest) returns (AgentConfigResponse);
}

message GetMerchantDashboardRequest {
  string tenant_id = 1; // optional, auto-set for tenant_admin
}

message MerchantDashboardResponse {
  int32 total_merchants = 1;
  int32 pending_approvals = 2;
  int32 active_merchants = 3;
  int32 suspended_merchants = 4;
  int32 total_agents = 5;
  repeated StatusCount status_breakdown = 6;
  repeated GeoPoint geographic_distribution = 7;
}

message ReviewMerchantApplicationRequest {
  string merchant_id = 1;
  string action = 2;       // approve, reject, request_info
  string reason = 3;       // mandatory for reject/request_info
  MerchantConfig initial_config = 4; // optional, for approve
}

message UpdateMerchantConfigRequest {
  string merchant_id = 1;
  map<string, string> fee_overrides = 2;    // fee_type -> amount (minor units)
  map<string, string> limit_overrides = 3;  // limit_type -> amount (minor units)
  string effective_date = 4;                 // ISO 8601, empty = immediate
}

message UpdateAgentConfigRequest {
  string merchant_id = 1;
  bool is_agent = 2;
  int64 float_limit_minor_units = 3;
  bool allow_cash_in = 4;
  bool allow_cash_out = 5;
}
```

### Database Changes
```sql
-- Merchant configuration overrides (in tenant schema)
CREATE TABLE {tenant_schema}.merchant_config_overrides (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id     UUID NOT NULL REFERENCES {tenant_schema}.merchants(id),
    config_key      VARCHAR(100) NOT NULL,   -- e.g., fee.nfc, limit.daily_transaction
    config_value    VARCHAR(255) NOT NULL,
    effective_from  TIMESTAMPTZ NOT NULL DEFAULT now(),
    effective_to    TIMESTAMPTZ,             -- NULL = no expiry
    created_by      UUID NOT NULL,           -- admin_user_id
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(merchant_id, config_key, effective_from)
);

CREATE INDEX idx_merchant_config_merchant ON {tenant_schema}.merchant_config_overrides(merchant_id);

-- Agent configuration (in tenant schema)
CREATE TABLE {tenant_schema}.agent_config (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id     UUID NOT NULL UNIQUE REFERENCES {tenant_schema}.merchants(id),
    is_agent        BOOLEAN NOT NULL DEFAULT false,
    float_limit     BIGINT NOT NULL DEFAULT 0,   -- in minor units
    allow_cash_in   BOOLEAN NOT NULL DEFAULT true,
    allow_cash_out  BOOLEAN NOT NULL DEFAULT true,
    current_float   BIGINT NOT NULL DEFAULT 0,   -- current float balance
    updated_by      UUID,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Merchant application review log
CREATE TABLE {tenant_schema}.merchant_review_log (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id     UUID NOT NULL REFERENCES {tenant_schema}.merchants(id),
    action          VARCHAR(20) NOT NULL,  -- approve, reject, request_info, suspend, deactivate, reactivate
    reason          TEXT,
    reviewed_by     UUID NOT NULL,         -- admin_user_id
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_merchant_review_merchant ON {tenant_schema}.merchant_review_log(merchant_id);
```

### Security Considerations
- Only `operations` and `super_admin` roles can approve/reject merchant applications
- `support` role has read-only access to merchant details
- `tenant_admin` can manage merchants within their tenant but cannot approve new merchants (requires `operations`)
- Fee and limit overrides are validated against system-wide bounds (cannot set fees below cost or limits above regulatory maximum)
- Geographic coordinates are stored but not exposed to non-operations roles
- Merchant KYC documents are served via time-limited pre-signed URLs (expire after 15 minutes)

### Edge Cases
- Concurrent approval: two admins reviewing the same application -- use optimistic locking to prevent double-approve
- Merchant with active transactions during suspension: pending transactions complete, new transactions blocked
- Agent float limit reached: system should prevent cash-out transactions that would exceed float limit
- Re-application after rejection: track previous applications and display rejection history on review page
- Merchant with terminals during deactivation: all terminals must be decommissioned first, or system auto-decommissions
- Large number of pending approvals: implement priority sorting (by wait time) and assignment to prevent duplicate review effort

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** None directly
**External Dependencies:** Mapping library for geographic distribution (e.g., Leaflet.Blazor or similar)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
