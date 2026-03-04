# STORY-071: Per-Tenant Admin Portal Access

**Epic:** EPIC-013 White-Label Configuration
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 8

---

## User Story

As a deploying institution
I want my own admin portal view with isolation
So that I can manage my deployment independently

---

## Description

### Background

UniBank's white-label model means multiple deploying institutions share the underlying platform while each operates independently. With the core white-label configuration (STORY-055) and branding engine (STORY-069) in place, this final story delivers the admin portal experience that enforces strict tenant isolation. This is critical for the pilot launch: the first deploying institution must be able to log into their own admin portal, see only their data, manage their users, and configure their deployment without any risk of cross-tenant data leakage.

In the Southern African context, regulatory requirements demand clear data isolation between different licensed financial institutions. Each deploying institution's compliance team must be confident that their data is not visible to other tenants and that their administrative actions affect only their own deployment. This story delivers that assurance through technical controls at the authentication, authorization, and data access layers.

### Scope

**In scope:**
- Tenant-scoped JWT claims and login flow for admin portal
- Tenant admin role hierarchy (tenant_admin, tenant_operations, tenant_support, tenant_finance)
- Data isolation enforcement on all admin portal pages via tenant_id claim filtering
- Super admin multi-tenant view with tenant selector
- Cross-tenant access detection, logging, and blocking
- Authorization policies enforcing tenant boundaries
- Tenant admin user management within a tenant
- Audit logging of all admin actions scoped to tenant

**Out of scope:**
- Tenant self-provisioning (handled by super admin or deployment automation)
- Branding customization of admin portal UI (covered in STORY-069)
- Billing or usage metering per tenant
- Tenant-specific custom admin pages or plugins
- Federation with external identity providers (future enhancement)

### User Flow

**Tenant Admin Login:**
1. Tenant admin navigates to admin portal URL (e.g., `admin.institution-name.co.za` or `admin.unibank.co.za/tenant-slug`)
2. Login page displays tenant branding (logo, colors from branding config)
3. Admin enters credentials (email + password + optional MFA)
4. System authenticates against `admin_users` table, verifying `tenant_id` match
5. JWT issued with claims: `sub` (user_id), `tenant_id`, `role` (tenant-scoped role), `permissions`
6. Admin redirected to tenant-scoped dashboard showing only their institution's data

**Super Admin Login:**
1. Super admin navigates to admin portal root URL
2. Login page shows UniBank platform branding
3. Super admin authenticates with elevated credentials + mandatory MFA
4. JWT issued with claims: `sub`, `role=super_admin`, `tenant_id=*` (wildcard)
5. Admin portal shows tenant selector dropdown in top navigation bar
6. Super admin selects a tenant to switch context; all pages filter to selected tenant
7. Super admin can also view cross-tenant aggregate dashboards

**Tenant Admin Managing Users:**
1. Tenant admin navigates to Settings > Admin Users
2. Sees list of admin users for their tenant only
3. Can invite new admin user (email, role assignment within tenant roles)
4. Can deactivate/reactivate admin users within their tenant
5. Cannot see or modify admin users from other tenants

**Cross-Tenant Access Attempt:**
1. Malicious or misconfigured request includes tenant_id different from JWT claim
2. Authorization middleware intercepts, compares route/query tenant_id with JWT tenant_id
3. Request blocked with HTTP 403 Forbidden
4. Security event logged: `CrossTenantAccessAttempt` with source tenant, target tenant, user, endpoint, timestamp
5. If repeated attempts detected (>3 in 5 minutes), alert raised to super admin

---

## Acceptance Criteria

- [ ] Tenant admins can only see data belonging to their own tenant across all admin portal pages (dashboard, users, transactions, configuration, reports)
- [ ] Super admin can view all tenants via a tenant selector dropdown in the top navigation bar
- [ ] Super admin tenant selector switches all page data to the selected tenant's context
- [ ] Tenant admin roles are independent per tenant (e.g., user can be tenant_admin in Tenant A and tenant_operations in Tenant B if accounts exist in both)
- [ ] Tenant admins cannot access other tenants' configurations, users, transactions, or any other data
- [ ] Cross-tenant access attempts return HTTP 403 and are logged as security events
- [ ] JWT tokens contain `tenant_id` claim that determines data visibility throughout the portal
- [ ] Tenant admin login flow correctly resolves tenant context from URL or login form
- [ ] Four tenant-scoped roles are supported: tenant_admin, tenant_operations, tenant_support, tenant_finance
- [ ] Each tenant-scoped role has appropriate permission boundaries (see Technical Notes)
- [ ] Admin actions are audit-logged with tenant_id, user_id, action, and timestamp
- [ ] Session timeout enforced at 30 minutes of inactivity for tenant admins
- [ ] Super admin sessions require re-authentication when switching to a different tenant context

