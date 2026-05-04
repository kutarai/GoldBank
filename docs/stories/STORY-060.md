# STORY-060: System Configuration Management (Admin)

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As a super admin
I want to configure fees, limits, and system parameters
So that the platform operates with correct business rules and can be adjusted without code deployments

---

## Description

### Background
GoldBank operates as a white-label platform serving multiple tenants, each potentially with different fee structures, transaction limits, and operational parameters. The system configuration management interface allows super admins to set global defaults and tenant admins to set tenant-specific overrides. Configuration changes are sensitive operations that affect revenue and customer experience, so they require an audit trail, optional scheduling (effective dates), and Redis cache invalidation to ensure changes propagate correctly across all services. This eliminates the need for code deployments to adjust business parameters.

### Scope
**In scope:**
- Global system configuration defaults (super_admin)
- Per-tenant configuration overrides (super_admin and tenant_admin)
- Configuration categories: transaction fees, transaction limits, agent commissions, security thresholds, notification settings
- Scheduled configuration changes with effective dates
- Configuration change audit log
- Redis cache invalidation on configuration changes
- Configuration comparison (current vs pending changes)
- Rollback capability for recent changes

**Out of scope:**
- Feature flags management (separate tooling)
- Infrastructure configuration (handled via DevOps)
- Tenant provisioning (separate admin workflow)
- Configuration templates or presets

### User Flow
1. Super admin navigates to "System Config" in the sidebar
2. Global configuration page shows all configuration categories in expandable sections
3. Admin selects a category (e.g., "Transaction Fees")
4. Current values are displayed in an editable form
5. Admin modifies values; changed fields are highlighted
6. Admin chooses effective date: "Immediate" or selects a future date/time
7. Admin clicks "Save Changes"
8. A confirmation modal shows: old values, new values, effective date
9. Admin confirms the change
10. System creates a `pending_config_changes` record (or applies immediately if "Immediate" was selected)
11. Redis cache is invalidated for affected configuration keys
12. Change is recorded in the `config_audit` table
13. For tenant-specific config: admin navigates to "Tenant Config", selects a tenant, and overrides specific values
14. Tenant overrides are displayed alongside global defaults for comparison

---

## Acceptance Criteria

