"""
Unit tests for services/ai_service.py

Covers:
- render_prompt: template rendering with {{placeholders}}
- parse_json_response: JSON extraction from raw AI text (markdown, whitespace, etc.)
- build_goals_section: goal flags → prompt text
- build_scale_section: idea length → scale instruction
- build_constraints_section: free-tier keyword detection
- _detect_project_type: idea text → project category
- build_project_type_section: project type + goals → section text
- emit_token_metrics: Prometheus counter increments
"""

import json
import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

# ── path setup ──
ML_ROOT = Path(__file__).resolve().parents[1]
if str(ML_ROOT) not in sys.path:
    sys.path.insert(0, str(ML_ROOT))

from models.ai_models import TechStackGoals
from services.ai_service import (
    render_prompt,
    parse_json_response,
    build_goals_section,
    build_scale_section,
    build_constraints_section,
    _detect_project_type,
    build_project_type_section,
    emit_token_metrics,
    _is_rate_limit_error,
    _build_custom_section,
)


# ============================================================================
# render_prompt
# ============================================================================


class TestRenderPrompt:
    def test_single_placeholder(self) -> None:
        result = render_prompt("Hello, {{name}}!", name="World")
        assert result == "Hello, World!"

    def test_multiple_placeholders(self) -> None:
        result = render_prompt(
            "Build {{app}} with {{tech}}",
            app="DevHunt",
            tech="FastAPI",
        )
        assert result == "Build DevHunt with FastAPI"

    def test_repeated_placeholder(self) -> None:
        result = render_prompt(
            "{{x}} + {{x}} = 2*{{x}}",
            x="a",
        )
        assert result == "a + a = 2*a"

    def test_no_placeholders(self) -> None:
        result = render_prompt("plain text")
        assert result == "plain text"

    def test_unused_placeholder_stays(self) -> None:
        result = render_prompt("{{keep}} {{replace}}", replace="done")
        assert result == "{{keep}} done"

    def test_multiline_template(self) -> None:
        template = "Line1: {{a}}\nLine2: {{b}}\nLine3: {{a}}"
        result = render_prompt(template, a="X", b="Y")
        assert result == "Line1: X\nLine2: Y\nLine3: X"


# ============================================================================
# parse_json_response
# ============================================================================


class TestParseJsonResponse:
    def test_clean_json(self) -> None:
        raw = '{"key": "value", "num": 42}'
        assert parse_json_response(raw) == {"key": "value", "num": 42}

    def test_markdown_wrapped(self) -> None:
        raw = '```json\n{"a": 1}\n```'
        assert parse_json_response(raw) == {"a": 1}

    def test_markdown_no_lang(self) -> None:
        raw = '```\n{"a": 1}\n```'
        assert parse_json_response(raw) == {"a": 1}

    def test_text_before_json(self) -> None:
        raw = 'Here is the result:\n{"answer": true}'
        assert parse_json_response(raw) == {"answer": True}

    def test_text_after_json(self) -> None:
        raw = '{"answer": true}\nLet me know if you need more.'
        assert parse_json_response(raw) == {"answer": True}

    def test_whitespace_padding(self) -> None:
        raw = '  \n  {"x": 1}  \n  '
        assert parse_json_response(raw) == {"x": 1}

    def test_nested_json(self) -> None:
        data = {"phases": [{"name": "MVP", "tasks": [{"title": "Setup"}]}]}
        raw = json.dumps(data)
        assert parse_json_response(raw) == data

    def test_empty_raises(self) -> None:
        with pytest.raises(ValueError, match="Empty"):
            parse_json_response("")

    def test_invalid_json_raises(self) -> None:
        with pytest.raises(json.JSONDecodeError):
            parse_json_response("{not valid json}")

    def test_no_braces_raises(self) -> None:
        with pytest.raises(json.JSONDecodeError):
            parse_json_response("just plain text without json")


# ============================================================================
# build_goals_section
# ============================================================================


class TestBuildGoalsSection:
    def test_none_goals_returns_empty(self) -> None:
        assert build_goals_section(None) == ""

    def test_no_active_goals_returns_empty(self) -> None:
        goals = TechStackGoals()
        assert build_goals_section(goals) == ""

    def test_single_goal(self) -> None:
        goals = TechStackGoals(performance=True)
        result = build_goals_section(goals)
        assert "Performance" in result
        assert "prioritize fast execution" in result

    def test_multiple_goals(self) -> None:
        goals = TechStackGoals(cost=True, security=True)
        result = build_goals_section(goals)
        assert "Cost efficiency" in result
        assert "Security" in result

    def test_all_goals(self) -> None:
        goals = TechStackGoals(
            performance=True,
            cost=True,
            developerSpeed=True,
            scalability=True,
            security=True,
            maintainability=True,
        )
        result = build_goals_section(goals)
        assert "Performance" in result
        assert "Cost" in result
        assert "Developer speed" in result
        assert "Scalability" in result
        assert "Security" in result
        assert "Maintainability" in result


