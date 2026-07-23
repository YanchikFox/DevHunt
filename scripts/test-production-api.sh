#!/bin/bash
# Script to test production API without Swagger
# This script checks all API endpoints and services

BASE_URL="${1:-http://localhost}"
VERBOSE="${2:-false}"

echo "=== Production API Health Check ==="
echo "Base URL: $BASE_URL"
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test function
test_endpoint() {
    local name=$1
    local url=$2
    local method=${3:-GET}
    
    if curl -s -f -X "$method" "$url" -o /dev/null -w "%{http_code}" > /tmp/response_code 2>/dev/null; then
        local status=$(cat /tmp/response_code)
        if [ "$status" = "200" ] || [ "$status" = "204" ]; then
            echo -e "${GREEN}✓${NC} $name ($status)"
            return 0
        else
            echo -e "${YELLOW}→${NC} $name ($status)"
            return 1
        fi
    else
        echo -e "${RED}✗${NC} $name - Connection failed"
        return 1
    fi
}

# Test API Gateway
echo "--- API Gateway ---"
test_endpoint "API Gateway (HTTP)" "$BASE_URL"

# Test Auth Service
echo ""
echo "--- Auth Service ---"
test_endpoint "Health Check" "http://localhost:7001/health"
test_endpoint "Metrics" "http://localhost:7001/metrics"

# Test Core API
echo ""
echo "--- Core API ---"
test_endpoint "Health Check" "http://localhost:7002/health"
test_endpoint "Metrics" "http://localhost:7002/metrics"

# Test Core API endpoints (public)
echo ""
echo "--- Core API Endpoints (Public) ---"
test_endpoint "Get All Skills" "http://localhost:7002/api/skills?page=1&pageSize=10"
test_endpoint "Get All Achievements" "http://localhost:7002/api/achievements?page=1&pageSize=10"

# Test Infrastructure
echo ""
echo "--- Infrastructure Services ---"
test_endpoint "Prometheus Metrics" "http://localhost:9090/metrics"
test_endpoint "Jaeger UI" "http://localhost:16686"

# Test with authentication (if token provided)
if [ -n "$TEST_TOKEN" ]; then
    echo ""
    echo "--- Authenticated Endpoints ---"
    test_endpoint "Get My Profile" "http://localhost:7002/api/profile/me" -H "Authorization: Bearer $TEST_TOKEN"
    test_endpoint "Get My Projects" "http://localhost:7002/api/projects?page=1&pageSize=10" -H "Authorization: Bearer $TEST_TOKEN"
fi

echo ""
echo "=== Test Complete ==="
echo ""
echo "To test with authentication, set environment variable:"
echo "  export TEST_TOKEN='your-jwt-token'"
echo ""
echo "To get a token, register/login via:"
echo "  POST http://localhost:7001/api/auth/register"
echo "  POST http://localhost:7001/api/auth/login"

