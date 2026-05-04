#!/usr/bin/env bash
# =============================================================================
# GoldBank - Run Tests with Coverage
# Runs unit and integration tests, generates coverage reports, and enforces
# the minimum coverage threshold.
#
# Usage:
#   ./scripts/ci/run-tests.sh [--unit-only | --integration-only] [--threshold N]
#
# Options:
#   --unit-only          Run only unit tests
#   --integration-only   Run only integration tests
#   --threshold N        Minimum coverage percentage (default: 80)
#   --help               Show this help message
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

# Default settings
RUN_UNIT=true
RUN_INTEGRATION=true
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-80}"
CONFIGURATION="Release"
SOLUTION_FILE="GoldBank.slnx"
TEST_RESULTS_DIR="${PROJECT_ROOT}/test-results"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# ─── Parse Arguments ─────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
    case $1 in
        --unit-only)
            RUN_UNIT=true
            RUN_INTEGRATION=false
            shift
            ;;
        --integration-only)
            RUN_UNIT=false
            RUN_INTEGRATION=true
            shift
            ;;
        --threshold)
            COVERAGE_THRESHOLD="$2"
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

# ─── Setup ────────────────────────────────────────────────────────────────────

cd "${PROJECT_ROOT}"

log_info "GoldBank Test Runner"
log_info "Solution: ${SOLUTION_FILE}"
log_info "Configuration: ${CONFIGURATION}"
log_info "Coverage threshold: ${COVERAGE_THRESHOLD}%"
echo ""

# Clean previous test results
rm -rf "${TEST_RESULTS_DIR}"
mkdir -p "${TEST_RESULTS_DIR}"

EXIT_CODE=0

# ─── Unit Tests ───────────────────────────────────────────────────────────────

if [ "${RUN_UNIT}" = true ]; then
    log_info "Running unit tests..."

    dotnet test tests/GoldBank.Tests/GoldBank.Tests.csproj \
        --configuration "${CONFIGURATION}" \
        --logger "junit;LogFilePath=${TEST_RESULTS_DIR}/unit-tests.xml" \
        --collect:"XPlat Code Coverage" \
        --results-directory "${TEST_RESULTS_DIR}/unit-coverage" \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
        || EXIT_CODE=$?

    if [ ${EXIT_CODE} -ne 0 ]; then
        log_error "Unit tests failed with exit code ${EXIT_CODE}"
    else
        log_success "Unit tests passed"
    fi
fi

# ─── Integration Tests ───────────────────────────────────────────────────────

if [ "${RUN_INTEGRATION}" = true ]; then
    log_info "Running integration tests..."

    dotnet test tests/GoldBank.IntegrationTests/GoldBank.IntegrationTests.csproj \
        --configuration "${CONFIGURATION}" \
        --logger "junit;LogFilePath=${TEST_RESULTS_DIR}/integration-tests.xml" \
        --collect:"XPlat Code Coverage" \
        --results-directory "${TEST_RESULTS_DIR}/integration-coverage" \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
        || { INTEGRATION_EXIT=$?; [ ${EXIT_CODE} -eq 0 ] && EXIT_CODE=${INTEGRATION_EXIT}; }

    if [ ${EXIT_CODE} -ne 0 ]; then
        log_error "Integration tests failed"
    else
        log_success "Integration tests passed"
    fi
fi

# ─── Coverage Threshold Enforcement ──────────────────────────────────────────

if [ "${RUN_UNIT}" = true ]; then
    log_info "Checking coverage threshold..."

    COVERAGE_FILE=$(find "${TEST_RESULTS_DIR}" -name "coverage.cobertura.xml" -path "*unit-coverage*" | head -1)

    if [ -n "${COVERAGE_FILE}" ]; then
        LINE_RATE=$(grep -oP 'line-rate="\K[^"]+' "${COVERAGE_FILE}" | head -1)
        COVERAGE_PCT=$(echo "${LINE_RATE} * 100" | bc -l | xargs printf "%.2f")

        log_info "Line coverage: ${COVERAGE_PCT}%"

        THRESHOLD_MET=$(echo "${COVERAGE_PCT} >= ${COVERAGE_THRESHOLD}" | bc -l)
        if [ "${THRESHOLD_MET}" -eq 0 ]; then
            log_error "Coverage ${COVERAGE_PCT}% is below the required threshold of ${COVERAGE_THRESHOLD}%"
            EXIT_CODE=1
        else
            log_success "Coverage threshold met: ${COVERAGE_PCT}% >= ${COVERAGE_THRESHOLD}%"
        fi
    else
        log_warn "No coverage report found. Skipping threshold check."
    fi
fi

# ─── Summary ──────────────────────────────────────────────────────────────────

echo ""
log_info "Test results saved to: ${TEST_RESULTS_DIR}/"

if [ -f "${TEST_RESULTS_DIR}/unit-tests.xml" ]; then
    log_info "  - Unit test report:        ${TEST_RESULTS_DIR}/unit-tests.xml"
fi
if [ -f "${TEST_RESULTS_DIR}/integration-tests.xml" ]; then
    log_info "  - Integration test report:  ${TEST_RESULTS_DIR}/integration-tests.xml"
fi

COVERAGE_FILES=$(find "${TEST_RESULTS_DIR}" -name "coverage.cobertura.xml" 2>/dev/null)
if [ -n "${COVERAGE_FILES}" ]; then
    log_info "  - Coverage reports:"
    echo "${COVERAGE_FILES}" | while read -r f; do
        log_info "      ${f}"
    done
fi

echo ""
if [ ${EXIT_CODE} -eq 0 ]; then
    log_success "All tests passed!"
else
    log_error "Some tests failed. Exit code: ${EXIT_CODE}"
fi

exit ${EXIT_CODE}
