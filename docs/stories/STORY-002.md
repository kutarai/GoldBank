# STORY-002: Docker Compose Development Environment

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want a Docker Compose setup for local development,
So that I can run the full stack locally with one command.

---

## Description

### Background
GoldBank comprises multiple services (Gateway, Core Banking, Switching, Terminal Manager, HSM, Admin, Reporting, Notifications) along with infrastructure dependencies (PostgreSQL, Redis, monitoring stack). Without a containerized development environment, developers would need to manually install and configure each component, leading to "works on my machine" issues and wasted setup time.

This story provides a comprehensive Docker Compose configuration that allows any developer to spin up the entire GoldBank platform with a single `docker compose up` command. Docker Compose profiles allow selective startup of service groups (e.g., only infrastructure, or infrastructure + core services).

### Scope

**In scope:**
- `docker-compose.yml` with all services and infrastructure containers
- `docker-compose.override.yml` for development-specific overrides (debug ports, volume mounts)
- Dockerfiles for each .NET service (multi-stage builds)
- Docker Compose profiles for selective startup
- Docker networks for service isolation (frontend, backend, monitoring)
- Named volumes for data persistence across restarts
- Health check definitions for all containers
- Environment variable documentation in `.env.example`
- Container resource limits appropriate for local development

**Out of scope:**
- Production Docker orchestration (Kubernetes, Docker Swarm)
- Production-grade TLS certificate management
- Cloud-specific container registry configuration
- Load balancing configuration

### User Flow
1. Developer clones the repository
2. Developer copies `.env.example` to `.env` and adjusts values if needed
3. Developer runs `docker compose up -d` to start all services
4. Developer waits for health checks to pass (visible via `docker compose ps`)
5. Developer accesses services:
   - Gateway gRPC: `localhost:5000` (HTTP/2) / `localhost:5001` (HTTPS)
   - Admin UI: `localhost:5010`
   - PostgreSQL: `localhost:5432`
   - Redis: `localhost:6379`
   - Grafana: `localhost:3000`
   - Kibana: `localhost:5601`
   - Prometheus: `localhost:9090`
6. Developer uses profiles for selective startup:
   - `docker compose --profile infra up -d` (PostgreSQL, Redis only)
   - `docker compose --profile monitoring up -d` (Prometheus, Grafana, ELK)
   - `docker compose --profile core up -d` (Gateway, Core, Notifications)
   - `docker compose up -d` (everything)

---

## Acceptance Criteria

- [ ] `docker-compose.yml` defines all required services:
  - Infrastructure: `postgres` (18), `redis` (7+)
  - Application: `gateway`, `core`, `switching`, `terminal-manager`, `hsm`, `admin`, `reporting`, `notifications`
  - Monitoring: `prometheus`, `grafana`, `elasticsearch`, `kibana`
- [ ] `docker-compose.override.yml` provides development overrides (debug ports, source mounts)
- [ ] Each .NET service has a multi-stage `Dockerfile` (build + runtime)
- [ ] Health checks are defined for all services and infrastructure containers
- [ ] Environment variables are documented in `.env.example`
- [ ] `docker compose up -d` starts all services successfully
- [ ] `docker compose ps` shows all containers as healthy within 120 seconds
- [ ] Docker Compose profiles allow selective startup (`infra`, `core`, `monitoring`, `all`)
- [ ] Data persists across container restarts via named volumes (PostgreSQL, Redis, Elasticsearch)
- [ ] Three Docker networks are created: `frontend`, `backend`, `monitoring`
- [ ] PostgreSQL is accessible on `localhost:5432` with configured credentials
- [ ] Redis is accessible on `localhost:6379`
- [ ] Grafana is accessible on `localhost:3000`

---

## Technical Notes

### Components

**Docker Compose Services:**

| Service | Image/Build | Port(s) | Profile | Network(s) |
|---------|-------------|---------|---------|-------------|
| postgres | `postgres:18` | 5432:5432 | infra | backend |
| redis | `redis:7-alpine` | 6379:6379 | infra | backend |
| gateway | Build: `src/GoldBank.Gateway` | 5000:5000, 5001:5001 | core | frontend, backend |
| core | Build: `src/GoldBank.Core` | 5002:5002 | core | backend |
| switching | Build: `src/GoldBank.Switching` | 5003:5003 | core | backend |
| terminal-manager | Build: `src/GoldBank.TerminalManager` | 5004:5004, 1883:1883 | core | backend |
| hsm | Build: `src/GoldBank.HSM` | 5005:5005 | core | backend |
| admin | Build: `src/GoldBank.Admin` | 5010:5010 | core | frontend, backend |
| reporting | Build: `src/GoldBank.Reporting` | 5006:5006 | core | backend |
| notifications | Build: `src/GoldBank.Notifications` | 5007:5007 | core | backend |
| prometheus | `prom/prometheus:latest` | 9090:9090 | monitoring | monitoring, backend |
| grafana | `grafana/grafana:latest` | 3000:3000 | monitoring | monitoring, frontend |
| elasticsearch | `elasticsearch:8.x` | 9200:9200 | monitoring | monitoring, backend |
| kibana | `kibana:8.x` | 5601:5601 | monitoring | monitoring, frontend |

