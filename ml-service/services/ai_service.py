"""
AI Service — business logic for AI generation endpoints.

Contains prompt management, provider integration, and generation functions.
Extracted from routers/ai.py to keep router thin.
"""

import asyncio
import json
import logging
import os
import time
from pathlib import Path
from typing import Any, List

import httpx
from pydantic import ValidationError
from fastapi import HTTPException

from clients.groq_client import GroqClient, GroqRateLimitError, GroqJSONParseError
from models.ai_models import (
    PlanDraft,
    PlanRequest,
    RefinePlanRequest,
    RefineTechStackRequest,
    TechStackGoals,
    TechStackRequest,
    TechStackResponse,
)
from metrics import (
    AI_CACHE_HITS,
    AI_CACHE_MISSES,
    AI_MODEL_LATENCY,
    AI_RATE_LIMITS,
    AI_TOKENS_TOTAL,
    AI_VALIDATION_FAILURES,
    AI_FALLBACK_TRIGGERS,
)
from services import ai_cache

logger = logging.getLogger(__name__)

# ============================================================================
# Provider Configuration
# ============================================================================

DEFAULT_PROVIDER = os.getenv("AI_PROVIDER", "groq")
DEFAULT_MODEL = os.getenv("AI_MODEL", "smart")

# Groq may return 413 if the user message + system prompt + long plan instructions exceed
# provider limits (especially for large models). Keep these conservative.
def _ai_plan_idea_max_chars() -> int:
    return int(os.getenv("AI_PLAN_MAX_IDEA_CHARS", "4500"))


def _ai_plan_tech_max_chars() -> int:
    return int(os.getenv("AI_PLAN_MAX_TECH_CHARS", "1200"))


def _truncate_for_llm(text: str | None, max_chars: int, field: str) -> str:
    if not text:
        return ""
    if len(text) <= max_chars:
        return text
    logger.warning("Truncated %s for LLM: %d -> %d chars", field, len(text), max_chars)
    return text[: max_chars - 1].rstrip() + "…"

GROQ_API_KEY = os.getenv("GROQ_API_KEY")
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY")

AI_NOT_CONFIGURED_MSG = "AI Service not configured (set GROQ_API_KEY)"

# Groq client (primary for MVP)
groq_client: GroqClient | None = None

if GROQ_API_KEY:
    groq_client = GroqClient(
        GROQ_API_KEY,
        on_rate_limit=lambda model: AI_RATE_LIMITS.labels(
            model=model, provider="groq"
        ).inc(),
        on_fallback=lambda from_m, to_m, reason: AI_FALLBACK_TRIGGERS.labels(
            from_model=from_m, to_model=to_m, reason=reason
        ).inc(),
    )
    logger.info("Groq client initialized")
else:
    logger.warning("GROQ_API_KEY not set. AI features will fail.")

# Gemini client (fallback)
genai = None
if GEMINI_API_KEY:
    try:
        import google.generativeai as genai_module

        genai_module.configure(api_key=GEMINI_API_KEY)
        genai = genai_module
        logger.info("Gemini configured as fallback")
    except ImportError:
        logger.warning("google-generativeai not installed, Gemini fallback unavailable")

# ============================================================================
# Prompt Management
# ============================================================================

PROMPT_ROOT = Path(__file__).resolve().parents[1] / "prompts"
PROMPT_CACHE: dict[tuple[str, str, str], str] = {}


def load_prompt_file(name: str, version: str, suffix: str, cache_key: str) -> str:
    key = (name, version, cache_key)
    if key in PROMPT_CACHE:
        return PROMPT_CACHE[key]
    filename = f"{version}.{suffix}"
    path = PROMPT_ROOT / name / filename
    if not path.exists():
        raise HTTPException(
            status_code=500,
            detail=f"Prompt asset not found: {name}/{filename}",
        )
    content = path.read_text(encoding="utf-8")
    PROMPT_CACHE[key] = content
    return content