- [ ] Super admin can view all global configuration parameters organized by category
- [ ] Super admin can modify transaction fee configuration by type (NFC, QR, P2P, bill pay, cash-in, cash-out)
- [ ] Super admin can modify transaction limits: per-transaction maximum, daily maximum, monthly maximum
- [ ] Super admin can modify agent commission rates by transaction type
- [ ] Super admin can modify security thresholds (e.g., suspicious transaction amount, velocity limits)
- [ ] Super admin can modify notification settings (SMS templates, push notification toggles)
- [ ] Configuration changes can be applied immediately or scheduled for a future effective date
- [ ] Scheduled changes are visible in a "Pending Changes" queue with the ability to cancel before effective date
- [ ] Per-tenant configuration overrides can be set that take precedence over global defaults
- [ ] Tenant configuration page shows global defaults alongside tenant-specific overrides for comparison
- [ ] `tenant_admin` can modify configuration only for their own tenant (within bounds set by super_admin)
- [ ] All configuration changes are logged in the `config_audit` table with: admin_user_id, config_key, old_value, new_value, effective_date, timestamp
- [ ] Redis cache is invalidated immediately when configuration changes take effect
- [ ] Recent configuration changes can be rolled back within 24 hours
- [ ] Configuration values are validated against min/max bounds before saving
- [ ] Changed fields are visually highlighted before save confirmation

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Config/SystemConfig.razor` -- global configuration management
  - `Pages/Config/TenantConfig.razor` -- per-tenant configuration overrides
  - `Pages/Config/PendingChanges.razor` -- scheduled changes queue
  - `Pages/Config/ConfigAuditLog.razor` -- configuration change history
- **Components:**
  - `Components/Config/ConfigCategoryPanel.razor` -- expandable configuration category section
  - `Components/Config/ConfigField.razor` -- individual config field with validation and change highlighting
  - `Components/Config/ConfigComparisonTable.razor` -- side-by-side global vs tenant comparison
  - `Components/Config/EffectiveDatePicker.razor` -- immediate or scheduled date selector
  - `Components/Config/ChangeConfirmationModal.razor` -- old vs new value comparison before save
  - `Components/Config/RollbackModal.razor` -- rollback confirmation with affected values
- **Services:**
  - `Services/ConfigurationService.cs` -- config CRUD, validation, and Redis cache management
  - `Services/ConfigSchedulerService.cs` -- background service that applies scheduled config changes at their effective time

### API / gRPC Endpoints
```protobuf
service AdminConfigService {
  rpc GetSystemConfig (GetSystemConfigRequest) returns (SystemConfigResponse);
  rpc UpdateSystemConfig (UpdateSystemConfigRequest) returns (UpdateSystemConfigResponse);
  rpc GetTenantConfig (GetTenantConfigRequest) returns (TenantConfigResponse);
  rpc UpdateTenantConfig (UpdateTenantConfigRequest) returns (UpdateTenantConfigResponse);
  rpc GetPendingChanges (GetPendingChangesRequest) returns (PendingChangesResponse);
  rpc CancelPendingChange (CancelPendingChangeRequest) returns (CancelPendingChangeResponse);
  rpc GetConfigAuditLog (GetConfigAuditLogRequest) returns (ConfigAuditLogResponse);
  rpc RollbackConfigChange (RollbackConfigChangeRequest) returns (RollbackConfigChangeResponse);
}

message GetSystemConfigRequest {
  string category = 1;  // optional filter: transaction_fees, transaction_limits, etc.
}

message SystemConfigResponse {
  repeated ConfigCategory categories = 1;
}

message ConfigCategory {
  string name = 1;
  string display_name = 2;
  string description = 3;
  repeated ConfigEntry entries = 4;
}

message ConfigEntry {
  string key = 1;                    // e.g., "fees.nfc.percentage"
  string display_name = 2;
  string description = 3;
  string value = 4;
  string data_type = 5;             // int, decimal, boolean, string
  string unit = 6;                  // percentage, minor_units, seconds, etc.
  string min_value = 7;
  string max_value = 8;
  bool is_overridable = 9;          // can tenants override this?
}

message UpdateSystemConfigRequest {
  repeated ConfigChange changes = 1;
  string effective_date = 2;        // ISO 8601, empty = immediate
  string admin_user_id = 3;
  string change_reason = 4;         // optional description of why the change is made
}

message ConfigChange {
  string key = 1;
  string new_value = 2;
}

message UpdateSystemConfigResponse {
  bool success = 1;
  string message = 2;
  repeated ConfigChangeResult results = 3;
}

message ConfigChangeResult {
  string key = 1;
  string old_value = 2;
  string new_value = 3;
  bool applied = 4;
  string error = 5;                 // if validation failed
}

message GetTenantConfigRequest {
  string tenant_id = 1;
  string category = 2;             // optional filter
}

message TenantConfigResponse {
  string tenant_id = 1;
  string tenant_name = 2;
  repeated TenantConfigCategory categories = 3;
}

message TenantConfigCategory {
  string name = 1;
  string display_name = 2;
  repeated TenantConfigEntry entries = 3;
}

message TenantConfigEntry {
  string key = 1;
  string display_name = 2;
  string global_value = 3;         // the system default
  string tenant_value = 4;         // the override (empty = using global)
  string effective_value = 5;      // what is actually in effect
  string data_type = 6;
  string unit = 7;
  string min_value = 8;
  string max_value = 9;
}