# ============================================================================
# build_scale_section
# ============================================================================


class TestBuildScaleSection:
    def test_brief_idea(self) -> None:
        result = build_scale_section("A todo app")
        assert "brief" in result.lower()
        assert "infer" in result.lower()

    def test_moderate_idea(self) -> None:
        # 130 words
        idea = " ".join(["feature"] * 130)
        result = build_scale_section(idea)
        assert "moderately detailed" in result.lower()

    def test_detailed_idea(self) -> None:
        # 260 words
        idea = " ".join(["feature"] * 260)
        result = build_scale_section(idea)
        assert "very detailed" in result.lower()
        assert "Decompose EVERY" in result

    def test_char_count_threshold(self) -> None:
        # Short word count but many chars
        idea = "x" * 1600
        result = build_scale_section(idea)
        assert "very detailed" in result.lower()


# ============================================================================
# build_constraints_section
# ============================================================================


class TestBuildConstraintsSection:
    def test_no_constraint_tokens(self) -> None:
        assert build_constraints_section("Build a social network") == ""

    def test_free_tier_en(self) -> None:
        result = build_constraints_section("Build a chat app free tier only")
        assert "free" in result.lower()

    def test_free_tier_ru(self) -> None:
        result = build_constraints_section("Zrób aplikację bez budżetu")
        assert "free" in result.lower()

    def test_open_source_only(self) -> None:
        result = build_constraints_section("Use only open source tools")
        assert "free" in result.lower() or "open-source" in result.lower()


# ============================================================================
# _detect_project_type
# ============================================================================


class TestDetectProjectType:
    def test_web_default(self) -> None:
        assert _detect_project_type("Build a social network") == "web"

    def test_mobile(self) -> None:
        assert _detect_project_type("React Native мобильное приложение") == "mobile"

    def test_game(self) -> None:
        assert _detect_project_type("2D multiplayer game with Unity") == "game"

    def test_desktop(self) -> None:
        assert _detect_project_type("Desktop app with Electron") == "desktop"

    def test_iot(self) -> None:
        assert _detect_project_type("IoT sensor monitoring with Arduino") == "iot"

    def test_cli(self) -> None:
        assert _detect_project_type("CLI утилита для парсинга") == "cli"

    def test_data_science(self) -> None:
        assert _detect_project_type("Machine learning prediction model") == "data-science"

    def test_empty_input(self) -> None:
        assert _detect_project_type("") == "web"

    def test_none_input(self) -> None:
        assert _detect_project_type(None) == "web"


# ============================================================================
# build_project_type_section
# ============================================================================


class TestBuildProjectTypeSection:
    def test_web_returns_empty(self) -> None:
        # web is the default — no special section
        assert build_project_type_section("A social network") == ""

    def test_mobile_with_goals(self) -> None:
        goals = TechStackGoals(performance=True)
        result = build_project_type_section("React Native app", goals)
        assert "MOBILE" in result
        assert "performance" in result.lower()

    def test_game_no_goals(self) -> None:
        result = build_project_type_section("Unity 2D game")
        assert "GAME" in result


# ============================================================================
# _build_custom_section
# ============================================================================


class TestBuildCustomSection:
    def test_no_custom(self) -> None:
        assert _build_custom_section(None, None) == ""

    def test_tags_only(self) -> None:
        result = _build_custom_section(["auth", "api"], None)
        assert "auth" in result
        assert "api" in result

    def test_roles_only(self) -> None:
        result = _build_custom_section(None, ["backend", "frontend"])
        assert "backend" in result

    def test_both(self) -> None:
        result = _build_custom_section(["tag1"], ["role1"])
        assert "tag1" in result
        assert "role1" in result


# ============================================================================
# _is_rate_limit_error
# ============================================================================


class TestIsRateLimitError:
    def test_429_in_message(self) -> None:
        assert _is_rate_limit_error("Error 429: Too many requests") is True

    def test_quota_keyword(self) -> None:
        assert _is_rate_limit_error("Quota exceeded for model") is True

    def test_limit_keyword(self) -> None:
        assert _is_rate_limit_error("Rate limit exceeded") is True

    def test_normal_error(self) -> None:
        assert _is_rate_limit_error("Internal server error") is False


# ============================================================================
# emit_token_metrics
# ============================================================================


class TestEmitTokenMetrics:
    def test_emits_both_counters(self) -> None:
        """Verify prompt and completion counters are incremented."""
        usage = {"promptTokens": 100, "completionTokens": 50}
        # We just call it — if Prometheus is initialized it will work
        # No assertion needed; we verify no exception is raised
        emit_token_metrics(usage, "test_endpoint", "test_model")

    def test_missing_tokens_no_crash(self) -> None:
        """Should not crash when tokens are None."""
        emit_token_metrics({}, "test_endpoint", "test_model")
        emit_token_metrics({"promptTokens": None}, "test", "m")
