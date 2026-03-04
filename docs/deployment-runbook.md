# UniBank White-Label Platform - Deployment Runbook

## Overview
This runbook covers the deployment of the UniBank white-label banking platform for pilot institutions. It includes prerequisites, environment setup, tenant onboarding, rollback procedures, incident response, and health check verification.

---

## 1. Prerequisites

### Infrastructure Requirements
- **Docker**: v24.0+ with Docker Compose v2.20+
- **.NET 10 SDK**: Required for building and running the application
- **PostgreSQL**: v16+ with logical replication support
- **Redis**: v7+ for caching, rate limiting, and session management
- **MQTT Broker**: Mosquitto v2+ or EMQX for real-time notifications
- **HSM**: Hardware Security Module for cryptographic operations (Thales Luna or SoftHSM for development)

### Network Requirements
- TLS 1.2+ certificates for all public-facing endpoints
- Internal network connectivity between services on Docker overlay network
- DNS records configured for the tenant's custom domain

### Access Requirements
- SSH access to deployment servers
- Docker registry credentials
- PostgreSQL superuser credentials for initial schema setup
- Redis AUTH password

---

## 2. Environment Setup

### 2.1 Clone and Build
```bash
git clone <repository-url> unibank-whitelabel
cd unibank-whitelabel

# Build all services
/path/to/dotnet build UniBank.slnx --configuration Release
```

### 2.2 Configure Environment Variables
Create `.env` file in the project root:
```env
# Database
POSTGRES_HOST=db
POSTGRES_PORT=5432
POSTGRES_DB=unibank
POSTGRES_USER=unibank_app
POSTGRES_PASSWORD=<secure-password>

# Redis
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=<secure-password>

# MQTT
MQTT_HOST=mqtt
MQTT_PORT=1883
MQTT_USERNAME=unibank
MQTT_PASSWORD=<secure-password>

# HSM
HSM_ENDPOINT=http://hsm:8080
HSM_SLOT_ID=0

# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://+:5001;http://+:5000

# JWT
JWT_SECRET=<256-bit-secret>
JWT_ISSUER=unibank
JWT_AUDIENCE=unibank-mobile
JWT_EXPIRY_MINUTES=30
```

### 2.3 Start Infrastructure Services
```bash
docker compose up -d db redis mqtt
```

### 2.4 Initialize Database
```bash
# Create the main database
docker compose exec db psql -U postgres -c "CREATE DATABASE unibank;"

# Run migrations (if using EF Core migrations)
dotnet ef database update --project server/UniBank.Core
```

### 2.5 Start Application Services
```bash
docker compose up -d
```

### 2.6 Verify Deployment
```bash
# Health check
curl -k https://localhost:5001/health

# Expected response: {"status": "Healthy", ...}
```

---

## 3. First Tenant Onboarding

### 3.1 Create Tenant Schema
```sql
-- Connect to the database
INSERT INTO public.tenants (id, name, code, schema_name, country_code, currency_code, timezone, status, is_active, created_at)
VALUES (
  gen_random_uuid(),
  'First National Bank',
  'fnb_zw',
  'tenant_fnb_zw',
  'ZWE',
  'ZWG',
  'Africa/Harare',
  'active',
  true,
  NOW()
);

-- Create the tenant schema
CREATE SCHEMA IF NOT EXISTS tenant_fnb_zw;
```

### 3.2 Run Tenant Migrations
```bash
# Apply EF Core migrations to the new tenant schema
dotnet ef database update --project server/UniBank.Core -- --tenant-schema tenant_fnb_zw
```

### 3.3 Configure Tenant-Specific Settings
```sql
-- Set tenant-specific configuration
INSERT INTO tenant_fnb_zw.system_configs (id, key, value_json, tenant_id, created_at)
VALUES
  (gen_random_uuid(), 'transfer.daily_limit', '{"amount": "10000", "currency": "ZWG"}', 'fnb_zw', NOW()),
  (gen_random_uuid(), 'transfer.fee_percentage', '0.01', 'fnb_zw', NOW()),
  (gen_random_uuid(), 'kyc.auto_approve_level', '1', 'fnb_zw', NOW());
```

### 3.4 Create Initial Admin User
```sql
INSERT INTO tenant_fnb_zw.admin_users (id, username, password_hash, full_name, role, tenant_id, status, created_at)
VALUES (
  gen_random_uuid(),
  'admin@fnb.co.zw',
  '<bcrypt-hash>',
  'FNB Administrator',
  'tenant_admin',
  'fnb_zw',
  'active',
  NOW()
);
```

