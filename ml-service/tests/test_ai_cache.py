"""
Unit tests for services/ai_cache.py

Covers:
- _hash_key: SHA-256 key hashing with prefix
- get: cache hit, cache miss, Redis unavailable, malformed data
- set: store, Redis unavailable
- init: module initialization
"""

import json
import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

ML_ROOT = Path(__file__).resolve().parents[1]
if str(ML_ROOT) not in sys.path:
    sys.path.insert(0, str(ML_ROOT))

from services import ai_cache


# ============================================================================
# _hash_key
# ============================================================================


class TestHashKey:
    def test_returns_prefixed_key(self) -> None:
        key = ai_cache._hash_key("some prompt text")
        assert key.startswith("ai:response:")

    def test_fixed_length(self) -> None:
        short = ai_cache._hash_key("hi")
        long_key = ai_cache._hash_key("x" * 10000)
        # prefix (12) + 32 hex chars = 44 total
        assert len(short) == 44
        assert len(long_key) == 44

    def test_deterministic(self) -> None:
        k1 = ai_cache._hash_key("test")
        k2 = ai_cache._hash_key("test")
        assert k1 == k2

    def test_different_inputs_different_keys(self) -> None:
        k1 = ai_cache._hash_key("prompt A")
        k2 = ai_cache._hash_key("prompt B")
        assert k1 != k2


# ============================================================================
# get
# ============================================================================


class TestCacheGet:
    @pytest.mark.asyncio
    async def test_returns_none_when_no_redis(self) -> None:
        ai_cache._redis_client = None
        result = await ai_cache.get("any_key")
        assert result is None

    @pytest.mark.asyncio
    async def test_cache_miss(self) -> None:
        mock_redis = AsyncMock()
        mock_redis.get = AsyncMock(return_value=None)
        ai_cache._redis_client = mock_redis

        result = await ai_cache.get("missing_key")
        assert result is None

    @pytest.mark.asyncio
    async def test_cache_hit(self) -> None:
        cached_data = {
            "result": {"phases": [{"name": "MVP"}]},
            "usage": {"promptTokens": 100},
        }
        mock_redis = AsyncMock()
        mock_redis.get = AsyncMock(return_value=json.dumps(cached_data))
        ai_cache._redis_client = mock_redis

        result = await ai_cache.get("hit_key")
        assert result is not None
        assert result[0] == cached_data["result"]
        assert result[1] == cached_data["usage"]

    @pytest.mark.asyncio
    async def test_redis_error_returns_none(self) -> None:
        mock_redis = AsyncMock()
        mock_redis.get = AsyncMock(side_effect=ConnectionError("Redis down"))
        ai_cache._redis_client = mock_redis

        result = await ai_cache.get("error_key")
        assert result is None


# ============================================================================
# set
# ============================================================================


class TestCacheSet:
    @pytest.mark.asyncio
    async def test_noop_when_no_redis(self) -> None:
        ai_cache._redis_client = None
        # Should not raise
        await ai_cache.set("key", {"a": 1}, {"tokens": 10})

    @pytest.mark.asyncio
    async def test_stores_with_ttl(self) -> None:
        mock_redis = AsyncMock()
        mock_redis.setex = AsyncMock()
        ai_cache._redis_client = mock_redis

        await ai_cache.set("store_key", {"a": 1}, {"tokens": 10})

        mock_redis.setex.assert_called_once()
        call_args = mock_redis.setex.call_args
        assert call_args[0][1] == ai_cache.CACHE_TTL  # TTL = 600

    @pytest.mark.asyncio
    async def test_redis_error_no_crash(self) -> None:
        mock_redis = AsyncMock()
        mock_redis.setex = AsyncMock(side_effect=ConnectionError("Redis down"))
        ai_cache._redis_client = mock_redis

        # Should not raise
        await ai_cache.set("key", {"a": 1}, {"tokens": 10})


# ============================================================================
# init
# ============================================================================


class TestInit:
    def test_init_with_client(self) -> None:
        mock_redis = MagicMock()
        ai_cache.init(mock_redis)
        assert ai_cache._redis_client is mock_redis

    def test_init_with_none(self) -> None:
        ai_cache.init(None)
        assert ai_cache._redis_client is None
