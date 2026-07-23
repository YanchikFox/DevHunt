#!/bin/bash
# OpenObserve Alerts Initialization Script
# Runs after OpenObserve starts для настройки алертов

OPENOBSERVE_URL="${OPENOBSERVE_URL:-http://openobserve:5080}"
OPENOBSERVE_USER="${OPENOBSERVE_USER:-admin@devhunt.local}"
OPENOBSERVE_PASSWORD="${OPENOBSERVE_PASSWORD:-ChangeMe123!}"
ALERT_EMAIL="${ALERT_EMAIL:-}"

# Wait for OpenObserve to be ready
echo "Waiting for OpenObserve to be ready..."
until curl -sf "${OPENOBSERVE_URL}/healthz" > /dev/null 2>&1; do
  sleep 2
done
echo "OpenObserve is ready!"

AUTH=$(echo -n "${OPENOBSERVE_USER}:${OPENOBSERVE_PASSWORD}" | base64)

# Function to make API calls
api_call() {
  local method=$1
  local endpoint=$2
  local data=$3

  curl -sf -X "${method}" \
    "${OPENOBSERVE_URL}/api/default${endpoint}" \
    -H "Authorization: Basic ${AUTH}" \
    -H "Content-Type: application/json" \
    -d "${data}"
}

# 1. Create Email Alert Destination (if email is configured)
if [ -n "${ALERT_EMAIL}" ]; then
  echo "Creating email alert destination..."
  api_call POST "/alerts/destinations" '{
    "name": "devhunt-email",
    "template": "devhunt-template",
    "destination_type": "email",
    "emails": ["'"${ALERT_EMAIL}"'"]
  }' && echo " - Email destination created"
fi

# 2. Create Webhook Alert Destination (for custom integrations)
echo "Creating webhook alert destination..."
api_call POST "/alerts/destinations" '{
  "name": "devhunt-webhook",
  "template": "devhunt-template",
  "destination_type": "http",
  "url": "http://notification-service:5003/api/alerts/webhook",
  "method": "POST",
  "headers": {"Content-Type": "application/json"}
}' && echo " - Webhook destination created"

# 3. Create Alert Template
echo "Creating alert template..."
api_call POST "/alerts/templates" '{
  "name": "devhunt-template",
  "body": "Alert: {{alert_name}}\nSeverity: {{severity}}\nOrganization: {{org_name}}\nStream: {{stream_name}}\nTime: {{alert_start_time}}\n\nCondition: {{alert_condition}}\nValue: {{alert_value}}\n\nDescription: {{alert_description}}"
}' && echo " - Alert template created"

# 4. Create Alert Rules

# High Error Rate Alert
echo "Creating high error rate alert..."
api_call POST "/alerts" '{
  "name": "high-error-rate",
  "stream_name": "default",
  "stream_type": "logs",
  "query_condition": {
    "type": "sql",
    "sql": "SELECT count(*) as error_count FROM default WHERE level = '\''error'\'' OR level = '\''ERROR'\''"
  },
  "trigger_condition": {
    "period": 5,
    "operator": ">=",
    "threshold": 10
  },
  "destination": "devhunt-webhook",
  "enabled": true,
  "description": "Triggers when error count exceeds 10 in 5 minutes"
}' && echo " - High error rate alert created"

# Service Down Alert (no logs for 5 minutes)
echo "Creating service health alerts..."
for service in core-api auth-service notification-service integration-gateway ml-service; do
  api_call POST "/alerts" '{
    "name": "'"${service}"'-silent",
    "stream_name": "default",
    "stream_type": "logs",
    "query_condition": {
      "type": "sql",
      "sql": "SELECT count(*) as log_count FROM default WHERE service = '\'''"${service}"'\''"
    },
    "trigger_condition": {
      "period": 5,
      "operator": "<",
      "threshold": 1
    },
    "destination": "devhunt-webhook",
    "enabled": true,
    "description": "No logs from '"${service}"' for 5 minutes - service may be down"
  }' && echo " - ${service} health alert created"
done

# Slow Response Time Alert (traces)
echo "Creating slow response alert..."
api_call POST "/alerts" '{
  "name": "slow-response-time",
  "stream_name": "default",
  "stream_type": "traces",
  "query_condition": {
    "type": "sql",
    "sql": "SELECT avg(duration) as avg_duration FROM default WHERE duration > 1000000000"
  },
  "trigger_condition": {
    "period": 5,
    "operator": ">=",
    "threshold": 2000000000
  },
  "destination": "devhunt-webhook",
  "enabled": true,
  "description": "Average response time exceeds 2 seconds"
}' && echo " - Slow response alert created"

# High Memory Usage Alert (if metrics are available)
echo "Creating resource usage alert..."
api_call POST "/alerts" '{
  "name": "high-memory-usage",
  "stream_name": "default",
  "stream_type": "metrics",
  "query_condition": {
    "type": "sql",
    "sql": "SELECT avg(value) as memory_pct FROM default WHERE __name__ = '\''process_resident_memory_bytes'\''"
  },
  "trigger_condition": {
    "period": 5,
    "operator": ">=",
    "threshold": 1073741824
  },
  "destination": "devhunt-webhook",
  "enabled": false,
  "description": "Memory usage exceeds 1GB (disabled by default)"
}' && echo " - Memory alert created (disabled)"

# 5. Create Dashboard
DASHBOARD_FILE="/monitoring/openobserve/openobserve-dashboard.json"
if [ -f "$DASHBOARD_FILE" ]; then
  echo "Creating DevHunt Services dashboard..."
  HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -X POST \
    "${OPENOBSERVE_URL}/api/default/dashboards" \
    -H "Authorization: Basic ${AUTH}" \
    -H "Content-Type: application/json" \
    -d "@${DASHBOARD_FILE}")
  if [ "$HTTP_STATUS" = "200" ] || [ "$HTTP_STATUS" = "201" ]; then
    echo " - Dashboard created (HTTP ${HTTP_STATUS})"
  else
    echo " - Dashboard creation returned HTTP ${HTTP_STATUS} (may already exist, skipping)"
  fi
fi

echo ""
echo "========================================="
echo "OpenObserve alerts initialization complete!"
echo "========================================="
echo ""
echo "Configured alerts:"
echo "  - high-error-rate (logs)"
echo "  - *-silent (service health)"
echo "  - slow-response-time (traces)"
echo "  - high-memory-usage (metrics, disabled)"
echo ""
echo "Web Interface: ${OPENOBSERVE_URL}"
echo "User: ${OPENOBSERVE_USER}"
echo "Password: ${OPENOBSERVE_PASSWORD}"
echo "Alerts: ${OPENOBSERVE_URL}/web/alerts"
echo "Dashboards: ${OPENOBSERVE_URL}/web/dashboards"

