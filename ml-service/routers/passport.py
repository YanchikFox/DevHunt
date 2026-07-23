"""
Project Passport router — generates persistent AI context artifacts.

Provides a structured "passport" with sections: overview, tech_stack,
architecture, roadmap, decisions.  These are stored by Core API and
injected into subsequent AI chat sessions so the assistant retains
project knowledge beyond the initial planning phase.
"""

import asyncio
import json
import logging
import re
import time
from typing import List

from fastapi import APIRouter, Depends, HTTPException
from security import verify_token

from clients.groq_client import GroqClient, GroqRateLimitError
from metrics import AI_MODEL_LATENCY
from models.ai_models import (
    PassportRequest,
    PassportResponse,
    PassportSection,
)
from services.ai_service import (
    DEFAULT_PROVIDER,
    GEMINI_API_KEY,
    GROQ_API_KEY,
    genai,
    groq_client,
    require_groq_client,
    emit_token_metrics,
)
logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["AI Passport"], dependencies=[Depends(verify_token)])


# ============================================================================
# Mermaid sanitization for embedded code blocks
# ============================================================================

def _sanitize_mermaid_code(code: str) -> str:
    """Sanitize Mermaid diagram code — fix common LLM output issues."""
    lines = []
    for line in code.splitlines():
        # Remove HTML tags that break Mermaid rendering
        cleaned = re.sub(r"<br\s*/?>", " ", line)
        cleaned = re.sub(r"<[^>]+>", "", cleaned)
        # Escape problematic characters in labels
        cleaned = cleaned.replace("&", "&amp;")
        lines.append(cleaned)
    return "\n".join(lines)


def _sanitize_mermaid_in_markdown(content: str) -> str:
    """Find and sanitize embedded Mermaid code blocks within markdown content."""
    def _replace_block(match: re.Match) -> str:
        mermaid_code = match.group(1)
        sanitized = _sanitize_mermaid_code(mermaid_code)
        return f"```mermaid\n{sanitized}\n```"

    return re.sub(
        r'```mermaid\n([\s\S]*?)```',
        _replace_block,
        content,
    )


# ============================================================================
# Section prompts
# ============================================================================

_SECTION_SPECS: list[dict[str, str]] = [
    {
        "type": "overview",
        "title": "Project Overview",
        "instruction": (
            "Write a concise project overview (150-250 words). Include: "
            "what the project does, its main goals, target audience, "
            "key value proposition, and any constraints or assumptions."
        ),
    },
    {
        "type": "tech_stack",
        "title": "Technology Stack",
        "instruction": (
            "Describe the chosen technology stack. For each major component "
            "(frontend, backend, database, infrastructure, etc.) explain: "
            "what technology is used and WHY it was chosen for this project. "
            "Mention key libraries/frameworks. Format as a structured list."
        ),
    },
    {
        "type": "architecture",
        "title": "System Architecture",
        "instruction": (
            "Describe the high-level system architecture. Include: "
            "main components/services, how they communicate, data flow, "
            "external integrations, and deployment model. "
            "After the text description, include a Mermaid flowchart diagram "
            "(flowchart TD) illustrating the architecture. "
            "Wrap the Mermaid code in a ```mermaid code block.\n"
            "CRITICAL MERMAID SYNTAX RULES:\n"
            "- NEVER use parentheses () inside square brackets []. "
            "Wrong: node[CDN (Static)]. Correct: node[\"CDN - Static Assets\"].\n"
            "- NEVER use reserved words as class names: end, start, default. "
            "Use 'finish', 'begin', 'dflt' instead.\n"
            "- Always quote labels containing special characters with double quotes.\n"
            "- In subgraph titles, always use double quotes: subgraph \"My Group\".\n"
            "- For ER diagrams: attribute syntax is 'type name constraint'. "
            "Correct: string email PK, uuid id PK. Wrong: uuid PK (missing name)."
        ),
    },
    {
        "type": "roadmap",
        "title": "Development Roadmap",
        "instruction": (
            "Create a development roadmap based on the project phases. "
            "For each phase: name, goals, expected deliverables, "
            "key milestones, and dependencies on other phases. "
            "Include a summary timeline overview."
        ),
    },
    {
        "type": "decisions",
        "title": "Key Decisions",
        "instruction": (
            "List 5-8 key architectural and technical decisions for this project. "
            "For each decision: what was decided, WHY (rationale), "
            "what alternatives were considered, and any trade-offs. "
            "Format as numbered entries."
        ),
    },
]