message RollbackConfigChangeRequest {
  string audit_log_id = 1;
  string admin_user_id = 2;
  string reason = 3;
}
```

### Database Changes
```sql
-- System configuration table (global defaults)
CREATE TABLE admin.system_config (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    category        VARCHAR(50) NOT NULL,
    config_key      VARCHAR(100) NOT NULL UNIQUE,
    display_name    VARCHAR(200) NOT NULL,
    description     TEXT,
    config_value    VARCHAR(500) NOT NULL,
    data_type       VARCHAR(20) NOT NULL,   -- int, decimal, boolean, string
    unit            VARCHAR(50),            -- percentage, minor_units, seconds
    min_value       VARCHAR(100),
    max_value       VARCHAR(100),
    is_overridable  BOOLEAN NOT NULL DEFAULT true,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_system_config_category ON admin.system_config(category);

-- Tenant configuration overrides
CREATE TABLE admin.tenant_config_overrides (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id),
    config_key      VARCHAR(100) NOT NULL REFERENCES admin.system_config(config_key),
    config_value    VARCHAR(500) NOT NULL,
    updated_by      UUID NOT NULL REFERENCES admin.admin_users(id),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(tenant_id, config_key)
);

CREATE INDEX idx_tenant_config_tenant ON admin.tenant_config_overrides(tenant_id);

-- Pending configuration changes (scheduled)
CREATE TABLE admin.pending_config_changes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID,                   -- NULL for global changes
    config_key      VARCHAR(100) NOT NULL,
    old_value       VARCHAR(500),
    new_value       VARCHAR(500) NOT NULL,
    effective_date  TIMESTAMPTZ NOT NULL,
    status          VARCHAR(20) NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'applied', 'cancelled')),
    created_by      UUID NOT NULL REFERENCES admin.admin_users(id),
    change_reason   TEXT,
    applied_at      TIMESTAMPTZ,
    cancelled_at    TIMESTAMPTZ,
    cancelled_by    UUID REFERENCES admin.admin_users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_pending_config_status ON admin.pending_config_changes(status, effective_date)
    WHERE status = 'pending';