### 3.5 Verify Tenant Access
```bash
# Test tenant-specific gRPC endpoint
grpcurl -d '{"phone": "+263771234567", "country_code": "+263", "tenant_id": "fnb_zw"}' \
  localhost:5001 unibank.v1.account.AccountService/Register
```

---

## 4. Rollback Procedures

### 4.1 Application Rollback
```bash
# Stop current version
docker compose down

# Switch to previous image tag
export UNIBANK_VERSION=<previous-version>
docker compose up -d

# Verify health
curl -k https://localhost:5001/health
```

### 4.2 Database Rollback
```bash
# Revert last migration
dotnet ef database update <previous-migration-name> --project server/UniBank.Core

# If data corruption, restore from backup
pg_restore -U postgres -d unibank /backups/unibank_<timestamp>.dump
```

### 4.3 Configuration Rollback
```bash
# Restore environment variables
cp .env.backup .env
docker compose up -d --force-recreate
```

---

## 5. Incident Response Procedures

### 5.1 Severity Levels
| Level | Description | Response Time | Examples |
|-------|-------------|---------------|----------|
| P1 - Critical | Complete service outage | 15 minutes | Database down, all transactions failing |
| P2 - High | Major feature unavailable | 30 minutes | Payments failing, auth broken |
| P3 - Medium | Degraded performance | 2 hours | Slow response times, intermittent errors |
| P4 - Low | Minor issue | Next business day | UI glitch, non-critical log errors |

### 5.2 Incident Response Steps
1. **Detect**: Monitor health check endpoint and alerting system
2. **Assess**: Check logs, determine severity, identify affected tenants
3. **Communicate**: Notify stakeholders per severity level
4. **Mitigate**: Apply temporary fix or rollback if necessary
5. **Resolve**: Deploy permanent fix
6. **Review**: Conduct post-incident review within 48 hours

### 5.3 Key Log Locations
```bash
# Application logs
docker compose logs unibank-core --tail 100

# Database logs
docker compose logs db --tail 100

# Redis logs
docker compose logs redis --tail 100
```

### 5.4 Emergency Contacts
- **On-call Engineer**: See PagerDuty rotation
- **Database Admin**: See PagerDuty rotation
- **Security Team**: security@unibank.com

---

## 6. Health Check Endpoints

### 6.1 Application Health
```
GET /health
```

Returns comprehensive health status including:
- PostgreSQL database connectivity
- Redis cache connectivity
- MQTT broker status
- HSM service status
- gRPC channel health

### 6.2 Expected Response
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.150",
  "checks": [
    {
      "component": "PostgreSQL Database",
      "status": "Healthy",
      "duration": "00:00:00.045",
      "details": "Connected successfully in 45ms."
    },
    {
      "component": "Redis Cache",
      "status": "Healthy",
      "duration": "00:00:00.012",
      "details": "Ping response: 1.2ms. Connected endpoints: 1."
    },
    {
      "component": "MQTT Broker",
      "status": "Healthy",
      "details": "MQTT broker connectivity is managed by the notification service."
    },
    {
      "component": "HSM Service",
      "status": "Healthy",
      "details": "HSM service connectivity is managed at the infrastructure level."
    },
    {
      "component": "gRPC Channels",
      "status": "Healthy",
      "details": "gRPC services are registered and listening."
    }
  ],
  "timestamp": "2026-02-24T12:00:00Z",
  "version": "0.1.0",
  "environment": "Production"
}
```

### 6.3 Monitoring Integration
Configure your monitoring system to poll `/health` every 30 seconds:
```yaml
# Prometheus scrape config
scrape_configs:
  - job_name: 'unibank-health'
    scrape_interval: 30s
    metrics_path: '/metrics'
    static_configs:
      - targets: ['unibank-core:5000']
```

---

## 7. Pre-Launch Checklist

- [ ] All infrastructure services running and healthy
- [ ] TLS certificates installed and valid
- [ ] Database backups configured and tested
- [ ] Tenant schema created and migrated
- [ ] Admin user created with correct role
- [ ] Rate limiting configured and verified
- [ ] Security headers verified (check with securityheaders.com)
- [ ] PII masking verified in log output
- [ ] Fraud detection rules configured for tenant
- [ ] Monitoring and alerting configured
- [ ] Incident response contacts updated
- [ ] Load testing completed against NFR baselines
- [ ] Security audit passed all checks
