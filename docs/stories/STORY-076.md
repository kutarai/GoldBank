# STORY-076: Pilot Deployment Preparation

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
I want to prepare for pilot deployment with first white-label institution
So that we can launch with a real customer

---

## Description

### Background

UniBank has completed development across seven sprints: user registration, KYC, account management, NFC/QR payments, P2P transfers, cash-in/cash-out, bill payments, admin portal, white-label configuration, and fraud detection. Sprint 8 is the launch sprint, and this story represents the culmination of all prior work: preparing the platform for its first real-world deployment with a pilot institution in Southern Africa.

The pilot deployment is the single most important milestone for the project. It transforms UniBank from a development project into a live financial services platform serving real customers. Every decision in this story carries real-world consequences: a failed deployment means lost trust with the pilot institution; a security gap means regulatory exposure; an untested recovery procedure means potential data loss.

The on-premise deployment model means the team must deliver a complete, self-contained deployment package that the pilot institution's infrastructure team can operate. Unlike SaaS deployments managed by the development team, the pilot institution will eventually own operations. This story ensures everything needed for that handover is prepared, tested, and documented.

### Scope

**In scope:**
- Production environment setup and configuration (Docker Compose production profile)
- PostgreSQL 18 production tuning and configuration
- Redis production configuration with persistence
- TLS certificate provisioning and configuration
- First tenant provisioning: schema, branding, admin users, fee/limit configuration
- End-to-end functional test of complete user journey
- Deployment runbook (`docs/runbook.md`)
- Monitoring verification (Grafana dashboards, alerting)
- Backup and restore verification
- Support team training on admin portal
- Go/no-go checklist for launch decision

