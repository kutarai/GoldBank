#!/usr/bin/env bash
# =============================================================================
# GoldBank - Deploy to Staging
# Deploys all services to the staging environment via docker compose.
#
# Usage:
#   ./scripts/ci/deploy-staging.sh [--tag TAG] [--registry REGISTRY] [--dry-run]
#
# Options:
#   --tag TAG             Image tag to deploy (default: git short SHA)
#   --registry REGISTRY   Docker registry URL (default: from DOCKER_REGISTRY env)
#   --dry-run             Show what would be done without executing
#   --skip-pull           Skip pulling images (use locally built images)
#   --help                Show this help message
#
# Environment Variables:
#   DOCKER_REGISTRY       Docker registry URL
#   POSTGRES_USER         PostgreSQL username (default: goldbank)
#   POSTGRES_PASSWORD     PostgreSQL password (required in non-dry-run)
#   JWT_SECRET            JWT signing key (required in non-dry-run)
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Default settings
DOCKER_REGISTRY="${DOCKER_REGISTRY:-localhost:5000}"
IMAGE_PREFIX="${IMAGE_PREFIX:-goldbank}"
DOCKER_TAG="${DOCKER_TAG:-$(git -C "${PROJECT_ROOT}" rev-parse --short HEAD 2>/dev/null || echo 'latest')}"
DRY_RUN=false
SKIP_PULL=false
COMPOSE_FILE="${PROJECT_ROOT}/docker-compose.yml"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# ─── Parse Arguments ─────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
    case $1 in
        --tag)
            DOCKER_TAG="$2"
            shift 2
            ;;
        --registry)
            DOCKER_REGISTRY="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --skip-pull)
            SKIP_PULL=true
            shift
            ;;
        --help)
            head -20 "$0" | tail -14
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# ─── Helper Functions ─────────────────────────────────────────────────────────

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[PASS]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[FAIL]${NC} $1"
}

run_cmd() {
    if [ "${DRY_RUN}" = true ]; then
        echo -e "${YELLOW}[DRY-RUN]${NC} $*"
    else
        "$@"
    fi
}

# ─── Pre-flight Checks ───────────────────────────────────────────────────────

cd "${PROJECT_ROOT}"

echo ""
log_info "=============================================="
log_info "GoldBank Staging Deployment"
log_info "=============================================="
log_info "Registry:      ${DOCKER_REGISTRY}"
log_info "Image prefix:  ${IMAGE_PREFIX}"
log_info "Tag:           ${DOCKER_TAG}"
log_info "Compose file:  ${COMPOSE_FILE}"
log_info "Dry run:       ${DRY_RUN}"
echo ""

# Verify docker compose is available
if ! command -v docker &> /dev/null; then
    log_error "docker is not installed or not in PATH"
    exit 1
fi

if ! docker compose version &> /dev/null; then
    log_error "docker compose (v2) is required but not available"
    log_info "Install Docker Compose V2: https://docs.docker.com/compose/install/"
    exit 1
fi

# Verify compose file exists
if [ ! -f "${COMPOSE_FILE}" ]; then
    log_error "Compose file not found: ${COMPOSE_FILE}"
    exit 1
fi

# ─── Export Environment for Compose ───────────────────────────────────────────

export IMAGE_TAG="${DOCKER_TAG}"
export ASPNETCORE_ENVIRONMENT="Staging"

# Services and their images
declare -A SERVICES=(
    ["gateway"]="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/gateway:${DOCKER_TAG}"
    ["switching"]="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/switching:${DOCKER_TAG}"
    ["terminal-manager"]="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/terminal-manager:${DOCKER_TAG}"
    ["hsm"]="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/hsm:${DOCKER_TAG}"
    ["admin"]="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/admin:${DOCKER_TAG}"
    ["notifications"]="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/notifications:${DOCKER_TAG}"
)

# ─── Pull Images ──────────────────────────────────────────────────────────────

if [ "${SKIP_PULL}" = false ]; then
    log_info "Pulling service images..."
    for service_name in "${!SERVICES[@]}"; do
        image="${SERVICES[${service_name}]}"
        log_info "  Pulling ${image}..."
        run_cmd docker pull "${image}"
    done
    log_success "All images pulled"
else
    log_warn "Skipping image pull (--skip-pull specified)"
fi

# ─── Stop Existing Services ──────────────────────────────────────────────────

log_info "Stopping existing services..."
run_cmd docker compose -f "${COMPOSE_FILE}" --profile core down --timeout 30 || true
log_success "Existing services stopped"

# ─── Start Infrastructure ────────────────────────────────────────────────────

log_info "Starting infrastructure services (postgres, redis)..."
run_cmd docker compose -f "${COMPOSE_FILE}" --profile infra up -d

if [ "${DRY_RUN}" = false ]; then
    log_info "Waiting for infrastructure to be healthy..."
    # Wait for postgres
    RETRIES=30
    until docker compose -f "${COMPOSE_FILE}" ps postgres 2>/dev/null | grep -q "healthy" || [ ${RETRIES} -eq 0 ]; do
        log_info "  Waiting for PostgreSQL... (${RETRIES} retries remaining)"
        sleep 2
        RETRIES=$((RETRIES - 1))
    done

    if [ ${RETRIES} -eq 0 ]; then
        log_error "PostgreSQL did not become healthy in time"
        exit 1
    fi
    log_success "Infrastructure is healthy"
fi

# ─── Start Application Services ──────────────────────────────────────────────

log_info "Starting application services..."
run_cmd docker compose -f "${COMPOSE_FILE}" --profile core up -d
log_success "Application services started"

# ─── Health Check ─────────────────────────────────────────────────────────────

if [ "${DRY_RUN}" = false ]; then
    log_info "Running health checks..."
    sleep 10

    GATEWAY_PORT="${GATEWAY_PORT:-5000}"
    HEALTH_URL="http://localhost:${GATEWAY_PORT}/health"

    RETRIES=12
    until curl -sf "${HEALTH_URL}" > /dev/null 2>&1 || [ ${RETRIES} -eq 0 ]; do
        log_info "  Waiting for gateway health endpoint... (${RETRIES} retries remaining)"
        sleep 5
        RETRIES=$((RETRIES - 1))
    done

    if [ ${RETRIES} -eq 0 ]; then
        log_warn "Gateway health check did not pass within timeout"
        log_info "Services may still be starting. Check logs with:"
        log_info "  docker compose -f ${COMPOSE_FILE} --profile core logs"
    else
        log_success "Gateway health check passed"
    fi
fi

# ─── Summary ──────────────────────────────────────────────────────────────────

echo ""
log_info "=============================================="
log_info "Deployment Summary"
log_info "=============================================="
log_info "Tag deployed:  ${DOCKER_TAG}"

if [ "${DRY_RUN}" = false ]; then
    echo ""
    log_info "Running containers:"
    docker compose -f "${COMPOSE_FILE}" --profile infra --profile core ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null || \
        docker compose -f "${COMPOSE_FILE}" --profile infra --profile core ps
fi

echo ""
log_success "Staging deployment complete!"
log_info "Gateway:           http://localhost:${GATEWAY_PORT:-5000}"
log_info "Admin:             http://localhost:${ADMIN_PORT:-5010}"
log_info "Switching:         http://localhost:${SWITCHING_PORT:-5003}"
log_info "Terminal Manager:  http://localhost:${TERMINAL_MANAGER_PORT:-5004}"
log_info "HSM:               http://localhost:${HSM_PORT:-5005}"
log_info "Notifications:     http://localhost:${NOTIFICATIONS_PORT:-5007}"
