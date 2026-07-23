"""
DevHunt ML Service — FastAPI application entry point.

Handles app setup, middleware, tracing, and lifecycle.
Business logic lives in:
  - routers/ai.py            — AI planning endpoints
  - routers/passport.py      — project passport generation
  - routers/recommendations.py — recommendation engine
  - services/ai_service.py   — AI generation logic
  - services/ai_cache.py     — Redis-based AI response cache

Refactored from 823 → ~190 lines (task #2).
"""

import asyncio
import base64
import logging
import os
import time

import redis.asyncio as aioredis
from consumers.event_consumer import start_event_consumer, stop_event_consumer
from fastapi import Depends, FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import PlainTextResponse
from opentelemetry import trace
from opentelemetry._logs import set_logger_provider
from opentelemetry.exporter.otlp.proto.http._log_exporter import OTLPLogExporter
from opentelemetry.exporter.otlp.proto.http.trace_exporter import (
    OTLPSpanExporter as OTLPHTTPSpanExporter,
)
from opentelemetry.instrumentation.asyncpg import AsyncPGInstrumentor
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor
from opentelemetry.instrumentation.logging import LoggingInstrumentor
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from pydantic import BaseModel
from prometheus_client import CONTENT_TYPE_LATEST, generate_latest

from deps import get_db_pool
from metrics_auth import verify_metrics_access
from metrics import (
    registry as metrics_registry,
    REQUEST_COUNTER,
    REQUEST_LATENCY,
    RABBIT_CONNECTION_STATE,
    EVENT_MESSAGES_TOTAL,
)
from routers import ai
from routers import recommendations
from routers import passport
from services import ai_cache

# Optional: embeddings router (requires fastembed — skip gracefully if not installed)
_has_embeddings = False
try:
    from routers import embeddings as _embeddings_router
    _has_embeddings = True
except (ImportError, Exception):
    pass  # logged after basicConfig below

# Logging configuration
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

ENVIRONMENT = os.getenv("ENVIRONMENT", "development").lower()


# ============================================================================
# OpenTelemetry Tracing + Logging
# ============================================================================


def configure_tracing():
    resource = Resource.create(
        {"service.name": os.getenv("OTEL_SERVICE_NAME", "ml-service")}
    )
    provider = TracerProvider(resource=resource)

    openobserve_endpoint = os.getenv(
        "OTEL_EXPORTER_OTLP_ENDPOINT",
        "http://openobserve:5080/api/default/v1/traces",
    )

    openobserve_user = os.getenv("OPENOBSERVE_ROOT_USER")
    openobserve_password = os.getenv("OPENOBSERVE_ROOT_PASSWORD")

    if ENVIRONMENT == "production":
        if not openobserve_user or not openobserve_password:
            logger.warning(
                "OpenObserve credentials not set in production. Tracing disabled."
            )
            return provider
    else:
        openobserve_user = openobserve_user or "admin@devhunt.local"
        openobserve_password = openobserve_password or "ChangeMe123!"

    openobserve_token = base64.b64encode(
        f"{openobserve_user}:{openobserve_password}".encode()
    ).decode()

    provider.add_span_processor(
        BatchSpanProcessor(
            OTLPHTTPSpanExporter(
                endpoint=openobserve_endpoint,
                headers={"Authorization": f"Basic {openobserve_token}"},
            )
        )
    )

    trace.set_tracer_provider(provider)
    AsyncPGInstrumentor().instrument()
    HTTPXClientInstrumentor().instrument()

    # OTLP Logging
    logger_provider = LoggerProvider(resource=resource)
    set_logger_provider(logger_provider)
    log_exporter = OTLPLogExporter(
        endpoint=os.getenv(
            "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT",
            "http://openobserve:5080/api/default/logs/otlp",
        ),
        headers={"Authorization": f"Basic {openobserve_token}"},
    )
    logger_provider.add_log_record_processor(BatchLogRecordProcessor(log_exporter))
    LoggingInstrumentor().instrument(set_logging_format=True)

    return provider


# ============================================================================
# FastAPI App
# ============================================================================

app = FastAPI(
    title="DevHunt ML Service",
    description="Recommendation engine + AI planning assistant",
    version="1.0.0",
)

tracer_provider = configure_tracing()
FastAPIInstrumentor.instrument_app(
    app, tracer_provider=tracer_provider, excluded_urls="/metrics"
)

