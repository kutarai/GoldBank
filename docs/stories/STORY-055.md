# STORY-055: Admin Portal Foundation & RBAC

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As an admin
I want a back-office portal with role-based access
So that team members can manage the platform with appropriate permissions

---

## Description

### Background
UniBank requires a centralised back-office portal that allows internal staff and tenant administrators to manage the platform. The portal must enforce strict role-based access control (RBAC) to ensure that each team member can only access functionality appropriate to their role. This is the foundational story for the entire Admin Portal epic -- every subsequent admin story depends on the authentication, authorisation, layout, and gRPC connectivity established here.

The portal is implemented as a Blazor Server application. Blazor Server is the correct choice because the admin portal is an internal tool where latency to the server is low, and it allows us to keep sensitive logic server-side while still delivering a rich interactive UI. Communication with the Core Banking engine is via gRPC-Web (required for browser compatibility since browsers cannot use HTTP/2 trailers directly).

### Scope
**In scope:**
- Blazor Server project scaffolding (`UniBank.Admin`)
- JWT-based authentication with cookie-based session management
- Admin user table and seed data for initial super_admin account
- Role definitions: `super_admin`, `operations`, `support`, `finance`, `compliance`, `tenant_admin`
- RBAC middleware using `[Authorize(Roles = "...")]` on Razor pages/components
- Role-filtered sidebar navigation
- Tenant-scoped data access for `tenant_admin` role
- gRPC-Web client configuration via `Grpc.Net.Client.Web`
- Responsive layout: sidebar navigation, top bar with user info/logout, main content area
- Login page with brute-force protection
- Session timeout and idle logout

**Out of scope:**
- Self-service admin user registration (super_admin creates accounts)
- Two-factor authentication (planned for later sprint)
- Mobile-responsive admin layout (desktop-first for back-office)
- Admin API for external consumption

### User Flow
1. Admin navigates to the admin portal URL (e.g., `https://admin.unibank.co.za`)
2. If not authenticated, the login page is presented
3. Admin enters username and password
4. System validates credentials against `admin_users` table (bcrypt password hash)
5. On success, a JWT is issued and stored in an HTTP-only secure cookie; the Blazor Server circuit is established
6. The sidebar navigation renders menu items filtered by the admin's role
7. If the admin is a `tenant_admin`, all data queries are automatically scoped to their `tenant_id`
8. Admin interacts with portal pages; each page enforces `[Authorize]` attributes
9. gRPC-Web calls are made from the Blazor Server backend to Core Banking services
10. After idle timeout (configurable, default 30 minutes), the session expires and the admin is redirected to login

---

## Acceptance Criteria

- [ ] Blazor Server application is created as `UniBank.Admin` project within the solution
- [ ] Admin users can log in with username/email and password
- [ ] Authentication uses JWT tokens stored in HTTP-only secure cookies
- [ ] Six roles are supported: `super_admin`, `operations`, `support`, `finance`, `compliance`, `tenant_admin`
- [ ] Pages are protected with `[Authorize(Roles = "...")]` attributes matching role requirements
- [ ] Sidebar navigation displays only menu items the current user's role permits
- [ ] `tenant_admin` users see only data belonging to their assigned tenant (tenant_id filter enforced on all queries)
- [ ] `super_admin` users can see data across all tenants and switch tenant context
- [ ] gRPC-Web connectivity to Core Banking services is established and functional
- [ ] Layout includes sidebar navigation, top bar with user info and logout, and main content area
- [ ] Login page includes brute-force protection (account lockout after 5 failed attempts for 15 minutes)
- [ ] Session expires after configurable idle timeout (default 30 minutes)
- [ ] Initial super_admin account is seeded via database migration
- [ ] All authentication events (login, logout, failed attempt, lockout) are audit logged

---

## Technical Notes

### Components
- **Project:** `src/UniBank.Admin/` -- Blazor Server application
- **Pages:**
  - `Pages/Login.razor` -- authentication page
  - `Pages/Dashboard.razor` -- landing page after login
  - `Pages/Admin/AdminUsers.razor` -- manage admin users (super_admin only)
- **Shared Components:**
  - `Shared/MainLayout.razor` -- sidebar + top bar + content area layout
  - `Shared/NavMenu.razor` -- role-filtered navigation menu
  - `Shared/TopBar.razor` -- user info, tenant selector (super_admin), logout
- **Services:**
  - `Services/AuthService.cs` -- login, token generation, session management
  - `Services/AdminUserService.cs` -- CRUD for admin users
  - `Services/TenantContextService.cs` -- resolves current tenant scope from user claims
  - `Services/GrpcChannelFactory.cs` -- creates gRPC-Web channels to Core Banking
- **Middleware:**
  - `Middleware/RbacMiddleware.cs` -- role validation
  - `Middleware/TenantScopeMiddleware.cs` -- injects tenant_id into request context