def load_prompt(name: str, version: str) -> str:
    return load_prompt_file(name, version, "prompt.txt", "prompt")


def load_schema(name: str, version: str) -> str:
    return load_prompt_file(name, version, "schema.json", "schema")


def render_prompt(template: str, **kwargs: str) -> str:
    rendered = template
    for key, value in kwargs.items():
        rendered = rendered.replace(f"{{{{{key}}}}}", value)
    return rendered


# ============================================================================
# Prompt Building Helpers
# ============================================================================

# Goal flag to description mapping
_GOAL_DESCRIPTIONS: dict[str, str] = {
    "performance": "Performance — prioritize fast execution, low latency, and efficient resource usage for this specific project type",
    "cost": "Cost efficiency — minimize hosting and operational costs, prefer free tiers and self-hosted options",
    "developerSpeed": "Developer speed — fast development, rapid iteration, minimal boilerplate",
    "scalability": "Scalability — handle growth in users, data volume, and traffic",
    "security": "Security — protect sensitive data, strong auth, audit trails",
    "maintainability": "Maintainability — clean architecture, easy to extend, well-tested",
}


def build_goals_section(goals: TechStackGoals | None) -> str:
    """Build goals section for prompts from request goals."""
    if not goals:
        return ""
    active_goals = [
        desc
        for attr, desc in _GOAL_DESCRIPTIONS.items()
        if getattr(goals, attr, False)
    ]
    if not active_goals:
        return ""
    return (
        "\nProject goals (the user cares about these — factor them into your recommendations):\n- "
        + "\n- ".join(active_goals)
    )


def build_scale_section(idea: str) -> str:
    idea_text = idea or ""
    word_count = len(idea_text.split())
    char_count = len(idea_text)

    if word_count >= 250 or char_count >= 1500:
        return "\nThe idea description is very detailed. Decompose EVERY mentioned feature into granular engineering tasks. Do not summarize — each feature should produce 3-10+ tasks."
    elif word_count >= 120 or char_count >= 800:
        return "\nThe idea is moderately detailed. Decompose all mentioned features into granular tasks and infer necessary supporting components (auth, admin, settings, notifications, etc.)."
    else:
        return "\nThe idea description is brief. This does NOT mean the project is small. Infer ALL implied features (auth, profiles, UI pages, API, data models, admin panel, settings, notifications, search, testing, deployment) and decompose each into granular engineering tasks."


def build_constraints_section(idea: str) -> str:
    idea_lower = (idea or "").lower()
    free_tokens = [
        "only free", "free only", "free-only", "free tier only", "free-tier only",
        "free tier", "open source only", "open-source only", "only open source",
        "only open-source", "no budget", "zero budget", "without budget",
        "no paid", "no paid tools", "bez budżetu", "нет бюджета", "без денег",
        "только бесплат", "только фри", "только опенсорс",
        "только open source", "только open-source",
    ]
    if any(token in idea_lower for token in free_tokens):
        return (
            "\nConstraints: User requests free/open-source tooling or free-tier services. "
            "Avoid paid-only SaaS; include free/self-hosted alternatives where possible."
        )
    return ""


def _build_custom_section(
    custom_tags: List[str] | None, custom_roles: List[str] | None
) -> str:
    """Build custom tags/roles section for plan prompt."""
    parts = []
    if custom_tags:
        parts.append(f"\nCustom tags to use (in addition to defaults): {', '.join(custom_tags)}")
    if custom_roles:
        parts.append(f"\nCustom roles for tasks: {', '.join(custom_roles)}")
    return "".join(parts)