# CORS
allowed_origins = os.getenv("CORS_ALLOWED_ORIGINS", "*").split(",")
if ENVIRONMENT == "production":
    if "*" in allowed_origins or allowed_origins == ["*"]:
        raise ValueError(
            "CORS_ALLOWED_ORIGINS must be explicitly set in production. "
            "Wildcard '*' is not allowed. Set specific origins."
        )
    logger.info("CORS production origins: %s", allowed_origins)

app.add_middleware(
    CORSMiddleware,
    allow_origins=allowed_origins,
    allow_credentials=True,
    allow_methods=["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    allow_headers=["Content-Type", "Authorization", "X-Requested-With"],
)

# Include routers
app.include_router(ai.router)
app.include_router(recommendations.router)
app.include_router(passport.router)
if _has_embeddings:
    app.include_router(_embeddings_router.router)
    logger.info("Embeddings router enabled")
else:
    logger.warning("fastembed not available — embeddings endpoints disabled")

# App state defaults
app.state.db_pool = None
app.state.redis_client = None


@app.middleware("http")
async def record_metrics(request: Request, call_next):
    start_time = time.perf_counter()
    response = await call_next(request)
    status_code = str(response.status_code)
    route = getattr(request.scope.get("route"), "path", request.url.path)
    REQUEST_COUNTER.labels(request.method, route, status_code).inc()
    REQUEST_LATENCY.labels(request.method, route).observe(
        time.perf_counter() - start_time
    )
    return response


# ============================================================================
# Lifecycle Events
# ============================================================================

_consumer_task: asyncio.Task | None = None


@app.on_event("startup")
async def startup_event():
    global _consumer_task

    # Initialize Redis + AI cache
    redis_url = os.getenv("REDIS_URL", "redis://cache-service:6379/0")
    try:
        app.state.redis_client = await aioredis.from_url(
            redis_url, decode_responses=True
        )
        ai_cache.init(app.state.redis_client)
        logger.info("Redis + AI cache initialized")
    except Exception as e:
        logger.warning("Redis not available, AI cache disabled: %s", e)
        app.state.redis_client = None

    # Start RabbitMQ event consumer
    try:
        _consumer_task = asyncio.create_task(
            start_event_consumer(
                on_connection_state=lambda connected: RABBIT_CONNECTION_STATE.set(
                    1 if connected else 0
                ),
                on_event=lambda routing_key, status: EVENT_MESSAGES_TOTAL.labels(
                    routing_key or "unknown", status
                ).inc(),
            )
        )
        logger.info("Event consumer started")
    except Exception as e:
        logger.warning("Failed to start event consumer: %s", e)


@app.on_event("shutdown")
async def shutdown_event():
    try:
        await stop_event_consumer()
        logger.info("Event consumer stopped")
    except Exception as e:
        logger.warning("Error stopping event consumer: %s", e)

    db_pool = getattr(app.state, "db_pool", None)
    if db_pool:
        await db_pool.close()
        app.state.db_pool = None

    redis_client = getattr(app.state, "redis_client", None)
    if redis_client:
        await redis_client.close()
        app.state.redis_client = None
        logger.info("Redis client closed")


# ============================================================================
# Core Endpoints
# ============================================================================


class HealthResponse(BaseModel):
    status: str
    database: str
    version: str = "1.0.0"


@app.get("/metrics")
async def metrics(request: Request) -> PlainTextResponse:
    verify_metrics_access(request)
    return PlainTextResponse(
        generate_latest(metrics_registry), media_type=CONTENT_TYPE_LATEST
    )


@app.get("/health")
async def health_check(db_pool=Depends(get_db_pool)) -> HealthResponse:
    try:
        async with db_pool.acquire() as conn:
            await conn.fetchval("SELECT 1")
        db_status = "connected"
    except Exception as e:
        logger.error("Database health check failed: %s", e)
        db_status = "disconnected"

    return HealthResponse(
        status="healthy" if db_status == "connected" else "degraded",
        database=db_status,
    )


@app.get("/")
async def root():
    return {
        "service": "DevHunt ML Service",
        "version": "1.0.0",
        "status": "running",
        "endpoints": {
            "health": "/health",
            "generate": "/api/recommendations/generate",
            "ai": "/api/ai",
            "refresh": "/api/recommendations/refresh",
            "docs": "/docs",
        },
    }


if __name__ == "__main__":
    import uvicorn

    host = os.getenv("HOST", "127.0.0.1")
    port_str = os.getenv("PORT", "8000")
    port = int(port_str) if port_str else 8000
    uvicorn.run(app, host=host, port=port)