def _build_passport_prompt(req: PassportRequest) -> str:
    """Build the full passport generation prompt."""
    phases_text = ""
    if req.phases:
        phase_lines = []
        for phase in req.phases:
            goals_str = ", ".join(phase.goals) if phase.goals else "N/A"
            phase_lines.append(
                f"- {phase.name}: {phase.description} "
                f"(goals: {goals_str}, tasks: {phase.taskCount})"
            )
        phases_text = "\n".join(phase_lines)

    section_instructions = "\n\n".join(
        f"### Section: {spec['title']} (type: {spec['type']})\n{spec['instruction']}"
        for spec in _SECTION_SPECS
    )

    desc_line = f"ADDITIONAL DESCRIPTION: {req.description}" if req.description else ""
    phases_line = f"DEVELOPMENT PHASES:\n{phases_text}" if phases_text else ""
    docs_line = ""
    if req.documentsContext:
        docs_line = f"EXISTING PROJECT DOCUMENTS (use as context, do not repeat verbatim):\n{req.documentsContext[:4000]}"
    lang_name = "Russian" if req.language == "ru" else "English"

    return f"""Generate a complete Project Passport for the following project.

PROJECT IDEA: {req.idea}
TECHNOLOGY STACK: {req.techStack}
{desc_line}
{phases_line}
{docs_line}

---

Generate ALL of the following sections. For each section, provide content in markdown format.
Write in {lang_name}.

{section_instructions}

---

Return a JSON object with a "sections" array. Each element must have:
- "type": the section type identifier (overview, tech_stack, architecture, roadmap, decisions)
- "title": section display title
- "content": the markdown content

Return ONLY valid JSON, no markdown code blocks, no explanation."""


@router.post("/generate-passport", response_model=PassportResponse)
async def generate_passport(request: PassportRequest) -> PassportResponse:
    """Generate a full project passport with all knowledge sections."""
    client = require_groq_client()

    prompt = _build_passport_prompt(request)

    max_retries = 3
    start = time.time()
    for attempt in range(max_retries):
        try:
            result, usage_info = await client.generate(
                prompt=prompt,
                system_prompt=(
                    "You are a senior software architect creating a structured "
                    "project knowledge base document. Output ONLY valid JSON."
                ),
                model="smart",
            )
            break
        except GroqRateLimitError:
            if attempt < max_retries - 1:
                wait = (attempt + 1) * 10
                logger.warning("Rate limit hit, retrying in %ds (attempt %d/%d)", wait, attempt + 1, max_retries)
                await asyncio.sleep(wait)
                continue
            raise HTTPException(status_code=429, detail="AI service rate limit exceeded, please try again later")

    try:
        latency = time.time() - start
        model_used = usage_info.get("model", "unknown")
        AI_MODEL_LATENCY.labels(model=model_used, endpoint="generate-passport").observe(latency)
        emit_token_metrics(usage_info, "generate-passport", model_used)

        # Parse JSON response
        cleaned = result.strip()
        if cleaned.startswith("```"):
            cleaned = cleaned.split("\n", 1)[-1].rsplit("```", 1)[0]

        parsed = json.loads(cleaned)
        raw_sections = parsed.get("sections", parsed if isinstance(parsed, list) else [])

        sections: List[PassportSection] = []
        for raw in raw_sections:
            if isinstance(raw, dict) and "type" in raw and "content" in raw:
                content = raw["content"]
                # Sanitize embedded Mermaid code blocks in architecture sections
                if raw["type"] in ("architecture", "architecture_diagram"):
                    content = _sanitize_mermaid_in_markdown(content)
                sections.append(
                    PassportSection(
                        type=raw["type"],
                        title=raw.get("title", raw["type"]),
                        content=content,
                    )
                )

        if not sections:
            raise ValueError("No valid sections in AI response")

        return PassportResponse(
            sections=sections,
            provider=usage_info.get("provider", DEFAULT_PROVIDER),
            model=model_used,
            promptTokens=usage_info.get("prompt_tokens"),
            completionTokens=usage_info.get("completion_tokens"),
            totalTokens=usage_info.get("total_tokens"),
        )
    except json.JSONDecodeError as exc:
        logger.error("Passport JSON parse error: %s", exc)
        raise HTTPException(status_code=502, detail="Invalid AI response format") from exc
    except ValueError as exc:
        logger.error("Passport validation error: %s", exc)
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    except Exception as exc:
        logger.error("Passport generation failed: %s", exc)
        raise HTTPException(status_code=502, detail="AI service error") from exc
