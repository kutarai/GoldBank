# STORY-075: Security Audit & Hardening

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
I want a security audit before launch
So that vulnerabilities are identified and fixed

---

## Description

### Background

GoldBank processes financial transactions for the unbanked population in Southern Africa, handling sensitive data including PINs, phone numbers, national ID numbers, account balances, and transaction histories. Before pilot deployment, a comprehensive security audit is mandatory to ensure the platform meets PCI-DSS v4.0 requirements, protects customer data, and withstands common attack vectors.

The on-premise deployment model means GoldBank's security perimeter is the deploying institution's infrastructure. Unlike cloud providers that handle many infrastructure security concerns, the GoldBank platform must ensure its own security from the application layer through to data-at-rest encryption. The first deploying institution's compliance team will require evidence of a security audit as a condition of going live.

This story covers both automated security scanning and manual verification of security controls across all system layers: transport security, authentication, authorization, input validation, data protection, cryptographic controls, logging, and PCI-DSS self-assessment.

### Scope

**In scope:**
- TLS/mTLS verification on all communication channels
- Certificate management documentation
- PII masking audit across all log statements
- SQL injection testing and parameterized query verification
- Authentication security testing (JWT manipulation, expiry, cross-tenant)
- Authorization testing (role-based access control bypass attempts)
- Rate limiting verification
- Input validation testing (boundary values, oversized payloads, malformed protobuf)
- HSM key management verification
- OWASP ZAP automated security scan
- PCI-DSS v4.0 Self-Assessment Questionnaire (SAQ) for relevant sections
- Security findings documentation with severity classification and remediation tracking
- Critical and high findings remediated before launch

**Out of scope:**
- External penetration testing by third-party firm (recommended post-launch)
- Physical security audit of on-premise infrastructure
- Mobile application security testing (MASVS/OWASP Mobile — separate engagement)
- Social engineering testing
- DDoS resilience testing (infrastructure-level concern for deploying institution)
- Full PCI-DSS QSA assessment (requires qualified security assessor)

### Audit Checklist

**1. Transport Security (TLS/mTLS)**
1. Verify TLS 1.3 enforced on all external-facing endpoints:
   - gRPC endpoints (mobile client connections)
   - Admin portal (Blazor Server / HTTPS)
   - Any REST endpoints (if applicable)
2. Verify mTLS on all internal service-to-service gRPC calls:
   - Core Banking module to Satellite Services
   - Wolverine message bus connections (if over network)
3. Verify TLS certificate validity, chain of trust, and expiry dates
4. Verify certificate pinning configuration documented for mobile clients
5. Verify no fallback to TLS 1.2 or earlier protocols
6. Verify HSTS headers present on all HTTPS responses
7. Test with `nmap --script ssl-enum-ciphers` to confirm only strong cipher suites
8. Document certificate management: issuance, renewal, revocation procedures

**2. PII Masking in Logs**
1. Search all log statements in codebase for potential PII exposure:
   - Phone numbers: must show at most last 4 digits (`****1234`)
   - Account numbers: must be fully masked or show last 4 (`****5678`)
   - PIN data: must NEVER appear in any log, even masked
   - National ID numbers: must be fully masked
   - Card numbers (PAN): must show at most first 6 and last 4 (BIN + last 4)
   - CVV/CVC: must NEVER appear in any log
   - Names: acceptable in admin audit logs, not in application debug logs
2. Verify structured logging (Serilog) destructure policies mask sensitive fields
3. Verify gRPC interceptor logs do not dump request/response bodies containing PII
4. Verify Wolverine message handler logs do not expose message payloads with PII
5. Verify PostgreSQL query logs (if enabled) do not expose parameterized values containing PII
6. Check Redis key naming does not embed PII (e.g., no `session:{phone_number}` patterns)

**3. SQL Injection Prevention**
1. Verify all database queries use EF Core parameterized queries:
   - Search for `FromSqlRaw`, `ExecuteSqlRaw`, `FromSqlInterpolated` — document and verify each
   - Any raw SQL must use parameterized queries, never string concatenation
2. Run OWASP ZAP automated scan against gRPC gateway (if HTTP transcoding exists) and admin portal
3. Test stored procedures (if any) for parameter injection
4. Verify no dynamic table/column name construction from user input (tenant schema selection must use allow-list, not user input)

