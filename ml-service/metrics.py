"""
Centralized Prometheus metrics for DevHunt ML Service.

All metrics use a shared CollectorRegistry so the /metrics endpoint
returns them all in one scrape.
"""

from prometheus_client import CollectorRegistry, Counter, Gauge, Histogram

registry = CollectorRegistry()

# ============================================================================
# HTTP Request Metrics (moved from main.py)
# ============================================================================

REQUEST_COUNTER = Counter(
    "ml_service_requests_total",
    "Total HTTP requests processed",
    ["method", "endpoint", "status_code"],
    registry=registry,
)

REQUEST_LATENCY = Histogram(
    "ml_service_request_duration_seconds",
    "Request latency in seconds",
    ["method", "endpoint"],
    registry=registry,
    buckets=(0.05, 0.1, 0.25, 0.5, 1, 2, 5),
)

# ============================================================================
# RabbitMQ Metrics (moved from main.py)
# ============================================================================

RABBIT_CONNECTION_STATE = Gauge(
    "ml_service_rabbitmq_connection_state",
    "RabbitMQ connection state (1=connected, 0=disconnected)",
    registry=registry,
)

EVENT_MESSAGES_TOTAL = Counter(
    "ml_service_event_messages_total",
    "Events consumed from RabbitMQ",
    ["routing_key", "status"],
    registry=registry,
)

# ============================================================================
# AI Metrics — Task #15-#19
# ============================================================================

# #15: AI response validation failure rate
AI_VALIDATION_FAILURES = Counter(
    "ml_service_ai_validation_failures_total",
    "AI response validation failures",
    ["endpoint", "error_type"],
    registry=registry,
)

# #16: Fallback chain trigger rate (by model)
AI_FALLBACK_TRIGGERS = Counter(
    "ml_service_ai_fallback_triggers_total",
    "AI model fallback chain triggers",
    ["from_model", "to_model", "reason"],
    registry=registry,
)

# #17: Token consumption aggregated (by endpoint)
AI_TOKENS_TOTAL = Counter(
    "ml_service_ai_tokens_total",
    "Total AI tokens consumed",
    ["endpoint", "token_type", "model"],
    registry=registry,
)

# #18: Latency per model (not just per endpoint)
AI_MODEL_LATENCY = Histogram(
    "ml_service_ai_model_latency_seconds",
    "AI model response latency in seconds",
    ["model", "endpoint"],
    registry=registry,
    buckets=(0.5, 1, 2, 5, 10, 20, 30, 60),
)

# #19: Rate limit (429) frequency
AI_RATE_LIMITS = Counter(
    "ml_service_ai_rate_limits_total",
    "AI provider rate limit (429) responses",
    ["model", "provider"],
    registry=registry,
)

# AI cache hit/miss tracking
AI_CACHE_HITS = Counter(
    "ml_service_ai_cache_hits_total",
    "AI response cache hits",
    ["endpoint"],
    registry=registry,
)

AI_CACHE_MISSES = Counter(
    "ml_service_ai_cache_misses_total",
    "AI response cache misses",
    ["endpoint"],
    registry=registry,
)
