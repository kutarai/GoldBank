#!/usr/bin/env bash
# =============================================================================
# UniBank - Start All Services with Podman
#
# Usage:
#   ./scripts/podman-up.sh              # Start infra + core + monitoring
#   ./scripts/podman-up.sh infra        # Start only infrastructure (postgres, redis)
#   ./scripts/podman-up.sh core         # Start infra + application services
#   ./scripts/podman-up.sh monitoring   # Start only monitoring (prometheus, grafana)
#   ./scripts/podman-up.sh --build      # Rebuild images before starting
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

NETWORK_NAME="synergy-net"
BUILD_FLAG=""
PROFILES=()

# ─── Parse Arguments ─────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
    case $1 in
        infra)
            PROFILES+=("infra")
            shift
            ;;
        core)
            PROFILES+=("infra" "core")
            shift
            ;;
        monitoring)
            PROFILES+=("monitoring")
            shift
            ;;
        --build)
            BUILD_FLAG="--build"
            shift
            ;;
        --help|-h)
            head -12 "$0" | tail -8
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Default: start everything
if [ ${#PROFILES[@]} -eq 0 ]; then
    PROFILES=("infra" "core" "monitoring")
fi

# Deduplicate profiles
PROFILES=($(printf '%s\n' "${PROFILES[@]}" | sort -u))

# ─── Pre-flight Checks ──────────────────────────────────────────────────────

if ! command -v podman &>/dev/null; then
    echo -e "${RED}podman is not installed. Install it first:${NC}"
    echo "  https://podman.io/docs/installation"
    exit 1
fi

if ! command -v podman-compose &>/dev/null && ! podman compose version &>/dev/null 2>&1; then
    echo -e "${RED}Neither podman-compose nor 'podman compose' plugin found.${NC}"
    echo "  Install podman-compose: pip install podman-compose"
    echo "  Or install podman compose plugin"
    exit 1
fi

# Determine compose command
if podman compose version &>/dev/null 2>&1; then
    COMPOSE_CMD="podman compose"
else
    COMPOSE_CMD="podman-compose"
fi

# ─── Create Network ─────────────────────────────────────────────────────────

if ! podman network exists "${NETWORK_NAME}" 2>/dev/null; then
    echo -e "${BLUE}[INFO]${NC} Creating network: ${NETWORK_NAME}"
    podman network create "${NETWORK_NAME}"
    echo -e "${GREEN}[PASS]${NC} Network ${NETWORK_NAME} created"
else
    echo -e "${BLUE}[INFO]${NC} Network ${NETWORK_NAME} already exists"
fi

# ─── Start Services ─────────────────────────────────────────────────────────

cd "${PROJECT_ROOT}"

PROFILE_ARGS=""
for p in "${PROFILES[@]}"; do
    PROFILE_ARGS="${PROFILE_ARGS} --profile ${p}"
done

echo ""
echo -e "${BLUE}[INFO]${NC} =============================================="
echo -e "${BLUE}[INFO]${NC} UniBank — Starting with Podman"
echo -e "${BLUE}[INFO]${NC} =============================================="
echo -e "${BLUE}[INFO]${NC} Profiles:  ${PROFILES[*]}"
echo -e "${BLUE}[INFO]${NC} Network:   ${NETWORK_NAME}"
echo -e "${BLUE}[INFO]${NC} Compose:   ${COMPOSE_CMD}"
echo ""

${COMPOSE_CMD} ${PROFILE_ARGS} up -d ${BUILD_FLAG}

# ─── Summary ─────────────────────────────────────────────────────────────────

echo ""
echo -e "${GREEN}[PASS]${NC} UniBank services started."
echo ""

# Show running containers
podman ps --filter "label=com.docker.compose.project=unibank" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" 2>/dev/null \
    || podman ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}" | grep -E "unibank|NAMES"

echo ""

# Print access URLs from .env
source "${PROJECT_ROOT}/.env" 2>/dev/null || true

for p in "${PROFILES[@]}"; do
    case $p in
        infra)
            echo -e "${BLUE}Infrastructure:${NC}"
            echo "  PostgreSQL:  localhost:${POSTGRES_PORT:-5432}"
            echo "  Redis:       localhost:${REDIS_PORT:-6379}"
            ;;
        core)
            echo -e "${BLUE}Application:${NC}"
            echo "  Gateway gRPC:      localhost:${GATEWAY_GRPC_PORT:-5000}"
            echo "  Gateway HTTP:      localhost:${GATEWAY_HTTP_PORT:-5001}"
            echo "  Admin Portal:      localhost:${ADMIN_PORT:-5010}"
            echo "  Switch gRPC:       localhost:${SWITCH_GRPC_PORT:-3333}"
            echo "  Switch Dashboard:  http://localhost:${SWITCH_WEB_PORT:-8080}"
            ;;
        monitoring)
            echo -e "${BLUE}Monitoring:${NC}"
            echo "  Prometheus:  http://localhost:${PROMETHEUS_PORT:-9190}"
            echo "  Grafana:     http://localhost:${GRAFANA_PORT:-3100}  (admin / ${GRAFANA_ADMIN_PASSWORD:-admin})"
            ;;
    esac
done
echo ""
