"""
Redis-based cache for AI responses.

Replaces the in-memory dict cache with persistent Redis storage.
Gracefully degrades to no-cache if Redis is unavailable.

Cache keys are SHA-256 hashed because prompts can be very long.
TTL is 10 minutes (matches the previous in-memory implementation).
"""

import hashlib
import json
import logging

logger = logging.getLogger(__name__)

CACHE_TTL = 600  # 10 minutes
CACHE_PREFIX = "ai:response:"

# Module-level Redis reference (initialized during app startup)
_redis_client = None


def init(redis_client) -> None:
    """Initialize cache with Redis client. Called during app startup."""
    global _redis_client
    _redis_client = redis_client
    if redis_client:
        logger.info("AI response cache initialized with Redis (TTL=%ds)", CACHE_TTL)
    else:
        logger.warning("AI response cache disabled (no Redis client)")


def _hash_key(raw_key: str) -> str:
    """Create a fixed-length Redis key from a potentially long cache key."""
    digest = hashlib.sha256(raw_key.encode()).hexdigest()[:32]
    return f"{CACHE_PREFIX}{digest}"


async def get(cache_key: str) -> tuple[dict, dict] | None:
    """Get cached AI response from Redis.

    Args:
        cache_key: Raw cache key (will be hashed).

    Returns:
        Tuple of (result dict, usage dict) or None if not found/expired.
    """
    if not _redis_client:
        return None

    try:
        redis_key = _hash_key(cache_key)
        data = await _redis_client.get(redis_key)
        if data:
            parsed = json.loads(data)
            logger.info("AI cache HIT: %s", redis_key[:40])
            return parsed["result"], parsed["usage"]
    except Exception as e:
        logger.warning("Redis AI cache read failed: %s", e)

    return None


async def set(cache_key: str, result: dict, usage: dict) -> None:
    """Store AI response in Redis cache.

    Args:
        cache_key: Raw cache key (will be hashed).
        result: Parsed AI response dict.
        usage: Token usage metadata dict.
    """
    if not _redis_client:
        return

    try:
        redis_key = _hash_key(cache_key)
        data = json.dumps({"result": result, "usage": usage}, default=str)
        await _redis_client.setex(redis_key, CACHE_TTL, data)
        logger.debug("AI cache SET: %s (TTL=%ds)", redis_key[:40], CACHE_TTL)
    except Exception as e:
        logger.warning("Redis AI cache write failed: %s", e)