def _detect_project_type(idea: str) -> str:
    """Detect project type from idea text to guide stack recommendations."""
    idea_lower = (idea or "").lower()
    type_tokens: dict[str, list[str]] = {
        "mobile": [
            "mobile", "mobiln", "ios", "android", "react native", "flutter",
            "приложение для telefon", "mobilnое приложение", "app store",
            "google play", "phone app", "tablet", "tablet", "смартфон", "smartphone",
        ],
        "data-science": [
            "machine learning", "ml", "data science", "sieć neuronowa", "neural",
            "tensorflow", "pytorch", "модель", "обучени", "training",
            "prediction", "предсказан", "классификац", "regression",
            "nlp", "computer vision", "deep learning", "dataset",
        ],
        "game": [
            "game", "gra", "unity", "unreal", "godot", "геймплей",
            "gameplay", "2d", "3d", "multiplayer", "мультиплеер",
        ],
        "desktop": [
            "desktop", "desktopow", "stacjonarn", "electron", "tauri",
            ".net maui", "wpf", "winforms", "qt",
        ],
        "iot": [
            "iot", "embedded", "wbudowan", "mikrokontroler", "arduino",
            "raspberry", "датчик", "sensor", "firmware",
        ],
        "cli": ["cli", "command line", "terminal", "konsolow", "утилит"],
    }
    for ptype, tokens in type_tokens.items():
        if any(t in idea_lower for t in tokens):
            return ptype
    return "web"


# Platform info for project type hints (with goal-specific guidance)
_PLATFORM_INFO: dict[str, dict] = {
    "mobile": {
        "base": "This is a MOBILE application. Focus on mobile frameworks and mobile-friendly backends.",
        "goals": {
            "performance": "For mobile performance: prefer native (Swift/Kotlin) or near-native (Flutter). Avoid heavy server-side frameworks.",
            "cost": "For mobile cost efficiency: React Native or Flutter allow one codebase for iOS+Android. Firebase/Supabase minimize backend costs.",
            "developerSpeed": "For mobile dev speed: React Native (if team knows React) or Flutter (fast UI). BaaS like Firebase reduces backend work.",
            "scalability": "For mobile scalability: scalable backend matters (Node.js, Go, .NET), but the mobile client itself must handle offline/caching well.",
            "security": "For mobile security: secure API communication (certificate pinning, token refresh), encrypted local storage, biometric auth.",
        },
    },
    "data-science": {
        "base": "This is a DATA SCIENCE / ML project. Python ecosystem is the standard.",
        "goals": {
            "performance": "For ML performance: GPU-accelerated frameworks (PyTorch, TensorFlow). For serving: FastAPI, TorchServe, or ONNX Runtime.",
            "cost": "For ML cost: use lightweight models, CPU inference where possible, Hugging Face free tier, or self-hosted.",
            "developerSpeed": "For ML dev speed: Jupyter + scikit-learn for prototyping, Hugging Face for pre-trained models, Streamlit for quick demos.",
            "scalability": "For ML scalability: model serving with Ray Serve, Kubernetes, or cloud ML platforms. Data pipeline with Apache Spark or Dask.",
        },
    },
    "game": {
        "base": "This is a GAME project. Use game engines and game-appropriate networking.",
        "goals": {
            "performance": "For game performance: C++ (Unreal) or C# (Unity) for rendering-intensive games. Godot for lighter 2D games.",
            "cost": "For game cost: Godot (free, open source) or Unity (free tier). Avoid Unreal licensing costs for small projects.",
            "developerSpeed": "For game dev speed: Unity or Godot have the fastest iteration. Visual scripting options available.",
        },
    },
    "desktop": {
        "base": "This is a DESKTOP application. Use desktop-native or cross-platform desktop frameworks.",
        "goals": {
            "performance": "For desktop performance: native (C++/Qt, C#/.NET MAUI) or Tauri (Rust backend). Avoid Electron if performance is critical.",
            "cost": "For desktop cost: Electron or Tauri for cross-platform from one codebase. .NET MAUI if team knows C#.",
            "developerSpeed": "For desktop dev speed: Electron (web tech reuse) or Tauri (lighter). Qt for complex native UIs.",
        },
    },
    "iot": {
        "base": "This is an IoT / EMBEDDED project. Use embedded-friendly languages and protocols.",
        "goals": {
            "performance": "For IoT performance: C/C++ or Rust for firmware. MQTT/CoAP for efficient communication. Minimal memory footprint.",
            "cost": "For IoT cost: MicroPython for prototyping, ESP32/Arduino for cheap hardware. MQTT brokers are lightweight.",
            "security": "For IoT security: TLS for communication, secure boot, OTA update signing, device identity management.",
        },
    },
    "cli": {
        "base": "This is a CLI / TERMINAL tool.",
        "goals": {
            "performance": "For CLI performance: Go or Rust compile to fast single binaries.",
            "developerSpeed": "For CLI dev speed: Python (Click/Typer) or Node.js (Commander) for rapid development. Go for simple deployment.",
        },
    },
}


