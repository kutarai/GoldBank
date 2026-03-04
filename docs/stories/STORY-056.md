# STORY-056: Customer Account Management (Admin)

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As a support admin
I want to search, view, and manage customer accounts
So that I can assist customers with account issues and perform administrative actions

---

## Description

### Background
Support and operations staff need the ability to look up customer accounts quickly, view comprehensive account details, and perform management actions such as suspending or closing accounts. This is a core back-office function that enables the support team to resolve customer queries, investigate issues, and take necessary administrative actions. Every action must be audit-logged to maintain a complete trail of who did what and why, which is critical for compliance in the Southern African banking regulatory environment.

### Scope
**In scope:**
- Customer search with multiple criteria (name, phone, ID number)
- Full customer detail view (profile, KYC status, balance, transactions, devices)
- Account management actions: suspend, freeze, close, reactivate
- Mandatory reason capture for all management actions
- Audit logging of all admin actions on customer accounts
- Paginated results with server-side filtering
- Tenant-scoped access for `tenant_admin` role

**Out of scope:**
- Editing customer personal details (customers self-manage via mobile app)
- Creating customer accounts (self-registration only)
- Balance adjustments (handled via dispute/chargeback process in STORY-061)
- Bulk operations on multiple accounts simultaneously

### User Flow
1. Support admin navigates to "Customers" in the sidebar
2. The customer search page is displayed with search input fields
3. Admin enters search criteria (name, phone number, or ID number) and clicks Search
4. System returns paginated list of matching customers with summary info (name, phone, account status, KYC status)
5. Admin clicks on a customer row to view full details
6. Detail page shows: personal info, KYC status, account balance, recent transactions (last 20), registered devices, account status history
7. To take an action, admin clicks "Manage Account" and selects an action (suspend/freeze/close/reactivate)
8. A modal prompts for a mandatory reason text
9. Admin enters reason and confirms
10. System executes the action, updates account status, creates audit log entry, and displays success confirmation
11. If the customer is associated with a different tenant than the admin's scope, access is denied

---

## Acceptance Criteria

- [ ] Support admin can search customers by full name (partial match, case-insensitive)
- [ ] Support admin can search customers by phone number (exact match)
- [ ] Support admin can search customers by national ID number (exact match)
- [ ] Search results are paginated (default 25 per page) with total count displayed
- [ ] Customer detail page displays: full name, phone, email, ID number, date of birth, registration date, KYC status, account balance, account status
- [ ] Customer detail page shows last 20 transactions with date, type, amount, status
- [ ] Customer detail page shows registered devices with device type, OS, last active date
- [ ] Customer detail page shows account status history (all status changes with dates, actors, reasons)
- [ ] Admin can suspend an account (temporarily block all transactions)
- [ ] Admin can freeze an account (block all transactions and balance changes, regulatory hold)
- [ ] Admin can close an account (permanent closure, requires zero balance)
- [ ] Admin can reactivate a suspended or frozen account
- [ ] All management actions require a mandatory reason (minimum 10 characters)
- [ ] All management actions are audit logged with admin_user_id, action, reason, timestamp, IP address
- [ ] `tenant_admin` users can only search and view customers within their tenant
- [ ] `super_admin` and `operations` roles can search across all tenants
- [ ] `support` role can search and view but requires `operations` approval for close action

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Customers/CustomerSearch.razor` -- search form and results table
  - `Pages/Customers/CustomerDetail.razor` -- full customer detail view
  - `Components/Customers/CustomerSearchForm.razor` -- reusable search input component
  - `Components/Customers/CustomerSummaryCard.razor` -- profile summary card
  - `Components/Customers/AccountActionModal.razor` -- action confirmation modal with reason input
  - `Components/Customers/TransactionHistoryTable.razor` -- recent transactions table
  - `Components/Customers/DeviceList.razor` -- registered devices list
  - `Components/Customers/StatusTimeline.razor` -- account status change history

### API / gRPC Endpoints
```protobuf
service AdminCustomerService {
  rpc SearchCustomers (SearchCustomersRequest) returns (SearchCustomersResponse);
  rpc GetCustomerDetail (GetCustomerDetailRequest) returns (CustomerDetailResponse);
  rpc ManageAccount (ManageAccountRequest) returns (ManageAccountResponse);
  rpc GetAccountStatusHistory (GetAccountStatusHistoryRequest) returns (AccountStatusHistoryResponse);
}