### API / gRPC Endpoints
```protobuf
service AdminAuthService {
  rpc Login (LoginRequest) returns (LoginResponse);
  rpc Logout (LogoutRequest) returns (LogoutResponse);
  rpc RefreshSession (RefreshRequest) returns (RefreshResponse);
  rpc GetCurrentUser (GetCurrentUserRequest) returns (AdminUserResponse);
}

service AdminUserManagementService {
  rpc CreateAdminUser (CreateAdminUserRequest) returns (AdminUserResponse);
  rpc UpdateAdminUser (UpdateAdminUserRequest) returns (AdminUserResponse);
  rpc DeactivateAdminUser (DeactivateAdminUserRequest) returns (AdminUserResponse);
  rpc ListAdminUsers (ListAdminUsersRequest) returns (ListAdminUsersResponse);
}

message LoginRequest {
  string username = 1;
  string password = 2;
}

message LoginResponse {
  string token = 1;
  AdminUserResponse user = 2;
  bool requires_password_change = 3;
}

message AdminUserResponse {
  string id = 1;
  string username = 2;
  string email = 3;
  string role = 4;
  string tenant_id = 5; // empty for super_admin
  string status = 6;
  google.protobuf.Timestamp created_at = 7;
  google.protobuf.Timestamp last_login_at = 8;
}
```

### Database Changes
```sql
-- Schema: admin (shared schema, not tenant-scoped)
CREATE TABLE admin.admin_users (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username        VARCHAR(50) NOT NULL UNIQUE,
    email           VARCHAR(255) NOT NULL UNIQUE,
    password_hash   VARCHAR(255) NOT NULL,
    role            VARCHAR(20) NOT NULL CHECK (role IN (
                        'super_admin', 'operations', 'support',
                        'finance', 'compliance', 'tenant_admin'
                    )),
    tenant_id       UUID REFERENCES tenants(id),  -- NULL for super_admin
    status          VARCHAR(20) NOT NULL DEFAULT 'active'
                        CHECK (status IN ('active', 'inactive', 'locked')),
    failed_login_count INT NOT NULL DEFAULT 0,
    locked_until    TIMESTAMPTZ,
    last_login_at   TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_tenant_admin_has_tenant
        CHECK (role != 'tenant_admin' OR tenant_id IS NOT NULL),
    CONSTRAINT chk_super_admin_no_tenant
        CHECK (role != 'super_admin' OR tenant_id IS NULL)
);

CREATE INDEX idx_admin_users_username ON admin.admin_users(username);
CREATE INDEX idx_admin_users_email ON admin.admin_users(email);
CREATE INDEX idx_admin_users_tenant ON admin.admin_users(tenant_id) WHERE tenant_id IS NOT NULL;

CREATE TABLE admin.admin_audit_log (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_user_id   UUID REFERENCES admin.admin_users(id),
    action          VARCHAR(50) NOT NULL, -- login, logout, failed_login, lockout, create_user, etc.
    target_type     VARCHAR(50),          -- admin_user, customer, merchant, etc.
    target_id       UUID,
    details         JSONB,
    ip_address      INET,
    user_agent      VARCHAR(500),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_admin_audit_log_user ON admin.admin_audit_log(admin_user_id);
CREATE INDEX idx_admin_audit_log_action ON admin.admin_audit_log(action);
CREATE INDEX idx_admin_audit_log_created ON admin.admin_audit_log(created_at);

-- Seed initial super_admin (password: to be changed on first login)
INSERT INTO admin.admin_users (username, email, password_hash, role, status)
VALUES ('superadmin', 'admin@unibank.co.za',
        '$2a$12$...', -- bcrypt hash of initial password
        'super_admin', 'active');
```

### Security Considerations
- Passwords hashed with bcrypt (cost factor 12)
- JWT tokens short-lived (15 minutes), refreshed via sliding session
- HTTP-only, Secure, SameSite=Strict cookies prevent XSS/CSRF token theft
- Brute-force protection: 5 failed attempts locks account for 15 minutes
- All admin actions audit-logged with IP address and user agent
- Tenant isolation enforced at query level -- `tenant_admin` cannot access other tenant data
- `super_admin` role cannot be assigned via the UI; requires direct database access or CLI
- Content Security Policy (CSP) headers to mitigate XSS
- gRPC-Web calls use mTLS between Admin portal and Core Banking

### Edge Cases
- Concurrent sessions: allow configurable max concurrent sessions per admin user (default 1)
- Session invalidation: when admin user is deactivated, all active sessions must be terminated immediately
- Tenant deletion: `tenant_admin` users for a deleted tenant must be automatically deactivated
- Clock skew: JWT validation allows 30-second clock skew tolerance
- Blazor Server circuit disconnect: handle graceful reconnection with re-authentication if session expired
- Browser tab duplication: ensure session state is consistent across multiple Blazor circuits for the same user

---

## Dependencies

**Prerequisite Stories:** STORY-005 (Multi-Tenant Foundation)
**Blocked Stories:** STORY-056, STORY-057, STORY-058, STORY-059, STORY-060, STORY-061, STORY-062, STORY-063, STORY-064, STORY-065, STORY-066, STORY-067 (all Sprint 7 admin and reporting stories)
**External Dependencies:**
- `Grpc.Net.Client.Web` NuGet package for gRPC-Web browser compatibility
- `BCrypt.Net-Next` for password hashing
- `Microsoft.AspNetCore.Authentication.JwtBearer` for JWT handling

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