def build_project_type_section(idea: str, goals: TechStackGoals | None = None) -> str:
    """Build project type hint with goal-specific guidance per platform."""
    ptype = _detect_project_type(idea)
    info = _PLATFORM_INFO.get(ptype)
    if not info:
        return ""
    parts = [f"\nProject type detected: {info['base']}"]
    if goals:
        goal_hints = info.get("goals", {})
        for attr in (
            "performance", "cost", "developerSpeed",
            "scalability", "security", "maintainability",
        ):
            if getattr(goals, attr, False) and attr in goal_hints:
                parts.append(goal_hints[attr])
    return "\n".join(parts)


# ============================================================================
# JSON Parsing
# ============================================================================


def _has_valid_json_braces(start: int, end: int) -> bool:
    return start != -1 and end != -1 and end > start


def parse_json_response(text: str) -> dict:
    if not text:
        raise ValueError("Empty response text")
    cleaned = text.strip()
    if cleaned.startswith("```"):
        cleaned = cleaned.replace("```json", "").replace("```", "").strip()
    start = cleaned.find("{")
    end = cleaned.rfind("}")
    if _has_valid_json_braces(start, end):
        cleaned = cleaned[start : end + 1]
    return json.loads(cleaned)


def _is_rate_limit_error(error_str: str) -> bool:
    error_lower = error_str.lower()
    return "429" in error_str or "quota" in error_lower or "limit" in error_lower


# ============================================================================
# Token Metrics Helper
# ============================================================================


def emit_token_metrics(usage: dict, endpoint: str, model: str) -> None:
    """Emit Prometheus token consumption metrics."""
    prompt_tokens = usage.get("promptTokens")
    completion_tokens = usage.get("completionTokens")
    if prompt_tokens:
        AI_TOKENS_TOTAL.labels(
            endpoint=endpoint, token_type="prompt", model=model
        ).inc(prompt_tokens)
    if completion_tokens:
        AI_TOKENS_TOTAL.labels(
            endpoint=endpoint, token_type="completion", model=model
        ).inc(completion_tokens)


# ============================================================================
# Helpers
# ============================================================================


def require_groq_client() -> GroqClient:
    """Ensure groq_client is available, raises HTTPException if not."""
    if not groq_client:
        raise HTTPException(status_code=500, detail=AI_NOT_CONFIGURED_MSG)
    return groq_client


def require_any_ai_client() -> None:
    """Ensure at least one AI client is available."""
    if not groq_client and not GEMINI_API_KEY:
        raise HTTPException(status_code=500, detail=AI_NOT_CONFIGURED_MSG)


def get_model_name() -> str:
    """Get the actual model name for response metadata."""
    if groq_client:
        return groq_client.MODELS.get(DEFAULT_MODEL, DEFAULT_MODEL)
    return DEFAULT_MODEL


def handle_validation_error(error: ValidationError, context: str) -> None:
    """Handle validation errors from AI response parsing."""
    AI_VALIDATION_FAILURES.labels(endpoint=context, error_type="validation").inc()
    logger.warning("%s validation failed: %s", context, error)
    raise HTTPException(
        status_code=500,
        detail={"error": "AI response did not match schema", "details": error.errors()},
    ) from error


# ============================================================================
# Model Calling (with cache + metrics)
# ============================================================================


