"""
Shared FastAPI dependencies for DevHunt ML Service.

Provides database pool and Redis client as injectable dependencies.
Uses Request to access app.state, avoiding circular imports with main.py.
"""

import logging
import os

import asyncpg
import redis.asyncio as aioredis
from fastapi import Request

logger = logging.getLogger(__name__)

def _require_database_url() -> str:
    """Return DATABASE_URL or raise — validated lazily, not at import time (DEV-69).

    Raising at module level crashed every import of this module (and thus the whole
    FastAPI app / OpenAPI generation) when DATABASE_URL was unset. The check belongs
    where a DB connection is actually needed.
    """
    url = os.getenv("DATABASE_URL")
    if not url:
        raise RuntimeError(
            "DATABASE_URL environment variable is required. "
            "Set it in .env file or environment."
        )
    return url


# Module-level shared pool for non-request contexts (e.g. tool resolvers)
_shared_pool: asyncpg.Pool | None = None


async def get_shared_pool() -> asyncpg.Pool:
    """Get or create a shared database pool (for use outside FastAPI request context)."""
    global _shared_pool
    if _shared_pool is None or _shared_pool._closed:
        _shared_pool = await asyncpg.create_pool(
            _require_database_url(), min_size=1, max_size=5
        )
    return _shared_pool


async def get_db_pool(request: Request):
    """Get or create database connection pool."""
    pool = getattr(request.app.state, "db_pool", None)
    if pool is None:
        request.app.state.db_pool = await asyncpg.create_pool(
            _require_database_url(), min_size=2, max_size=10
        )
        pool = request.app.state.db_pool
    return pool


async def get_redis_client(request: Request):
    """Get or create Redis client for caching."""
    client = getattr(request.app.state, "redis_client", None)
    if client is None:
        redis_url = os.getenv("REDIS_URL", "redis://cache-service:6379/0")
        request.app.state.redis_client = await aioredis.from_url(
            redis_url, decode_responses=True
        )
        client = request.app.state.redis_client
        logger.info("Redis cache client initialized: %s", redis_url)
    return client