---

## Technical Notes

### Components

- **Admin Portal (Blazor Server):** `src/AdminPortal/` — all pages must enforce tenant scoping
  - `Pages/Auth/Login.razor` — tenant-aware login page
  - `Pages/Dashboard/TenantDashboard.razor` — tenant-scoped metrics
  - `Pages/Users/AdminUserManagement.razor` — tenant-scoped user CRUD
  - `Shared/TenantSelector.razor` — super admin tenant context switcher
  - `Shared/MainLayout.razor` — inject tenant context into layout
- **Identity Module:** `src/Modules/Identity/` — JWT issuance with tenant claims
  - `Services/AdminAuthenticationService.cs` — admin login, JWT generation
  - `Policies/TenantScopedAuthorizationHandler.cs` — authorization policy enforcement
  - `Middleware/TenantContextMiddleware.cs` — extract and validate tenant_id from JWT
- **Core Banking Module:** `src/Modules/CoreBanking/` — data access filtered by tenant
  - All repository queries must include `.Where(x => x.TenantId == currentTenantId)`
- **Infrastructure:** `src/Infrastructure/`
  - `Security/CrossTenantAccessDetector.cs` — detect and log cross-tenant attempts
  - `Audit/AdminAuditLogger.cs` — structured audit logging for admin actions

### API / gRPC Endpoints

Admin portal is Blazor Server (SignalR-based), so endpoints are internal circuit calls, not REST/gRPC. However, the following backing services are exposed:

| Service Method | Description | Auth Policy |
|---|---|---|
| `AdminAuth.Login` | Authenticate admin user, return JWT | Public (rate-limited) |
| `AdminAuth.RefreshToken` | Refresh admin JWT | Authenticated |
| `AdminUsers.List` | List admin users for tenant | TenantScoped + tenant_admin |
| `AdminUsers.Create` | Create admin user within tenant | TenantScoped + tenant_admin |
| `AdminUsers.Update` | Update admin user role/status | TenantScoped + tenant_admin |
| `AdminUsers.Deactivate` | Deactivate admin user | TenantScoped + tenant_admin |
| `TenantContext.Switch` | Super admin switches tenant view | SuperAdmin only |
| `AuditLog.Query` | Query audit log for tenant | TenantScoped + tenant_admin or tenant_finance |

### Database Changes

**Table: `admin_users`** (in shared `admin` schema)

```sql
CREATE TABLE admin.admin_users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL REFERENCES admin.tenants(id),
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(512) NOT NULL,
    display_name VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL CHECK (role IN ('tenant_admin', 'tenant_operations', 'tenant_support', 'tenant_finance')),
    is_active BOOLEAN NOT NULL DEFAULT true,
    mfa_enabled BOOLEAN NOT NULL DEFAULT false,
    mfa_secret VARCHAR(255),
    last_login_at TIMESTAMPTZ,
    failed_login_attempts INTEGER NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    UNIQUE(tenant_id, email)
);

CREATE INDEX idx_admin_users_tenant ON admin.admin_users(tenant_id);
CREATE INDEX idx_admin_users_email ON admin.admin_users(email);
```

**Table: `admin_audit_log`** (in shared `admin` schema)

```sql
CREATE TABLE admin.admin_audit_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    user_id UUID NOT NULL,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100),
    entity_id UUID,
    details JSONB,
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_admin_audit_tenant ON admin.admin_audit_log(tenant_id, created_at DESC);
```

**Table: `cross_tenant_access_attempts`** (in shared `admin` schema)

```sql
CREATE TABLE admin.cross_tenant_access_attempts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_tenant_id UUID NOT NULL,
    target_tenant_id UUID NOT NULL,
    user_id UUID NOT NULL,
    endpoint VARCHAR(500) NOT NULL,
    ip_address INET,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_cross_tenant_attempts_source ON admin.cross_tenant_access_attempts(source_tenant_id, created_at DESC);
```

**Tenant-Scoped Role Permissions:**

