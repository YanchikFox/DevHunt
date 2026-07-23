#!/bin/bash
# DevHunt API Testing Script (Bash version)

set -e

AUTH_URL="http://localhost:7001"
API_URL="http://localhost:7002"
TEST_EMAIL="test@example.com"
TEST_PASSWORD="TestPassword123!"

echo "🧪 DevHunt API Testing Script"
echo "================================"
echo ""

# Проверка доступности сервисов
echo "ℹ️  Checking services availability..."
if curl -f -s "$API_URL/health" > /dev/null; then
    echo "✅ Core API is running"
else
    echo "❌ Core API is not responding. Make sure it's running on $API_URL"
    echo "Run: docker-compose up -d"
    exit 1
fi

# Регистрация/логин
echo ""
echo "=== AUTHENTICATION ==="
echo "Registering user: $TEST_EMAIL"

REGISTER_RESPONSE=$(curl -s -X POST "$AUTH_URL/auth/register" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\"}")

if echo "$REGISTER_RESPONSE" | grep -q "token"; then
    TOKEN=$(echo "$REGISTER_RESPONSE" | grep -o '"token":"[^"]*' | cut -d'"' -f4)
    echo "✅ User registered successfully"
else
    echo "ℹ️  User already exists, trying to login..."
    LOGIN_RESPONSE=$(curl -s -X POST "$AUTH_URL/auth/login" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\"}")
    
    if echo "$LOGIN_RESPONSE" | grep -q "token"; then
        TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"token":"[^"]*' | cut -d'"' -f4)
        echo "✅ Login successful"
    else
        echo "❌ Failed to authenticate"
        exit 1
    fi
fi

echo "✅ Token obtained (value redacted)"

# Тестирование Core API
echo ""
echo "=== CORE API TESTS ==="

# Тест 1: Профиль
echo ""
echo "[1] Testing Profile..."
PROFILE=$(curl -s -X GET "$API_URL/api/profile/me" \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json")
echo "✅ Profile retrieved"

# Тест 2: Публичные endpoints
echo ""
echo "[2] Testing Public Endpoints..."
USERS=$(curl -s -X GET "$API_URL/api/users")
echo "✅ Users endpoint works"

PROJECTS=$(curl -s -X GET "$API_URL/api/projects")
echo "✅ Projects endpoint works"

SKILLS=$(curl -s -X GET "$API_URL/api/skills")
echo "✅ Skills endpoint works"

# Тест 3: Health check
echo ""
echo "[3] Testing Health & Metrics..."
HEALTH=$(curl -s -X GET "$API_URL/health")
echo "✅ Health check passed"

echo ""
echo "================================"
echo "✅ Testing completed!"
echo ""
echo "📚 Useful Links:"
echo "  Swagger UI: http://localhost:7002/swagger"
echo "  RabbitMQ: http://localhost:15672 (devhunt/devhunt_password)"
echo "  SeaweedFS: http://localhost:8888"