async def _call_groq_model(
    prompt: str, model_name: str, max_tokens: int = 4000
) -> tuple[dict, dict]:
    """Call Groq API and handle Groq-specific errors."""
    try:
        return await groq_client.generate_json(
            prompt, model=model_name, max_tokens=max_tokens
        )
    except httpx.HTTPStatusError as e:
        if e.response is not None and e.response.status_code == 413:
            logger.error(
                "Groq 413 Payload Too Large (prompt_len=%d, model=%s). "
                "Shorten the idea or lower AI_PLAN_MAX_IDEA_CHARS / set AI_MODEL=fast",
                len(prompt),
                model_name,
            )
            raise HTTPException(
                status_code=400,
                detail=(
                    "The AI request is too large for the provider (HTTP 413). "
                    "Shorten the project description, or set AI_PLAN_MAX_IDEA_CHARS in ml-service (e.g. 3000) and restart."
                ),
            ) from e
        raise
    except GroqRateLimitError as e:
        logger.warning("Groq rate limit: %s", e)
        raise HTTPException(
            status_code=429,
            detail="AI Service rate limit exceeded. Please try again later.",
        ) from e
    except GroqJSONParseError as e:
        AI_VALIDATION_FAILURES.labels(endpoint="call_model", error_type="json_parse").inc()
        logger.error("Groq JSON parse error: %s", e)
        raise HTTPException(
            status_code=500,
            detail="Failed to parse AI response. Please try again.",
        ) from e


def _call_gemini_sync(prompt: str, model_name: str) -> tuple[dict, dict]:
    """Synchronous Gemini API call (runs in thread pool)."""
    model = genai.GenerativeModel(
        model_name if "gemini" in model_name else "gemini-2.0-flash"
    )
    response = model.generate_content(prompt)
    usage = getattr(response, "usage_metadata", None)
    usage_info = {
        "promptTokens": getattr(usage, "prompt_token_count", None),
        "completionTokens": getattr(usage, "candidates_token_count", None),
        "totalTokens": getattr(usage, "total_token_count", None),
    }
    result = parse_json_response(response.text)
    return result, usage_info


async def _call_gemini_model(prompt: str, model_name: str) -> tuple[dict, dict]:
    """Call Gemini API and handle Gemini-specific errors."""
    try:
        return await asyncio.to_thread(_call_gemini_sync, prompt, model_name)
    except Exception as e:
        if _is_rate_limit_error(str(e)):
            logger.warning("Gemini quota exhausted: %s", e)
            raise HTTPException(
                status_code=429,
                detail="AI Service rate limit exceeded. Please try again later.",
            ) from e
        raise


async def call_model(
    prompt: str,
    model_name: str,
    max_tokens: int = 4000,
    endpoint: str = "unknown",
) -> tuple[dict, dict]:
    """Call AI model to generate JSON response.

    Uses Groq as primary provider (MVP).
    Falls back to Gemini if Groq is not configured.
    Integrates with Redis cache and Prometheus metrics.

    Args:
        prompt: The prompt to send to the model.
        model_name: Model alias ("smart", "fast") or full model name.
        max_tokens: Maximum tokens to generate.
        endpoint: Endpoint name for metrics labels.

    Returns:
        Tuple of (parsed JSON response, usage metadata).
    """
    cache_key = f"{DEFAULT_PROVIDER}:{model_name}:{prompt}"

    # Check Redis cache
    cached = await ai_cache.get(cache_key)
    if cached:
        AI_CACHE_HITS.labels(endpoint=endpoint).inc()
        return cached

    AI_CACHE_MISSES.labels(endpoint=endpoint).inc()
    start_time = time.perf_counter()

    if groq_client:
        result, usage_info = await _call_groq_model(
            prompt, model_name, max_tokens=max_tokens
        )
    elif genai and GEMINI_API_KEY:
        result, usage_info = await _call_gemini_model(prompt, model_name)
    else:
        raise HTTPException(
            status_code=500,
            detail="No AI provider configured. Set GROQ_API_KEY.",
        )

    # Record metrics
    duration = time.perf_counter() - start_time
    actual_model = usage_info.get("model", model_name)
    AI_MODEL_LATENCY.labels(model=actual_model, endpoint=endpoint).observe(duration)
    emit_token_metrics(usage_info, endpoint, actual_model)

    # Store in Redis cache
    await ai_cache.set(cache_key, result, usage_info)

    return result, usage_info


