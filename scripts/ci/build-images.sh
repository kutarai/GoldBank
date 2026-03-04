#!/usr/bin/env bash
# =============================================================================
# UniBank - Build Docker Images
# Builds all service Docker images with proper tagging (commit SHA + latest).
#
# Usage:
#   ./scripts/ci/build-images.sh [--registry REGISTRY] [--tag TAG] [--push]
#
# Options:
#   --registry REGISTRY   Docker registry URL (default: from DOCKER_REGISTRY env)
#   --tag TAG             Image tag (default: git short SHA)
#   --push                Push images after building
#   --parallel            Build images in parallel (default: sequential)
#   --service NAME        Build only the specified service
#   --help                Show this help message
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Default settings
DOCKER_REGISTRY="${DOCKER_REGISTRY:-localhost:5000}"
IMAGE_PREFIX="${IMAGE_PREFIX:-unibank}"
DOCKER_TAG="${DOCKER_TAG:-$(git -C "${PROJECT_ROOT}" rev-parse --short HEAD 2>/dev/null || echo 'latest')}"
PUSH_IMAGES=false
PARALLEL_BUILD=false
SINGLE_SERVICE=""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Service definitions: name -> Dockerfile path
declare -A SERVICES=(
    ["gateway"]="server/UniBank.Gateway/Dockerfile"
    ["switching"]="switch/UniBank.Switching/Dockerfile"
    ["terminal-manager"]="terminal/UniBank.TerminalManager/Dockerfile"
    ["hsm"]="hsm/UniBank.HSM/Dockerfile"
    ["admin"]="admin/UniBank.Admin/Dockerfile"
    ["notifications"]="server/UniBank.Notifications/Dockerfile"
)

# ─── Parse Arguments ─────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
    case $1 in
        --registry)
            DOCKER_REGISTRY="$2"
            shift 2
            ;;
        --tag)
            DOCKER_TAG="$2"
            shift 2
            ;;
        --push)
            PUSH_IMAGES=true
            shift
            ;;
        --parallel)
            PARALLEL_BUILD=true
            shift
            ;;
        --service)
            SINGLE_SERVICE="$2"
            shift 2
            ;;
        --help)
            head -17 "$0" | tail -11
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

build_service() {
    local service_name="$1"
    local dockerfile="$2"
    local full_image="${DOCKER_REGISTRY}/${IMAGE_PREFIX}/${service_name}"

    log_info "Building ${service_name}..."
    log_info "  Dockerfile: ${dockerfile}"
    log_info "  Image:      ${full_image}:${DOCKER_TAG}"

    docker build \
        -t "${full_image}:${DOCKER_TAG}" \
        -t "${full_image}:latest" \
        -f "${dockerfile}" \
        --label "org.opencontainers.image.revision=${DOCKER_TAG}" \
        --label "org.opencontainers.image.source=unibank" \
        --label "org.opencontainers.image.title=${service_name}" \
        "${PROJECT_ROOT}"

    if [ $? -eq 0 ]; then
        log_success "Built ${service_name} successfully"
    else
        log_error "Failed to build ${service_name}"
        return 1
    fi

    if [ "${PUSH_IMAGES}" = true ]; then
        log_info "Pushing ${service_name}..."
        docker push "${full_image}:${DOCKER_TAG}"
        docker push "${full_image}:latest"
        log_success "Pushed ${service_name}"
    fi
}

# ─── Main ─────────────────────────────────────────────────────────────────────

cd "${PROJECT_ROOT}"

echo ""
log_info "=============================================="
log_info "UniBank Docker Image Builder"
log_info "=============================================="
log_info "Registry:  ${DOCKER_REGISTRY}"
log_info "Prefix:    ${IMAGE_PREFIX}"
log_info "Tag:       ${DOCKER_TAG}"
log_info "Push:      ${PUSH_IMAGES}"
log_info "Parallel:  ${PARALLEL_BUILD}"
echo ""

# Validate single service if specified
if [ -n "${SINGLE_SERVICE}" ]; then
    if [ -z "${SERVICES[${SINGLE_SERVICE}]+_}" ]; then
        log_error "Unknown service: ${SINGLE_SERVICE}"
        log_info "Available services: ${!SERVICES[*]}"
        exit 1
    fi
fi

EXIT_CODE=0
PIDS=()

if [ -n "${SINGLE_SERVICE}" ]; then
    # Build only the specified service
    build_service "${SINGLE_SERVICE}" "${SERVICES[${SINGLE_SERVICE}]}" || EXIT_CODE=1
elif [ "${PARALLEL_BUILD}" = true ]; then
    # Build all services in parallel
    log_info "Building all services in parallel..."
    for service_name in "${!SERVICES[@]}"; do
        dockerfile="${SERVICES[${service_name}]}"
        build_service "${service_name}" "${dockerfile}" &
        PIDS+=($!)
    done

    # Wait for all parallel builds
    for pid in "${PIDS[@]}"; do
        wait "${pid}" || EXIT_CODE=1
    done
else
    # Build all services sequentially
    log_info "Building all services sequentially..."
    for service_name in "${!SERVICES[@]}"; do
        dockerfile="${SERVICES[${service_name}]}"
        build_service "${service_name}" "${dockerfile}" || EXIT_CODE=1
    done
fi

# ─── Summary ──────────────────────────────────────────────────────────────────

echo ""
log_info "=============================================="
log_info "Build Summary"
log_info "=============================================="

if [ -n "${SINGLE_SERVICE}" ]; then
    log_info "  ${DOCKER_REGISTRY}/${IMAGE_PREFIX}/${SINGLE_SERVICE}:${DOCKER_TAG}"
else
    for service_name in "${!SERVICES[@]}"; do
        log_info "  ${DOCKER_REGISTRY}/${IMAGE_PREFIX}/${service_name}:${DOCKER_TAG}"
    done
fi

echo ""
if [ ${EXIT_CODE} -eq 0 ]; then
    log_success "All images built successfully!"
else
    log_error "Some images failed to build. Exit code: ${EXIT_CODE}"
fi

exit ${EXIT_CODE}