**4. Authentication Security**
1. Test expired JWT acceptance — system must reject
2. Test tampered JWT (modified claims, invalid signature) — system must reject
3. Test JWT with missing required claims (tenant_id, sub, role) — system must reject
4. Test cross-tenant JWT (valid JWT from Tenant A used against Tenant B data) — must return 403
5. Test JWT with elevated role claim (modified from user to admin) — must reject (signature invalid)
6. Verify JWT signing uses RS256 or ES256 (asymmetric), not HS256 (symmetric shared secret)
7. Verify JWT expiry is reasonable (15-30 minutes for access token, longer for refresh)
8. Verify refresh token rotation (old refresh token invalidated on use)
9. Test PIN brute force protection: account locks after N failed attempts
10. Verify PIN is never transmitted in plaintext (encrypted PIN block from device to HSM)
11. Test session fixation: new session ID issued after authentication
12. Verify logout invalidates JWT (token blacklist in Redis or short expiry + no refresh)

**5. Authorization Security**
1. For each role (user, merchant, agent, tenant_admin, tenant_operations, tenant_support, tenant_finance, super_admin):
   - Test accessing endpoints restricted to other roles
   - Verify 403 Forbidden returned (not 404 or 500)
2. Test horizontal privilege escalation:
   - User A accessing User B's account balance
   - User A viewing User B's transaction history
   - Merchant A accessing Merchant B's settlement data
3. Test vertical privilege escalation:
   - Regular user accessing admin portal endpoints
   - tenant_support accessing tenant_admin-only operations
4. Verify all gRPC endpoints have authorization metadata/policies defined
5. Test unauthenticated access to all endpoints — verify 401 Unauthorized

**6. Rate Limiting**
1. Verify rate limits configured and enforced:
   - Authentication endpoint: 5 attempts per 15 min per phone number
   - Registration endpoint: 3 attempts per hour per IP
   - Payment endpoints: 60 requests per minute per account
   - Admin login: 5 attempts per 15 min per email
   - OTP request: 3 per 10 min per phone number
2. Verify rate limit responses include `Retry-After` header
3. Verify Redis-backed counters with appropriate TTL
4. Test rate limit bypass attempts (header manipulation, IP spoofing via X-Forwarded-For)
5. Verify rate limits are per-tenant where appropriate

**7. Input Validation**
1. Test boundary values on all numeric inputs:
   - Payment amount: 0, negative, max value, decimal precision beyond 2 places
   - Account balance: overflow scenarios
   - Phone number: too short, too long, non-numeric characters, international formats
2. Test oversized payloads:
   - gRPC message exceeding configured max size
   - Admin portal form submissions with extremely large text fields
3. Test malformed protobuf messages:
   - Missing required fields
   - Wrong field types
   - Unknown fields (should be ignored per protobuf spec)
4. Test Unicode handling:
   - Names with special characters, emojis, RTL text
   - SQL injection via Unicode normalization
5. Verify all inputs validated at API boundary (proto validation) AND at business logic layer

**8. HSM & Cryptographic Controls**
1. Verify PIN encryption keys never leave HSM boundary
2. Verify PIN blocks encrypted in transit (device to HSM)
3. Verify PIN verification happens inside HSM (PIN block comparison, not plaintext)
4. Verify key rotation procedures documented and tested
5. Verify no cryptographic secrets in source code, configuration files, or environment variables (use HSM or vault)
6. Verify encryption at rest for PostgreSQL (TDE or filesystem encryption)
7. Verify Redis data encrypted at rest (if contains sensitive session data)
8. Verify backup encryption configured and tested

**9. PCI-DSS v4.0 Self-Assessment**
Relevant SAQ sections for GoldBank's scope:

| Requirement | Area | Assessment Items |
|---|---|---|
| 1 | Network Security | Firewall rules, network segmentation, DMZ configuration |
| 2 | Secure Configuration | Default passwords changed, unnecessary services disabled |
| 3 | Stored Data Protection | PAN storage (if applicable), encryption at rest, key management |
| 4 | Encryption in Transit | TLS 1.3, no weak ciphers, certificate management |
| 5 | Malware Protection | Anti-malware on servers, vulnerability scanning |
| 6 | Secure Development | SDLC practices, code review, change management |
| 7 | Access Control | Role-based access, need-to-know, least privilege |
| 8 | Authentication | Unique IDs, MFA for admin, password/PIN policies |
| 9 | Physical Security | Server room access controls (deploying institution responsibility) |
| 10 | Logging & Monitoring | Audit trails, log integrity, monitoring alerts |
| 11 | Security Testing | Vulnerability scans, penetration testing schedule |
| 12 | Security Policies | Information security policy, incident response plan |