# ============================================================================
# Generation Functions
# ============================================================================


async def generate_tech_stack_draft(request: TechStackRequest) -> TechStackResponse:
    """Generate tech stack recommendations for a project idea."""
    require_any_ai_client()

    version = os.getenv("AI_TECH_STACK_PROMPT_VERSION", "v1")
    goals_section = build_goals_section(request.goals)
    constraints_section = build_constraints_section(request.idea)
    project_type_section = build_project_type_section(request.idea, request.goals)

    prompt = render_prompt(
        load_prompt("techstack", version),
        idea=request.idea,
        schema=load_schema("techstack", version),
        goalsSection=goals_section,
        constraintsSection=constraints_section,
        projectTypeSection=project_type_section,
    )

    try:
        data, usage = await call_model(prompt, DEFAULT_MODEL, endpoint="tech_stack")
        data["version"] = version
        data["promptVersion"] = version
        data["provider"] = DEFAULT_PROVIDER
        data["model"] = get_model_name()
        data.update(usage)
        return TechStackResponse.model_validate(data)
    except HTTPException:
        raise
    except ValidationError as e:
        handle_validation_error(e, "tech_stack")
    except Exception as e:
        logger.exception("Tech stack generation failed")
        raise HTTPException(
            status_code=500, detail="Failed to generate tech stack"
        ) from e


def _fix_stringified_items(data: dict, key: str) -> None:
    """AI models sometimes double-serialize nested objects as JSON strings. Fix them."""
    items = data.get(key)
    if not isinstance(items, list):
        return
    for i, item in enumerate(items):
        if isinstance(item, str):
            try:
                items[i] = json.loads(item)
            except (json.JSONDecodeError, TypeError):
                pass
        # Also fix nested tasks if they're stringified
        if isinstance(items[i], dict) and "tasks" in items[i]:
            tasks = items[i]["tasks"]
            if isinstance(tasks, list):
                for j, task in enumerate(tasks):
                    if isinstance(task, str):
                        try:
                            tasks[j] = json.loads(task)
                        except (json.JSONDecodeError, TypeError):
                            pass


async def generate_plan_draft(request: PlanRequest) -> PlanDraft:
    """Generate development plan for a project idea."""
    require_any_ai_client()

    version = os.getenv("AI_PLAN_PROMPT_VERSION", "v1")
    idea = _truncate_for_llm(
        request.idea, _ai_plan_idea_max_chars(), "idea"
    )
    tech_stack = _truncate_for_llm(
        request.techStack, _ai_plan_tech_max_chars(), "techStack"
    )
    custom_section = _build_custom_section(request.customTags, request.customRoles)
    scale_section = build_scale_section(idea)
    goals_section = build_goals_section(request.goals)

    prompt = render_prompt(
        load_prompt("plan", version),
        idea=idea,
        techStack=tech_stack,
        schema=load_schema("plan", version),
        customSection=custom_section,
        scaleSection=scale_section,
        goalsSection=goals_section,
    )

    try:
        data, usage = await call_model(
            prompt,
            DEFAULT_MODEL,
            max_tokens=int(os.getenv("AI_PLAN_MAX_OUTPUT_TOKENS", "6000")),
            endpoint="plan",
        )
        data["version"] = version
        data["promptVersion"] = version
        data["idea"] = request.idea
        data["techStack"] = request.techStack
        data["provider"] = DEFAULT_PROVIDER
        data["model"] = DEFAULT_MODEL
        data.update(usage)
        _fix_stringified_items(data, "phases")
        return PlanDraft.model_validate(data)
    except HTTPException:
        raise
    except ValidationError as e:
        handle_validation_error(e, "plan")
    except Exception as e:
        logger.exception("Plan generation failed")
        raise HTTPException(
            status_code=500, detail="Failed to generate plan"
        ) from e