-- Configuration audit log
CREATE TABLE admin.config_audit (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID,                   -- NULL for global changes
    admin_user_id   UUID NOT NULL REFERENCES admin.admin_users(id),
    config_key      VARCHAR(100) NOT NULL,
    old_value       VARCHAR(500),
    new_value       VARCHAR(500) NOT NULL,
    effective_date  TIMESTAMPTZ NOT NULL,
    change_reason   TEXT,
    change_type     VARCHAR(20) NOT NULL,   -- update, rollback, scheduled
    is_rollbackable BOOLEAN NOT NULL DEFAULT true,
    rollback_deadline TIMESTAMPTZ,          -- 24 hours from application
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_config_audit_key ON admin.config_audit(config_key);
CREATE INDEX idx_config_audit_tenant ON admin.config_audit(tenant_id);
CREATE INDEX idx_config_audit_admin ON admin.config_audit(admin_user_id);
CREATE INDEX idx_config_audit_created ON admin.config_audit(created_at);

-- Seed initial configuration values
INSERT INTO admin.system_config (category, config_key, display_name, description, config_value, data_type, unit, min_value, max_value) VALUES
    ('transaction_fees', 'fees.nfc.percentage', 'NFC Transaction Fee (%)', 'Fee charged on NFC tap-to-pay transactions', '1.50', 'decimal', 'percentage', '0.00', '5.00'),
    ('transaction_fees', 'fees.qr.percentage', 'QR Transaction Fee (%)', 'Fee charged on QR code payment transactions', '1.00', 'decimal', 'percentage', '0.00', '5.00'),
    ('transaction_fees', 'fees.p2p.flat', 'P2P Transfer Fee (flat)', 'Flat fee on person-to-person transfers', '200', 'int', 'minor_units', '0', '10000'),
    ('transaction_fees', 'fees.bill_pay.percentage', 'Bill Payment Fee (%)', 'Fee on bill payment transactions', '1.50', 'decimal', 'percentage', '0.00', '5.00'),
    ('transaction_fees', 'fees.cash_in.percentage', 'Cash-In Fee (%)', 'Fee on cash deposit at agent', '0.50', 'decimal', 'percentage', '0.00', '3.00'),
    ('transaction_fees', 'fees.cash_out.percentage', 'Cash-Out Fee (%)', 'Fee on cash withdrawal at agent', '2.00', 'decimal', 'percentage', '0.00', '5.00'),
    ('transaction_limits', 'limits.per_transaction.max', 'Per-Transaction Maximum', 'Maximum single transaction amount', '5000000', 'int', 'minor_units', '10000', '50000000'),
    ('transaction_limits', 'limits.daily.max', 'Daily Transaction Maximum', 'Maximum total daily transaction amount', '25000000', 'int', 'minor_units', '100000', '250000000'),
    ('transaction_limits', 'limits.monthly.max', 'Monthly Transaction Maximum', 'Maximum total monthly transaction amount', '500000000', 'int', 'minor_units', '1000000', '5000000000'),
    ('agent_commissions', 'commission.cash_in.percentage', 'Agent Cash-In Commission (%)', 'Commission paid to agent on cash-in', '0.30', 'decimal', 'percentage', '0.00', '2.00'),
    ('agent_commissions', 'commission.cash_out.percentage', 'Agent Cash-Out Commission (%)', 'Commission paid to agent on cash-out', '0.50', 'decimal', 'percentage', '0.00', '2.00'),
    ('security_thresholds', 'security.suspicious_amount', 'Suspicious Transaction Amount', 'Transactions above this amount flagged for review', '10000000', 'int', 'minor_units', '1000000', '100000000'),
    ('security_thresholds', 'security.velocity_limit.count', 'Velocity Limit (count/hour)', 'Max transactions per hour before flag', '20', 'int', 'count', '5', '100'),
    ('notification_settings', 'notifications.sms.transaction_confirm', 'SMS Transaction Confirmation', 'Send SMS on every transaction', 'true', 'boolean', '', '', ''),
    ('notification_settings', 'notifications.push.enabled', 'Push Notifications Enabled', 'Master toggle for push notifications', 'true', 'boolean', '', '', '');
```

### Security Considerations
- Only `super_admin` can modify global system configuration
- `tenant_admin` can modify tenant-specific overrides only for their own tenant and only for keys marked `is_overridable = true`
- `finance` role has read-only access to fee and commission configuration
- Configuration changes require confirmation modal (two-step save)
- All changes are immutably audit-logged
- Rollback is limited to 24 hours and itself creates an audit entry
- Configuration values are validated against min/max bounds server-side (client-side validation is supplementary)
- Redis cache invalidation uses pub/sub to ensure all service instances receive the notification
- Scheduled changes are applied by a background service with its own authentication context

### Edge Cases
- Conflicting scheduled changes: if two pending changes affect the same key, the later-scheduled one wins; admin is warned at creation time
- Redis unavailable during cache invalidation: services fall back to database query with short TTL caching; alert is raised
- Invalid configuration value: server-side validation rejects with specific error message per field
- Tenant override exceeding global bounds: reject with message "Tenant override value {value} exceeds maximum bound {max}"
- Rollback of a change that has been superseded: warn admin that a newer change exists and rollback would override it
- Configuration change during high traffic: changes are atomic and eventually consistent; in-flight transactions use the configuration that was active when they started
- Bulk changes across multiple categories: all changes in a single request are applied atomically (all succeed or all fail)

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** None directly; all transaction-processing stories consume configuration values from this system
**External Dependencies:**
- Redis for configuration caching and pub/sub cache invalidation
- Background scheduler for applying pending configuration changes (Wolverine scheduled jobs or Hangfire)

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