**10. Additional Checks**
1. Verify CORS policy on admin portal (if applicable) restricts origins
2. Verify Content Security Policy (CSP) headers on admin portal
3. Verify no sensitive data in URL parameters (everything in body or headers)
4. Verify error messages do not leak stack traces or internal details to clients
5. Verify health check endpoints do not expose sensitive system information
6. Verify Docker images use minimal base images, run as non-root, no unnecessary packages
7. Verify dependency vulnerability scan (dotnet list package --vulnerable) shows no critical CVEs

---

## Acceptance Criteria

- [ ] TLS 1.3 verified on all external endpoints (gRPC, admin portal); mTLS verified on all internal service-to-service calls
- [ ] PII masking verified across all log statements — no phone numbers, PINs, PANs, or national IDs appear unmasked in any log output
- [ ] SQL injection testing confirms all queries use parameterized access; OWASP ZAP scan completed with no high-severity findings
- [ ] Authentication bypass testing confirms: expired JWTs rejected, tampered JWTs rejected, missing-claim JWTs rejected, cross-tenant JWTs return 403
- [ ] Authorization testing confirms each role can only access permitted endpoints; horizontal and vertical privilege escalation blocked
- [ ] Rate limiting verified and enforced on authentication, registration, payment, and OTP endpoints
- [ ] Input validation testing confirms boundary values, oversized payloads, and malformed protobuf handled gracefully
- [ ] HSM verification confirms PIN keys never leave HSM boundary and PIN blocks encrypted in transit
- [ ] PCI-DSS v4.0 self-assessment questionnaire completed for all relevant sections
- [ ] All critical and high severity findings remediated before pilot launch
- [ ] Medium and low findings documented with remediation timeline
- [ ] Security audit report documented in `docs/security-audit-report.md` with finding severity, description, and remediation status

---

## Technical Notes

### Components

This story touches all modules in the system as it is an audit of the entire platform:

- **All gRPC Services:** `src/Modules/*/Grpc/` — TLS configuration, authorization policies
- **Identity Module:** `src/Modules/Identity/` — JWT issuance, authentication, PIN handling
- **Core Banking Module:** `src/Modules/CoreBanking/` — payment processing, account management
- **Admin Portal:** `src/AdminPortal/` — Blazor Server security configuration, CSRF, CSP
- **Infrastructure:** `src/Infrastructure/`
  - `Security/TlsConfiguration.cs` — TLS/mTLS setup
  - `Security/RateLimitingMiddleware.cs` — rate limit enforcement
  - `Logging/PiiMaskingPolicy.cs` — Serilog destructure policy for PII
  - `Security/JwtValidationMiddleware.cs` — JWT validation pipeline
- **Docker Configuration:** `docker/` — Dockerfile security, compose security settings
- **Deployment Configuration:** `deploy/` — TLS certificates, environment configuration

### Security Scanning Tools

| Tool | Purpose | Usage |
|---|---|---|
| OWASP ZAP | Automated web security scan | Run against admin portal; active + passive scan |
| `nmap --script ssl-enum-ciphers` | TLS cipher verification | Run against all exposed ports |
| `dotnet list package --vulnerable` | .NET dependency CVE scan | Run against all project files |
| `trivy` | Docker image vulnerability scan | Run against all built images |
| `grpcurl` | gRPC endpoint testing | Manual auth/authz testing |
| Custom scripts | JWT manipulation testing | Generate tampered/expired tokens |
| Custom scripts | Rate limit testing | Rapid-fire requests to verify enforcement |

### API / gRPC Endpoints

No new endpoints. This story audits all existing endpoints:

| Endpoint Category | Security Tests |
|---|---|
| Authentication (Login, Register, OTP) | Rate limiting, brute force, input validation |
| Payment (NFC, QR, P2P) | Authorization, cross-tenant, input validation, amount boundaries |
| Account (Balance, History, Profile) | Authorization, horizontal privilege escalation, PII in responses |
| Admin Portal | CSRF, session management, tenant isolation, role enforcement |
| Health/Metrics | Information disclosure, authentication requirement |

### Database Changes

No schema changes. Audit actions include:

- Verify all EF Core entities have global query filters for tenant_id
- Verify no raw SQL queries exist without parameterization
- Verify PostgreSQL connection strings use SSL mode
- Verify database user permissions follow least privilege principle
- Verify pg_hba.conf restricts connection sources