async def refine_plan_draft(request: RefinePlanRequest) -> PlanDraft:
    """Refine an existing plan based on user feedback."""
    require_any_ai_client()

    version = os.getenv("AI_PLAN_PROMPT_VERSION", "v1")
    idea = _truncate_for_llm(
        request.idea, _ai_plan_idea_max_chars(), "idea"
    )
    tech_stack = _truncate_for_llm(
        request.techStack, _ai_plan_tech_max_chars(), "techStack"
    )
    instr = _truncate_for_llm(
        request.instructions, int(os.getenv("AI_PLAN_MAX_REFINE_INSTRUCTION_CHARS", "8000")),
        "instructions",
    )
    current_raw = json.dumps(request.currentPlan, ensure_ascii=False, indent=2)
    max_cp = int(os.getenv("AI_PLAN_MAX_REFINE_CURRENT_JSON_CHARS", "120000"))
    if len(current_raw) > max_cp:
        raise HTTPException(
            status_code=400,
            detail="The current plan is too large to refine in one request. Shorten the description and generate again, or split refinements into smaller steps.",
        )
    custom_section = _build_custom_section(request.customTags, request.customRoles)
    goals_section = build_goals_section(request.goals)

    prompt = render_prompt(
        load_prompt_file("plan", version, "refine.prompt.txt", "refine_prompt"),
        idea=idea,
        techStack=tech_stack,
        schema=load_schema("plan", version),
        customSection=custom_section,
        goalsSection=goals_section,
        currentPlan=current_raw,
        instructions=instr,
    )

    try:
        data, usage = await call_model(
            prompt,
            DEFAULT_MODEL,
            max_tokens=int(os.getenv("AI_PLAN_MAX_OUTPUT_TOKENS", "6000")),
            endpoint="refine_plan",
        )
        data["version"] = version
        data["promptVersion"] = version
        data["idea"] = request.idea
        data["techStack"] = request.techStack
        data["provider"] = DEFAULT_PROVIDER
        data["model"] = DEFAULT_MODEL
        data.update(usage)
        _fix_stringified_items(data, "phases")
        return PlanDraft.model_validate(data)
    except HTTPException:
        raise
    except ValidationError as e:
        handle_validation_error(e, "refine_plan")
    except Exception as e:
        logger.exception("Plan refinement failed")
        raise HTTPException(
            status_code=500, detail="Failed to refine plan"
        ) from e


async def refine_tech_stack_draft(
    request: RefineTechStackRequest,
) -> TechStackResponse:
    """Refine existing tech stack options based on user feedback."""
    require_any_ai_client()

    version = os.getenv("AI_TECH_STACK_PROMPT_VERSION", "v1")
    goals_section = build_goals_section(request.goals)

    prompt = render_prompt(
        load_prompt_file("techstack", version, "refine.prompt.txt", "refine_prompt"),
        idea=request.idea,
        schema=load_schema("techstack", version),
        goalsSection=goals_section,
        currentOptions=json.dumps(
            request.currentOptions, ensure_ascii=False, indent=2
        ),
        instructions=request.instructions,
    )

    try:
        data, usage = await call_model(
            prompt, DEFAULT_MODEL, endpoint="refine_tech_stack"
        )
        data["version"] = version
        data["promptVersion"] = version
        data["provider"] = DEFAULT_PROVIDER
        data["model"] = get_model_name()
        data.update(usage)
        return TechStackResponse.model_validate(data)
    except HTTPException:
        raise
    except ValidationError as e:
        handle_validation_error(e, "refine_tech_stack")
    except Exception as e:
        logger.exception("Tech stack refinement failed")
        raise HTTPException(
            status_code=500, detail="Failed to refine tech stack"
        ) from e
