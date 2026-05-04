#!/usr/bin/env bash
# =============================================================================
# GoldBank - Stop All Services with Podman
#
# Usage:
#   ./scripts/podman-down.sh            # Stop all services, keep volumes
#   ./scripts/podman-down.sh --volumes  # Stop all services and remove volumes
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m'

VOLUME_FLAG=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --volumes|-v)
            VOLUME_FLAG="--volumes"
            shift
            ;;
        --help|-h)
            head -9 "$0" | tail -5
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Determine compose command
if podman compose version &>/dev/null 2>&1; then
    COMPOSE_CMD="podman compose"
else
    COMPOSE_CMD="podman-compose"
fi

cd "${PROJECT_ROOT}"

echo -e "${BLUE}[INFO]${NC} Stopping GoldBank services..."

${COMPOSE_CMD} \
    --profile infra \
    --profile core \
    --profile monitoring \
    down ${VOLUME_FLAG}

echo -e "${GREEN}[PASS]${NC} All GoldBank services stopped."

if [ -n "${VOLUME_FLAG}" ]; then
    echo -e "${BLUE}[INFO]${NC} Volumes removed."
fi