**Dockerfile Pattern (Multi-Stage):**
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/GoldBank.Gateway/GoldBank.Gateway.csproj", "src/GoldBank.Gateway/"]
COPY ["src/GoldBank.Protos/GoldBank.Protos.csproj", "src/GoldBank.Protos/"]
COPY ["src/GoldBank.SharedKernel/GoldBank.SharedKernel.csproj", "src/GoldBank.SharedKernel/"]
RUN dotnet restore "src/GoldBank.Gateway/GoldBank.Gateway.csproj"
COPY . .
RUN dotnet publish "src/GoldBank.Gateway/GoldBank.Gateway.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "GoldBank.Gateway.dll"]
```

### Docker Networks

```yaml
networks:
  frontend:
    driver: bridge
    name: goldbank-frontend
  backend:
    driver: bridge
    name: goldbank-backend
  monitoring:
    driver: bridge
    name: goldbank-monitoring
```

- **frontend**: Exposes services accessible by external clients (Gateway, Admin, Grafana, Kibana)
- **backend**: Internal service-to-service communication (all services, PostgreSQL, Redis)
- **monitoring**: Prometheus scraping and monitoring traffic

### Named Volumes

```yaml
volumes:
  postgres-data:
    name: goldbank-postgres-data
  redis-data:
    name: goldbank-redis-data
  elasticsearch-data:
    name: goldbank-elasticsearch-data
  grafana-data:
    name: goldbank-grafana-data
  prometheus-data:
    name: goldbank-prometheus-data
```

### Health Check Definitions

**PostgreSQL:**
```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
  interval: 10s
  timeout: 5s
  retries: 5
  start_period: 30s
```

**Redis:**
```yaml
healthcheck:
  test: ["CMD", "redis-cli", "ping"]
  interval: 10s
  timeout: 5s
  retries: 5
```

**Gateway / .NET Services:**
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
  interval: 15s
  timeout: 10s
  retries: 5
  start_period: 30s
```

### Environment Variables (.env.example)

```env
# PostgreSQL
POSTGRES_USER=goldbank
POSTGRES_PASSWORD=goldbank_dev_password
POSTGRES_DB=goldbank
POSTGRES_PORT=5432

# Redis
REDIS_PORT=6379
REDIS_PASSWORD=

# JWT
JWT_SECRET=dev-secret-key-change-in-production-min-32-chars
JWT_ISSUER=goldbank-dev
JWT_AUDIENCE=goldbank-api

# Services
GATEWAY_PORT=5000
GATEWAY_HTTPS_PORT=5001
ADMIN_PORT=5010
CORE_PORT=5002

# Monitoring
GRAFANA_ADMIN_PASSWORD=admin
ELASTICSEARCH_MEMORY=512m

# Logging
LOG_LEVEL=Debug
ASPNETCORE_ENVIRONMENT=Development
```

### API / gRPC Endpoints
Not directly applicable -- this story configures the container orchestration rather than endpoints.

### Database Changes
PostgreSQL 18 container is provisioned. The actual schema creation is handled in STORY-003. An initialization script volume mount point (`./docker/postgres/init/`) is provided for future use.

### Security Considerations
- Default credentials in `.env.example` are for development only; document that production must use secrets management
- Redis should be configured without password for local dev (simplicity) but documented for production
- Elasticsearch security (xpack) disabled for local development
- Do not commit `.env` file -- only `.env.example`
- Container images should be pinned to specific versions (not `latest`) for reproducibility

### Edge Cases
- Port conflicts: If a developer has PostgreSQL or Redis already running locally on default ports, they should be able to override via `.env`
- Docker Desktop memory: The full stack (especially with Elasticsearch) requires minimum 8 GB allocated to Docker; document this requirement
- First-time startup will be slow due to image pulls; subsequent starts use cache
- If a service container fails health check, dependent services should wait (using `depends_on` with `condition: service_healthy`)
- Volume permissions on Linux may differ from macOS/Windows; document `DOCKER_USER` variable if needed
- Elasticsearch on Linux requires `vm.max_map_count=262144`; document `sysctl` requirement

### Implementation Guidance

**docker-compose.override.yml pattern for development:**
```yaml
services:
  gateway:
    build:
      target: build  # Stop at build stage for debugging
    volumes:
      - ./src/GoldBank.Gateway:/src/src/GoldBank.Gateway  # Hot reload
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DOTNET_USE_POLLING_FILE_WATCHER=1
    ports:
      - "5000:5000"
      - "5001:5001"
```

**Service dependency ordering:**
```
postgres, redis (no dependencies)
  -> core (depends_on: postgres, redis)
  -> gateway (depends_on: core)
  -> switching (depends_on: core, postgres)
  -> terminal-manager (depends_on: postgres, redis)
  -> hsm (depends_on: postgres)
  -> admin (depends_on: gateway)
  -> reporting (depends_on: postgres)
  -> notifications (depends_on: core, redis)
  -> prometheus (depends_on: gateway, core)
  -> grafana (depends_on: prometheus)
  -> elasticsearch (no dependencies)
  -> kibana (depends_on: elasticsearch)
```

---

## Dependencies

**Prerequisite Stories:**
- STORY-001: Solution Scaffolding & Project Structure (projects must exist to build Docker images)

**Blocked Stories:**
- STORY-003: PostgreSQL Database Schema & Multi-Tenant Foundation (needs PostgreSQL container)
- STORY-006: CI/CD Pipeline Setup (needs Dockerfiles)
- STORY-008: Monitoring & Logging Stack (needs Prometheus, Grafana, ELK containers)

**External Dependencies:**
- Docker Engine 24+ and Docker Compose v2+
- Docker Hub access for pulling base images
- MCR (Microsoft Container Registry) access for .NET images
- Minimum 8 GB RAM allocated to Docker Desktop

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) -- N/A for Docker config
- [ ] Integration tests passing -- `docker compose up -d` starts all services
- [ ] Code reviewed and approved
- [ ] Documentation updated (`.env.example`, README updated with Docker commands)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