message SearchCustomersRequest {
  string query = 1;           // general search term
  string phone = 2;            // exact phone match
  string id_number = 3;        // exact ID number match
  string tenant_id = 4;        // auto-populated for tenant_admin
  int32 page = 5;
  int32 page_size = 6;         // default 25, max 100
}

message SearchCustomersResponse {
  repeated CustomerSummary customers = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}

message CustomerSummary {
  string account_id = 1;
  string full_name = 2;
  string phone = 3;
  string kyc_status = 4;
  string account_status = 5;
  string tenant_id = 6;
  google.protobuf.Timestamp registered_at = 7;
}

message GetCustomerDetailRequest {
  string account_id = 1;
}

message CustomerDetailResponse {
  string account_id = 1;
  string full_name = 2;
  string phone = 3;
  string email = 4;
  string id_number = 5;
  google.protobuf.Timestamp date_of_birth = 6;
  string kyc_status = 7;
  string account_status = 8;
  int64 balance_minor_units = 9;   // balance in cents
  string currency = 10;
  string tenant_id = 11;
  google.protobuf.Timestamp registered_at = 12;
  repeated RecentTransaction recent_transactions = 13;
  repeated RegisteredDevice devices = 14;
}

message ManageAccountRequest {
  string account_id = 1;
  string action = 2;          // suspend, freeze, close, reactivate
  string reason = 3;          // mandatory, min 10 chars
  string admin_user_id = 4;   // from auth context
}

message ManageAccountResponse {
  bool success = 1;
  string new_status = 2;
  string message = 3;
}
```

### Database Changes
```sql
-- Account status history table (in tenant schema)
CREATE TABLE {tenant_schema}.account_status_history (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    previous_status VARCHAR(20),
    new_status      VARCHAR(20) NOT NULL,
    action          VARCHAR(20) NOT NULL, -- suspend, freeze, close, reactivate
    reason          TEXT NOT NULL,
    changed_by      UUID NOT NULL,        -- admin_user_id
    changed_by_type VARCHAR(10) NOT NULL DEFAULT 'admin', -- admin or system
    ip_address      INET,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_account_status_history_account ON {tenant_schema}.account_status_history(account_id);
CREATE INDEX idx_account_status_history_created ON {tenant_schema}.account_status_history(created_at);

-- Full-text search index on accounts for name search
CREATE INDEX idx_accounts_name_trgm ON {tenant_schema}.accounts
    USING gin (full_name gin_trgm_ops);
-- Requires: CREATE EXTENSION IF NOT EXISTS pg_trgm;
```

### Security Considerations
- Tenant isolation enforced at the gRPC service layer: `tenant_admin` requests automatically filtered by `tenant_id` from JWT claims
- `super_admin` can optionally specify `tenant_id` or search across all tenants
- Account close action requires `operations` or `super_admin` role (support cannot close directly)
- Reason field is mandatory and validated server-side (minimum 10 characters, maximum 1000)
- PII (ID numbers, phone numbers) displayed with partial masking by default; full reveal requires explicit "Show" click that is audit-logged
- Rate limiting on search to prevent data scraping (max 30 searches per minute per admin)
- Customer detail access is audit-logged (view events, not just action events)

### Edge Cases
- Search with no results: display "No customers found" message with suggestion to check criteria
- Account already in requested status: return informative error (e.g., "Account is already suspended")
- Close account with non-zero balance: reject with message "Account balance must be zero before closing. Current balance: {amount}"
- Concurrent status change: use optimistic concurrency (version column on accounts) to prevent race conditions
- Large search results: enforce pagination, maximum 100 per page, never return unbounded results
- Deleted/purged accounts: show tombstone record with limited info and "Account Purged" status
- Phone number search with country code variations: normalise to E.164 format before search

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** None directly; supports STORY-061 (Dispute Management) workflows
**External Dependencies:** `pg_trgm` PostgreSQL extension for trigram-based full-text search

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