| Permission | tenant_admin | tenant_operations | tenant_support | tenant_finance |
|---|:---:|:---:|:---:|:---:|
| View dashboard | Y | Y | Y | Y |
| Manage admin users | Y | N | N | N |
| View transactions | Y | Y | Y | Y |
| Reverse transactions | Y | Y | N | N |
| View customer data | Y | Y | Y | N |
| Suspend accounts | Y | Y | N | N |
| Configure fees/limits | Y | N | N | Y |
| Configure branding | Y | N | N | N |
| View audit logs | Y | N | N | Y |
| View financial reports | Y | N | N | Y |
| Manage merchants | Y | Y | N | N |
| Handle support tickets | Y | Y | Y | N |

### Security Considerations

- **Tenant Isolation is Non-Negotiable:** Every database query in the admin portal MUST filter by `tenant_id` extracted from the JWT. This must be enforced at the repository layer via a global query filter in EF Core, not left to individual page implementations.
- **JWT Tenant Claim Validation:** The `TenantScopedAuthorizationHandler` must validate that the `tenant_id` claim in the JWT matches the tenant context of the requested resource. This is the primary defense against cross-tenant access.
- **Global EF Core Query Filter:** Configure `modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _currentTenantId)` for all tenant-scoped entities to prevent accidental data leaks.
- **Super Admin Context Switching:** When a super admin switches tenant context, the new tenant_id must be stored server-side (in circuit state for Blazor Server), NOT in a client-side cookie or local storage that could be tampered with.
- **Password Hashing:** Use Argon2id for admin password hashing (stronger than bcrypt for this use case).
- **MFA Requirement:** MFA should be mandatory for tenant_admin and super_admin roles; optional but recommended for other roles.
- **Session Management:** Blazor Server circuits must be terminated on session timeout. Use server-side session tracking with Redis to enable cross-instance session invalidation.
- **Rate Limiting on Login:** Apply strict rate limiting (5 attempts per 15 minutes per email, 20 attempts per 15 minutes per IP) on the admin login endpoint to prevent brute force attacks.
- **Account Lockout:** Lock admin account after 5 consecutive failed login attempts for 30 minutes.
- **PCI-DSS Relevance:** Admin portal access to cardholder data (if displayed) must comply with PCI-DSS requirement 7 (restrict access by business need-to-know) and requirement 8 (identify and authenticate access).

### Edge Cases

- **Tenant Deleted/Disabled:** If a tenant is deactivated, all admin users for that tenant must be immediately locked out. Check tenant status on every request, not just at login.
- **Super Admin Impersonation Audit:** All actions performed by a super admin while viewing a tenant context must be clearly marked in audit logs as `performed_by=super_admin` to distinguish from actual tenant admin actions.
- **Concurrent Role Changes:** If a tenant_admin revokes another user's role while that user is actively logged in, the revocation should take effect on the next request (check permissions from DB, not just JWT cache).
- **JWT Expiry During Active Session:** Blazor Server circuits are long-lived. Implement JWT refresh within the circuit to prevent stale claims. Re-validate tenant_id on refresh.
- **Multiple Browser Tabs:** Tenant context (especially super admin's selected tenant) must be consistent across tabs. Use server-side circuit state, not client-side storage.
- **Tenant Schema Migration In Progress:** If a tenant's schema is being migrated, admin portal should show a maintenance page for that tenant rather than errors.

---

## Dependencies

**Prerequisite Stories:**
- STORY-055: White-Label Tenant Configuration — provides the tenant records, schema-per-tenant setup, and branding configuration that this story builds upon
- STORY-069: White-Label Branding Engine — provides the tenant branding (logos, colors) that the admin login page and portal chrome display

**Blocked Stories:**
- STORY-076: Pilot Deployment Preparation — requires tenant admin portal access for end-to-end testing and support team training

**External Dependencies:**
- TLS certificates for admin portal domain(s)
- DNS configuration for tenant-specific admin URLs (if using subdomain-based routing)
- Redis instance for session management and rate limiting

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
  - [ ] TenantScopedAuthorizationHandler correctly validates tenant claims
  - [ ] CrossTenantAccessDetector correctly identifies and blocks violations
  - [ ] Admin role permission checks enforced for all operations
  - [ ] JWT generation includes correct tenant_id and role claims
- [ ] Integration tests passing
  - [ ] End-to-end login flow for tenant admin produces correctly scoped JWT
  - [ ] Tenant admin queries return only data for their tenant
  - [ ] Cross-tenant request returns 403 and creates security log entry
  - [ ] Super admin can switch tenant context and see correct data
  - [ ] Account lockout activates after 5 failed login attempts
- [ ] Code reviewed and approved
- [ ] Documentation updated
  - [ ] Admin portal user guide for tenant administrators
  - [ ] Role and permission matrix documented
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Security review completed (cross-tenant isolation verified by second engineer)

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
