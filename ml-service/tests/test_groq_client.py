"""
Unit tests for clients/groq_client.py

Covers:
- _clean_and_parse_json: JSON extraction from markdown/raw text
- _get_models_to_try: fallback chain resolution
- _extract_usage_info: usage dict normalization
- _parse_tool_calls: tool call response parsing
- _build_headers: auth header construction
"""

import json
import sys
from pathlib import Path

import pytest

ML_ROOT = Path(__file__).resolve().parents[1]
if str(ML_ROOT) not in sys.path:
    sys.path.insert(0, str(ML_ROOT))

from clients.groq_client import GroqClient, GroqRateLimitError, GroqJSONParseError


@pytest.fixture
def client() -> GroqClient:
    """Create a GroqClient with a fake API key for unit testing."""
    return GroqClient(api_key="test-key-123")


# ============================================================================
# _clean_and_parse_json
# ============================================================================


class TestCleanAndParseJson:
    def test_plain_json(self) -> None:
        result = GroqClient._clean_and_parse_json('{"a": 1}')
        assert result == {"a": 1}

    def test_markdown_json_wrapper(self) -> None:
        raw = '```json\n{"key": "value"}\n```'
        result = GroqClient._clean_and_parse_json(raw)
        assert result == {"key": "value"}

    def test_markdown_no_language(self) -> None:
        raw = '```\n{"key": "value"}\n```'
        result = GroqClient._clean_and_parse_json(raw)
        assert result == {"key": "value"}

    def test_trailing_text_after_json(self) -> None:
        raw = '{"a": 1} some trailing text'
        result = GroqClient._clean_and_parse_json(raw)
        assert result == {"a": 1}

    def test_json_array(self) -> None:
        raw = '[1, 2, 3]'
        result = GroqClient._clean_and_parse_json(raw)
        assert result == [1, 2, 3]

    def test_nested_object(self) -> None:
        data = {"outer": {"inner": [1, 2]}}
        raw = json.dumps(data)
        result = GroqClient._clean_and_parse_json(raw)
        assert result == data

    def test_whitespace_padding(self) -> None:
        raw = '   \n  {"x": 1}  \n  '
        result = GroqClient._clean_and_parse_json(raw)
        assert result == {"x": 1}

    def test_invalid_json_raises(self) -> None:
        with pytest.raises(json.JSONDecodeError):
            GroqClient._clean_and_parse_json("{invalid}")


# ============================================================================
# _get_models_to_try
# ============================================================================


class TestGetModelsToTry:
    def test_smart_has_fallbacks(self, client: GroqClient) -> None:
        models = client._get_models_to_try("smart")
        assert models[0] == "smart"
        assert len(models) > 1
        assert "fast" in models  # fast is always in fallback chain

    def test_fast_no_fallbacks(self, client: GroqClient) -> None:
        models = client._get_models_to_try("fast")
        assert models == ["fast"]

    def test_unknown_model_returns_itself(self, client: GroqClient) -> None:
        models = client._get_models_to_try("custom-model-v2")
        assert models == ["custom-model-v2"]

    def test_versatile_has_fallbacks(self, client: GroqClient) -> None:
        models = client._get_models_to_try("versatile")
        assert models[0] == "versatile"
        assert len(models) > 1


# ============================================================================
# _extract_usage_info
# ============================================================================


class TestExtractUsageInfo:
    def test_full_usage(self) -> None:
        usage = {
            "prompt_tokens": 100,
            "completion_tokens": 50,
            "total_tokens": 150,
        }
        result = GroqClient._extract_usage_info(usage, "gpt-test")
        assert result["promptTokens"] == 100
        assert result["completionTokens"] == 50
        assert result["totalTokens"] == 150
        assert result["model"] == "gpt-test"

    def test_empty_usage(self) -> None:
        result = GroqClient._extract_usage_info({})
        assert result["promptTokens"] is None
        assert result["completionTokens"] is None
        assert "model" not in result

    def test_no_model_name(self) -> None:
        result = GroqClient._extract_usage_info({"prompt_tokens": 10})
        assert "model" not in result


# ============================================================================
# _build_headers
# ============================================================================


class TestBuildHeaders:
    def test_contains_auth(self, client: GroqClient) -> None:
        headers = client._build_headers()
        assert headers["Authorization"] == "Bearer test-key-123"
        assert headers["Content-Type"] == "application/json"


# ============================================================================
# _parse_tool_calls
# ============================================================================


class TestParseToolCalls:
    def test_none_input(self) -> None:
        assert GroqClient._parse_tool_calls(None) is None

    def test_empty_list(self) -> None:
        assert GroqClient._parse_tool_calls([]) is None

    def test_valid_tool_call(self) -> None:
        raw = [
            {
                "id": "call_1",
                "type": "function",
                "function": {
                    "name": "create_task",
                    "arguments": '{"title": "Setup DB"}',
                },
            }
        ]
        result = GroqClient._parse_tool_calls(raw)
        assert result is not None
        assert len(result) == 1
        assert result[0]["function"]["name"] == "create_task"
        assert result[0]["function"]["arguments_parsed"] == {"title": "Setup DB"}

    def test_invalid_arguments_json(self) -> None:
        raw = [
            {
                "id": "call_2",
                "type": "function",
                "function": {
                    "name": "update_task",
                    "arguments": "not valid json",
                },
            }
        ]
        result = GroqClient._parse_tool_calls(raw)
        assert result is not None
        assert result[0]["function"]["arguments_parsed"] is None

    def test_multiple_tool_calls(self) -> None:
        raw = [
            {
                "id": "call_1",
                "type": "function",
                "function": {"name": "fn1", "arguments": "{}"},
            },
            {
                "id": "call_2",
                "type": "function",
                "function": {"name": "fn2", "arguments": '{"x": 1}'},
            },
        ]
        result = GroqClient._parse_tool_calls(raw)
        assert result is not None
        assert len(result) == 2


# ============================================================================
# Exception classes
# ============================================================================


class TestExceptions:
    def test_rate_limit_error_is_exception(self) -> None:
        err = GroqRateLimitError("Too many requests")
        assert isinstance(err, Exception)
        assert "Too many" in str(err)

    def test_json_parse_error_is_exception(self) -> None:
        err = GroqJSONParseError("Failed after 3 retries")
        assert isinstance(err, Exception)


# ============================================================================
# MODELS dict
# ============================================================================


class TestModelsConfig:
    def test_smart_model_exists(self) -> None:
        assert "smart" in GroqClient.MODELS

    def test_fast_model_exists(self) -> None:
        assert "fast" in GroqClient.MODELS

    def test_fallback_chain_keys_are_valid(self) -> None:
        for key in GroqClient.FALLBACK_CHAIN:
            assert key in GroqClient.MODELS, f"Fallback key '{key}' not in MODELS"
            for fb in GroqClient.FALLBACK_CHAIN[key]:
                assert fb in GroqClient.MODELS, f"Fallback '{fb}' for '{key}' not in MODELS"