### Security Considerations

This entire story IS security. Key implementation notes:

- **Findings Classification:**
  - **Critical:** Actively exploitable vulnerability that could lead to data breach, financial loss, or complete system compromise. Must fix before launch.
  - **High:** Vulnerability that could be exploited with moderate effort. Must fix before launch.
  - **Medium:** Vulnerability that requires specific conditions to exploit. Must have remediation plan with timeline before launch.
  - **Low:** Minor security concern or best practice deviation. Document and address in next sprint.

- **Remediation Process:**
  1. Finding documented with reproduction steps
  2. Severity assigned
  3. Fix implemented (for critical/high)
  4. Fix verified by different team member
  5. Regression test added to prevent recurrence

- **PCI-DSS Compliance Notes:**
  - GoldBank likely falls under SAQ D (service provider) since it processes and stores cardholder data on behalf of deploying institutions
  - The self-assessment in this story is an internal readiness check, not a formal QSA assessment
  - Deploying institutions may need their own PCI-DSS certification depending on their role
  - HSM usage for PIN processing satisfies PCI PIN Security Requirements

### Edge Cases

- **Legacy Code Paths:** Audit must cover all code paths, including error handling paths that may bypass security checks (e.g., exception handlers that return raw error details).
- **Configuration Drift:** Security configuration in staging must match production. Document all security-relevant configuration parameters and verify they are set correctly in both environments.
- **Third-Party Dependencies:** Audit extends to third-party NuGet packages. Any package with known CVEs must be updated or risk-accepted with documentation.
- **Timing Attacks:** Verify that authentication comparison (PIN, password) uses constant-time comparison to prevent timing-based side channels.
- **Log Injection:** Verify that user-controlled input in log messages is sanitized to prevent log injection attacks (newline injection, log forging).
- **Deserialization Attacks:** Verify that protobuf deserialization does not allow arbitrary type instantiation. gRPC/protobuf is generally safe but custom deserializers may not be.
- **SSRF via Admin Portal:** If admin portal makes any server-side HTTP requests based on user input (e.g., webhook URLs), verify SSRF protection.
- **Race Conditions in Authorization:** Verify that time-of-check-to-time-of-use (TOCTOU) vulnerabilities do not exist in authorization checks (e.g., role checked, then role changed, then action performed).

---

## Dependencies

**Prerequisite Stories:**
- All functional stories from Sprints 1-7 must be complete — the audit covers the entire codebase
- STORY-071: Per-Tenant Admin Portal Access — tenant isolation is a primary audit focus
- STORY-072: Fraud Detection Alerts — fraud detection system reviewed for security

**Blocked Stories:**
- STORY-076: Pilot Deployment Preparation — security audit pass is a go/no-go criterion for launch; critical/high findings must be remediated

**External Dependencies:**
- OWASP ZAP installed and configured on a workstation with network access to staging
- `nmap` and `trivy` available for scanning
- HSM test environment accessible for key management verification
- TLS certificates provisioned for staging environment

---

## Definition of Done

- [ ] Code implemented and committed
  - [ ] Any remediation code for critical/high findings implemented and committed
  - [ ] Automated security test scripts committed for regression testing
- [ ] Unit tests written and passing (>=80% coverage)
  - [ ] PII masking policy tests confirm all sensitive data types are masked
  - [ ] JWT validation tests confirm all attack vectors are blocked
  - [ ] Rate limiting tests confirm enforcement at configured thresholds
- [ ] Integration tests passing
  - [ ] Cross-tenant access test returns 403 and logs security event
  - [ ] Expired/tampered JWT test returns 401
  - [ ] Role-based access tests confirm permission boundaries
  - [ ] Rate limit integration test confirms blocking after threshold
- [ ] Code reviewed and approved
  - [ ] Security audit findings reviewed by tech lead
  - [ ] Remediation code reviewed by second engineer
- [ ] Documentation updated
  - [ ] `docs/security-audit-report.md` complete with all findings, severities, and remediation status
  - [ ] PCI-DSS self-assessment questionnaire completed
  - [ ] Certificate management procedures documented
  - [ ] Security configuration parameters documented
- [ ] Acceptance criteria validated
  - [ ] All critical and high findings remediated and verified
  - [ ] No unresolved critical or high findings remain
- [ ] Deployed to staging
  - [ ] Remediation fixes deployed and re-verified on staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