**Out of scope:**
- Hardware procurement (deploying institution's responsibility)
- Network infrastructure setup (firewalls, load balancers, DNS — deploying institution's responsibility)
- Mobile app store submission (separate process)
- Marketing materials or customer onboarding content
- Long-term SLA negotiation
- Multi-site disaster recovery (future enhancement)
- Automated deployment pipeline (CI/CD) — manual deployment for pilot, automation to follow

### Deployment Preparation Steps

**Step 1: Production Environment Setup**
1. Create Docker Compose production profile (`docker-compose.production.yml`):
   - All services with production-appropriate resource limits (CPU, memory)
   - Health checks on all containers
   - Restart policies (`unless-stopped`)
   - Volume mounts for persistent data (PostgreSQL, Redis, logs)
   - TLS certificate volume mounts
   - Environment variables from `.env.production` (not committed to source control)
   - Log driver configuration (structured JSON to file + rotation)
2. PostgreSQL 18 production tuning:
   - `shared_buffers`: 25% of available RAM
   - `work_mem`: 64MB (adjust based on query complexity)
   - `maintenance_work_mem`: 512MB
   - `effective_cache_size`: 75% of available RAM
   - `max_connections`: 200 (sized for app pool + admin + monitoring + buffer)
   - `wal_level`: replica (for point-in-time recovery)
   - `archive_mode`: on (WAL archiving for PITR)
   - `max_wal_senders`: 3
   - `checkpoint_completion_target`: 0.9
   - `random_page_cost`: 1.1 (SSD storage assumed)
   - `log_min_duration_statement`: 100 (log slow queries >100ms)
   - `ssl`: on
   - `ssl_cert_file` / `ssl_key_file`: paths to TLS certificates
3. Redis production configuration:
   - `maxmemory`: sized for expected cache + session data
   - `maxmemory-policy`: `allkeys-lru`
   - `appendonly`: yes (AOF persistence)
   - `appendfsync`: everysec
   - `requirepass`: strong password from environment variable
   - TLS enabled for client connections
4. TLS certificate provisioning:
   - Server certificates for gRPC endpoints, admin portal, PostgreSQL, Redis
   - Client certificates for mTLS on internal service-to-service calls
   - Certificate chain including intermediate CA
   - Certificate renewal procedure documented

**Step 2: First Tenant Provisioning**
1. Create tenant record in `admin.tenants` table:
   - Tenant name, slug, status = active
   - Contact information for pilot institution
2. Create tenant schema via migration system:
   - All tenant-scoped tables created in `tenant_{slug}` schema
   - Seed reference data (transaction types, fee types, KYC tiers, etc.)
3. Configure branding:
   - Upload pilot institution's logo, favicon
   - Set primary/secondary colors, fonts
   - Configure SMS sender name
   - Set institution display name for receipts and notifications
4. Create admin users for pilot institution:
   - At least one tenant_admin account
   - Accounts for operations, support, and finance teams as needed
   - MFA enabled for all admin accounts
5. Configure fees and limits:
   - Transaction fees per type (NFC payment, QR payment, P2P transfer, cash-in, cash-out, bill payment)
   - Daily/monthly transaction limits per KYC tier
   - Merchant settlement fees and schedule
   - Agent commission rates
6. Configure fraud rules:
   - Review default thresholds with pilot institution's compliance team
   - Adjust thresholds per institution's risk appetite
   - Enable/disable rules per institution preference

**Step 3: End-to-End Functional Test**

Complete user journey test on staging (mirrors production):

| Step | Action | Expected Result |
|---|---|---|
| 1 | Register new user (phone + OTP + PIN) | Account created, KYC tier 0 |
| 2 | Submit KYC documents (ID + selfie) | KYC submitted for review |
| 3 | Admin approves KYC | Account upgraded to tier 1 |
| 4 | Activate account | Account active, wallet balance = 0 |
| 5 | Agent cash-in (deposit ZAR 500) | Wallet balance = 500, agent float reduced |
| 6 | NFC payment at merchant (ZAR 50) | Wallet = 450, merchant settlement updated |
| 7 | QR payment at merchant (ZAR 30) | Wallet = 420, merchant settlement updated |
| 8 | P2P transfer to another user (ZAR 100) | Sender = 320, recipient = 100 |
| 9 | Cash-out at agent (ZAR 50) | Wallet = 270 (minus any fees), agent float increased |
| 10 | Bill payment — electricity (ZAR 100) | Wallet = 170 (minus any fees), bill payment confirmed |
| 11 | View transaction history | All 6 transactions visible with correct details |
| 12 | View balance in admin portal | Admin sees correct balance for test user |
| 13 | View transactions in admin portal | Admin sees all transactions for test user |
| 14 | Generate fraud alert (trigger velocity rule) | Alert appears in admin fraud queue |
| 15 | Admin resolves fraud alert | Alert status updated, audit log created |
| 16 | Admin views dashboard metrics | Dashboard shows correct transaction counts and volumes |

**Step 4: Deployment Runbook**

Create `docs/runbook.md` covering:

1. **Pre-Deployment Checklist:**
   - Hardware meets minimum specifications
   - Network configuration complete (ports, firewalls, DNS)
   - TLS certificates provisioned and valid
   - `.env.production` configured with all required variables
   - PostgreSQL backup location configured
   - Monitoring endpoints accessible

2. **Deployment Steps:**
   - Pull Docker images from registry (or load from tarball for air-gapped)
   - Configure environment variables
   - Start PostgreSQL container, verify connectivity
   - Run database migrations
   - Start Redis container, verify connectivity
   - Start application containers (Core Banking, Satellite Services)
   - Start admin portal container
   - Run health checks on all services
   - Provision first tenant (if not done)
   - Run smoke test suite

3. **Rollback Procedure:**
   - Stop application containers
   - Restore PostgreSQL from backup (point-in-time if needed)
   - Restart previous version containers (keep previous image tags)
   - Verify system state after rollback
   - Document rollback reason and timeline

4. **Health Check Commands:**
   ```bash
   # Check all container status
   docker compose -f docker-compose.production.yml ps

   # Check application health endpoints
   curl -k https://localhost:5001/health
   curl -k https://localhost:5002/health

   # Check PostgreSQL connectivity
   docker compose exec postgres pg_isready -U unibank

   # Check Redis connectivity
   docker compose exec redis redis-cli -a $REDIS_PASSWORD ping

   # Check gRPC service health
   grpcurl -insecure localhost:5001 grpc.health.v1.Health/Check

   # Check recent logs for errors
   docker compose logs --tail=100 --since=1h | grep -i error
   ```

5. **Incident Response Process:**
   - Severity classification (P1-P4)
   - Escalation contacts and procedures
   - Communication templates for deploying institution
   - Post-incident review process

6. **Common Troubleshooting:**
   - Container fails to start: check logs, verify environment variables
   - Database connection errors: check pg_hba.conf, SSL configuration, connection pool
   - Redis connection errors: check password, TLS, maxclients
   - High latency: check database slow query log, connection pool, Redis memory
   - Authentication failures: check JWT signing key, certificate expiry, clock sync

**Step 5: Monitoring Verification**
1. Verify all Grafana dashboards are operational:
   - System Overview: CPU, memory, disk, network across all containers
   - Application Metrics: request rate, latency percentiles, error rate
   - Database Metrics: connections, query duration, cache hit ratio, replication lag
   - Redis Metrics: memory usage, hit rate, connected clients
   - Business Metrics: registrations, transactions by type, daily volume
   - Fraud Dashboard: alert count, severity distribution, response time
2. Verify alerting rules configured and tested:
   - Container down: alert within 1 minute
   - Error rate >1%: alert within 5 minutes
   - p95 latency >2s for payments: alert within 5 minutes
   - Database connections >80%: alert within 5 minutes
   - Disk usage >80%: alert within 15 minutes
   - Certificate expiry <30 days: daily alert
3. Test alert delivery: email, SMS, or webhook to on-call team

**Step 6: Backup Verification**
1. Configure PostgreSQL backup schedule:
   - Full backup: daily at 02:00 local time (pg_dump or pg_basebackup)
   - WAL archiving: continuous for point-in-time recovery
   - Backup retention: 30 days for daily, 7 days for WAL
2. Test backup and restore:
   - Take full backup of staging database
   - Restore to a separate instance
   - Verify data integrity: row counts, checksums on key tables
   - Verify application connects and functions correctly against restored database
   - Document restore time (RTO target: <1 hour)
3. Configure Redis backup:
   - AOF persistence enabled
   - RDB snapshots every 15 minutes
   - Backup RDB file to separate storage daily

**Step 7: Support Team Training**
1. Admin portal walkthrough for pilot institution's team:
   - Login and navigation
   - Dashboard interpretation
   - Customer lookup and support workflows
   - Transaction investigation
   - Fraud alert management
   - User account management (suspend, reactivate, KYC review)
   - Merchant onboarding and management
   - Agent management
   - Report generation
2. Common support scenarios and resolution:
   - Customer locked out (PIN reset procedure)
   - Failed transaction investigation
   - Disputed transaction process
   - KYC document re-submission
   - Merchant terminal troubleshooting
3. Escalation procedures:
   - When to contact UniBank development team
   - Communication channels and SLA expectations

**Go/No-Go Checklist:**

| Category | Item | Status |
|---|---|---|
| **Functional** | All acceptance criteria for Sprint 1-8 stories validated | [ ] |
| **Functional** | End-to-end test passed on staging | [ ] |
| **Performance** | All NFR targets met (STORY-074) | [ ] |
| **Security** | Security audit passed, no critical/high findings open (STORY-075) | [ ] |
| **Security** | PCI-DSS self-assessment completed | [ ] |
| **Infrastructure** | Production environment configured and tested | [ ] |
| **Infrastructure** | TLS certificates provisioned and valid | [ ] |
| **Infrastructure** | Backup and restore verified | [ ] |
| **Infrastructure** | Monitoring and alerting operational | [ ] |
| **Tenant** | First tenant provisioned with branding, fees, limits | [ ] |
| **Tenant** | Tenant admin users created with MFA enabled | [ ] |
| **Tenant** | Fraud rules configured and reviewed with compliance team | [ ] |
| **Documentation** | Deployment runbook complete and reviewed | [ ] |
| **Documentation** | Support team training completed | [ ] |
| **Operational** | On-call rotation defined for launch period | [ ] |
| **Operational** | Incident response process documented and tested | [ ] |
| **Operational** | Rollback procedure tested | [ ] |
| **Sign-off** | Tech lead sign-off | [ ] |
| **Sign-off** | Product owner sign-off | [ ] |
| **Sign-off** | Pilot institution sign-off | [ ] |

---

## Acceptance Criteria

- [ ] Staging environment mirrors production configuration (Docker Compose production profile, PostgreSQL production tuning, Redis persistence, TLS)
- [ ] First tenant fully configured with branding (logo, colors), admin users (MFA enabled), fee schedule, transaction limits, and fraud rules
- [ ] Merchant onboarding tested end-to-end: merchant registered, terminal configured, payment processed, settlement verified
- [ ] End-to-end functional test passes all 16 steps covering the complete user journey from registration through all payment types
- [ ] Deployment runbook documented in `docs/runbook.md` covering: deployment steps, rollback procedure, health check commands, incident response, and common troubleshooting
- [ ] All Grafana dashboards operational and displaying correct metrics for staging environment
- [ ] Alerting rules configured, tested, and verified to deliver notifications to the on-call team
- [ ] PostgreSQL backup and restore tested: full backup taken, restored to separate instance, data integrity verified, application verified against restored database
- [ ] Support team trained on admin portal operations, common support scenarios, and escalation procedures
- [ ] Go/no-go checklist completed with all items addressed (pass or documented risk acceptance)

---

## Technical Notes

### Components

This story touches deployment and operational concerns across the entire system:

- **Docker Configuration:** `docker/`
  - `docker-compose.production.yml` — production deployment profile
  - `Dockerfile` (per service) — verified for production security (non-root, minimal base image)
  - `.env.production.template` — template with all required environment variables (no secrets)
- **Database:** `src/Infrastructure/Database/`
  - `Migrations/` — all migrations verified for production readiness
  - `Scripts/seed-tenant.sql` — tenant provisioning script
  - `Scripts/seed-reference-data.sql` — reference data for new tenant schemas
- **Monitoring:** `deploy/monitoring/`
  - `prometheus.yml` — Prometheus scrape configuration
  - `grafana/dashboards/` — all dashboard JSON definitions
  - `grafana/alerts/` — alerting rule definitions
  - `grafana/datasources/` — datasource configuration
- **Documentation:** `docs/`
  - `runbook.md` — deployment and operations runbook
  - `admin-portal-training.md` — support team training guide
  - `go-no-go-checklist.md` — launch decision checklist

### API / gRPC Endpoints

No new endpoints. This story verifies all existing endpoints function correctly in the production-like environment:

| Verification | Endpoints | Method |
|---|---|---|
| Health checks | All services `/health` | Automated health check script |
| gRPC connectivity | All gRPC services | `grpcurl` smoke tests |
| Admin portal access | HTTPS admin portal URL | Browser-based verification |
| TLS verification | All exposed ports | `nmap` TLS scan |

### Database Changes

**Tenant Provisioning Script** (`src/Infrastructure/Database/Scripts/seed-tenant.sql`):

```sql
-- Step 1: Create tenant record
INSERT INTO admin.tenants (id, name, slug, status, contact_email, created_at)
VALUES (
    gen_random_uuid(),
    'Pilot Institution Name',
    'pilot_inst',
    'active',
    'admin@pilot-institution.co.za',
    NOW()
);

-- Step 2: Create tenant schema
CREATE SCHEMA IF NOT EXISTS tenant_pilot_inst;

-- Step 3: Run tenant schema migrations (via EF Core migration system)
-- dotnet ef database update --context TenantDbContext -- --tenant-id {tenant_id}

-- Step 4: Seed reference data
INSERT INTO tenant_pilot_inst.transaction_types (id, code, name, category) VALUES
    (gen_random_uuid(), 'NFC_PAYMENT', 'NFC Payment', 'payment'),
    (gen_random_uuid(), 'QR_PAYMENT', 'QR Payment', 'payment'),
    (gen_random_uuid(), 'P2P_TRANSFER', 'P2P Transfer', 'transfer'),
    (gen_random_uuid(), 'CASH_IN', 'Cash In', 'deposit'),
    (gen_random_uuid(), 'CASH_OUT', 'Cash Out', 'withdrawal'),
    (gen_random_uuid(), 'BILL_PAYMENT', 'Bill Payment', 'payment');

-- Step 5: Configure fees
INSERT INTO tenant_pilot_inst.fee_configurations (id, transaction_type, fee_type, amount, percentage, min_fee, max_fee, currency, enabled) VALUES
    (gen_random_uuid(), 'NFC_PAYMENT', 'flat', 0.00, 0.00, 0.00, 0.00, 'ZAR', true),
    (gen_random_uuid(), 'QR_PAYMENT', 'flat', 0.00, 0.00, 0.00, 0.00, 'ZAR', true),
    (gen_random_uuid(), 'P2P_TRANSFER', 'percentage', 0.00, 1.50, 1.00, 50.00, 'ZAR', true),
    (gen_random_uuid(), 'CASH_IN', 'flat', 0.00, 0.00, 0.00, 0.00, 'ZAR', true),
    (gen_random_uuid(), 'CASH_OUT', 'tiered', 0.00, 0.00, 0.00, 0.00, 'ZAR', true),
    (gen_random_uuid(), 'BILL_PAYMENT', 'flat', 5.00, 0.00, 5.00, 5.00, 'ZAR', true);

-- Step 6: Configure transaction limits per KYC tier
INSERT INTO tenant_pilot_inst.transaction_limits (id, kyc_tier, transaction_type, daily_limit, monthly_limit, per_transaction_limit, currency) VALUES
    (gen_random_uuid(), 0, 'ALL', 500.00, 5000.00, 200.00, 'ZAR'),
    (gen_random_uuid(), 1, 'ALL', 5000.00, 25000.00, 2000.00, 'ZAR'),
    (gen_random_uuid(), 2, 'ALL', 25000.00, 100000.00, 10000.00, 'ZAR');

-- Step 7: Create initial admin user (password set via application, not SQL)
-- Admin user created via application CLI or admin portal super admin interface
```

**PostgreSQL Production Configuration** (`deploy/postgresql/postgresql.conf`):

```ini
# Connection Settings
listen_addresses = '*'
port = 5432
max_connections = 200

# Memory Settings (adjust for actual hardware)
shared_buffers = 4GB           # 25% of 16GB RAM
work_mem = 64MB
maintenance_work_mem = 512MB
effective_cache_size = 12GB    # 75% of 16GB RAM

# WAL Settings
wal_level = replica
archive_mode = on
archive_command = 'cp %p /var/lib/postgresql/wal_archive/%f'
max_wal_senders = 3
wal_keep_size = 1GB

# Checkpoint Settings
checkpoint_completion_target = 0.9
max_wal_size = 2GB
min_wal_size = 1GB

# Query Planner
random_page_cost = 1.1
effective_io_concurrency = 200

# Logging
log_min_duration_statement = 100
log_line_prefix = '%m [%p] %q%u@%d '
log_statement = 'ddl'
log_connections = on
log_disconnections = on

# SSL
ssl = on
ssl_cert_file = '/var/lib/postgresql/certs/server.crt'
ssl_key_file = '/var/lib/postgresql/certs/server.key'
ssl_ca_file = '/var/lib/postgresql/certs/ca.crt'
ssl_min_protocol_version = 'TLSv1.3'

# Locale
lc_messages = 'en_US.UTF-8'
lc_monetary = 'en_US.UTF-8'
lc_numeric = 'en_US.UTF-8'
lc_time = 'en_US.UTF-8'
```

### Security Considerations

- **Production Secrets Management:** All production secrets (database passwords, Redis passwords, JWT signing keys, HSM credentials) must be managed via environment variables loaded from a secure source, never stored in source control or Docker images.
- **`.env.production` Security:** The actual `.env.production` file must never be committed. Only `.env.production.template` (with placeholder values) should be in source control.
- **TLS Certificate Security:** Private keys must have restricted file permissions (600) and be accessible only by the service user.
- **Docker Security:** All containers must run as non-root users. No containers should have `privileged` mode. Network policies should restrict inter-container communication to only necessary paths.
- **Backup Encryption:** Database backups must be encrypted at rest. If stored on network storage, encryption in transit required.
- **First Tenant Admin Credentials:** Initial admin passwords must be generated securely (minimum 16 characters, cryptographically random) and communicated to the pilot institution via a secure channel (not email). Force password change on first login.
- **Audit Trail:** All provisioning actions (tenant creation, admin user creation, configuration changes) must be logged in the admin audit trail.

### Edge Cases

- **Partial Deployment Failure:** If one container fails to start during deployment, the runbook must specify which services can operate independently and which require all dependencies. Include a dependency diagram.
- **Tenant Schema Migration Failure:** If schema creation fails mid-migration, the runbook must include steps to clean up the partial schema and retry. EF Core migrations should be idempotent.
- **Certificate Expiry During Launch:** TLS certificates should have at least 90 days validity at launch. Monitoring alert for <30 days to expiry. Document emergency certificate renewal procedure.
- **Backup Restore to Different Hardware:** Document any hardware-specific configuration in the backup restore procedure (e.g., shared_buffers adjustment for different RAM sizes).
- **Network Partition Between Services:** Document expected behavior when Redis is temporarily unavailable (graceful degradation vs. service outage) and when PostgreSQL is unavailable.
- **Time Zone Configuration:** All servers must use UTC. Document NTP configuration requirements. Application logs, database timestamps, and monitoring must all use UTC.
- **Disk Space Exhaustion:** Document disk usage monitoring thresholds and actions. WAL archiving can consume significant disk space if archive_command fails silently.
- **First User Registration Race Condition:** During launch, many users may attempt to register simultaneously. Verify the registration flow handles concurrent phone number registrations correctly (unique constraint, idempotent OTP generation).

---

## Dependencies

**Prerequisite Stories:**
- ALL stories from Sprints 1-8 must be complete, including:
  - STORY-071: Per-Tenant Admin Portal Access — tenant admin login and portal required for training
  - STORY-072: Fraud Detection Alerts — fraud rules must be configurable for tenant
  - STORY-074: Performance Testing & NFR Validation — performance must be validated before launch
  - STORY-075: Security Audit & Hardening — security must be verified before launch

**Blocked Stories:**
- None — this is the final story. Successful completion of this story and its go/no-go checklist triggers the pilot launch.

**External Dependencies:**
- Pilot institution identified and engaged (contract, contacts, branding assets)
- Production hardware provisioned by pilot institution
- Network infrastructure configured by pilot institution (DNS, firewalls, load balancer if applicable)
- TLS certificates issued by trusted CA (or institution's internal CA)
- HSM hardware provisioned and accessible from application servers
- Pilot institution's compliance team available for fraud rule review
- Pilot institution's support team available for training sessions
- Pilot institution's IT team available for deployment coordination

---

## Definition of Done

- [ ] Code implemented and committed
  - [ ] Docker Compose production profile complete and tested
  - [ ] Tenant provisioning scripts complete and tested
  - [ ] Health check and smoke test scripts complete
  - [ ] Monitoring dashboards and alert rules committed
- [ ] Unit tests written and passing (>=80% coverage)
  - [ ] Tenant provisioning service tests verify schema creation and seeding
  - [ ] Health check endpoint tests verify correct status reporting
- [ ] Integration tests passing
  - [ ] End-to-end functional test (16 steps) passes on staging
  - [ ] Backup and restore verified with data integrity check
  - [ ] All monitoring dashboards display correct data
  - [ ] All alerting rules fire correctly when thresholds breached
- [ ] Code reviewed and approved
  - [ ] Docker Compose production profile reviewed for security
  - [ ] PostgreSQL configuration reviewed for performance and security
  - [ ] Runbook reviewed by second engineer
- [ ] Documentation updated
  - [ ] `docs/runbook.md` complete with all sections
  - [ ] Training materials prepared for pilot institution support team
  - [ ] Go/no-go checklist completed
- [ ] Acceptance criteria validated
  - [ ] All go/no-go checklist items addressed
  - [ ] Pilot institution sign-off obtained
- [ ] Deployed to staging
  - [ ] Staging environment running with production profile
  - [ ] First tenant provisioned and functional on staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
