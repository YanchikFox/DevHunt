"""
AI Planning router — thin endpoint layer.

Business logic lives in:
  - services/ai_service.py          (tech-stack & plan generation)
  - services/diagram_service.py     (diagram prompts, Mermaid sanitization)
  - services/agent_chat_service.py  (agent chat orchestration, content sanitization)
  - models/ai_models.py             (Pydantic request/response models)
"""

import logging
import time
from typing import List

from fastapi import APIRouter, Depends, HTTPException

from security import verify_token

from clients.groq_client import GroqClient
from models.ai_models import (
    AgentChatRequest,
    AgentChatResponse,
    ChatRequest,
    ChatResponse,
    DiagramRequest,
    DiagramResponse,
    PlanDraft,
    PlanRequest,
    RefinePlanRequest,
    RefineTechStackRequest,
    RoadmapPhase,
    TaskGenerationRequest,
    TaskItem,
    TechStackOption,
    TechStackRequest,
    TechStackResponse,
)
from metrics import AI_MODEL_LATENCY
from services.ai_service import (
    DEFAULT_PROVIDER,
    GEMINI_API_KEY,
    GROQ_API_KEY,
    genai,
    generate_plan_draft,
    generate_tech_stack_draft,
    refine_plan_draft,
    refine_tech_stack_draft,
    require_groq_client,
    emit_token_metrics,
)
from services.diagram_service import generate_diagram_from_request
from services.agent_chat_service import handle_agent_chat

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/ai", tags=["AI Planning"], dependencies=[Depends(verify_token)])

_MAX_CHAT_HISTORY = 10


# ============================================================================
# Diagnostic Endpoint
# ============================================================================


@router.get("/models")
async def list_available_models(user=Depends(verify_token)):
    """Diagnostic endpoint to see available models. Requires authentication."""
    result = {
        "provider": DEFAULT_PROVIDER,
        "groq": {
            "configured": GROQ_API_KEY is not None,
            "models": list(GroqClient.MODELS.values()) if GROQ_API_KEY else [],
        },
        "gemini": {
            "configured": GEMINI_API_KEY is not None,
            "models": [],
        },
    }
    if genai and GEMINI_API_KEY:
        try:
            result["gemini"]["models"] = [
                m.name
                for m in genai.list_models()
                if "generateContent" in m.supported_generation_methods
            ]
        except Exception as e:
            result["gemini"]["error"] = str(e)
    return result


# ============================================================================
# Tech Stack & Plan Generation
# ============================================================================


@router.post("/generate-tech-stack", response_model=TechStackResponse)
async def generate_tech_stack(request: TechStackRequest):
    return await generate_tech_stack_draft(request)


@router.post("/generate-plan", response_model=PlanDraft)
async def generate_plan(request: PlanRequest):
    return await generate_plan_draft(request)


@router.post("/refine-plan", response_model=PlanDraft)
async def refine_plan(request: RefinePlanRequest):
    """Refine an existing development plan based on user instructions."""
    return await refine_plan_draft(request)


@router.post("/refine-tech-stack", response_model=TechStackResponse)
async def refine_tech_stack(request: RefineTechStackRequest):
    """Refine existing tech stack recommendations based on user instructions."""
    return await refine_tech_stack_draft(request)


# ============================================================================
# Diagram Generation (logic in services/diagram_service.py)
# ============================================================================


@router.post("/generate-diagram", response_model=DiagramResponse)
async def generate_diagram(request: DiagramRequest):
    """Generate architecture diagram in Mermaid or PlantUML format."""
    return await generate_diagram_from_request(request)


# ============================================================================
# AI Chat
# ============================================================================


@router.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest):
    """AI Chat endpoint with conversation history (sliding window)."""
    client = require_groq_client()

    history = (
        request.history[-_MAX_CHAT_HISTORY:]
        if len(request.history) > _MAX_CHAT_HISTORY
        else request.history
    )
    messages = [{"role": msg.role, "content": msg.content} for msg in history]
    messages.append({"role": "user", "content": request.message})

    try:
        start = time.perf_counter()
        result, usage = await client.chat(
            messages=messages,
            context=request.context,
            model="smart",
            language=request.language,
        )
        model_name = client.MODELS.get("smart", "smart")
        AI_MODEL_LATENCY.labels(model=model_name, endpoint="chat").observe(
            time.perf_counter() - start
        )
        emit_token_metrics(usage, "chat", model_name)

        return ChatResponse(
            message=result,
            provider=DEFAULT_PROVIDER,
            model=model_name,
            promptTokens=usage.get("promptTokens"),
            completionTokens=usage.get("completionTokens"),
            totalTokens=usage.get("totalTokens"),
        )
    except Exception as e:
        logger.exception("Chat failed")
        raise HTTPException(
            status_code=500, detail="Chat request failed"
        ) from e


# ============================================================================
# Agentic AI Chat (logic in services/agent_chat_service.py)
# ============================================================================


@router.post("/chat-agent", response_model=AgentChatResponse)
async def chat_agent(request: AgentChatRequest):
    """Agentic AI Chat endpoint with function calling and tool execution loop."""
    return await handle_agent_chat(request)


@router.get("/tools")
async def list_tools():
    """List available tools for the AI agent."""
    from tools import TOOLS, TOOL_DISPLAY_INFO

    result = []
    for tool in TOOLS:
        name = tool["function"]["name"]
        display = TOOL_DISPLAY_INFO.get(name, {})
        result.append(
            {
                "name": name,
                "description": tool["function"]["description"],
                "category": display.get("category", "other"),
                "icon": display.get("icon", "\U0001f527"),
                "label_en": display.get("label_en", name),
                "label_ru": display.get("label_ru", name),
                "parameters": tool["function"]["parameters"],
            }
        )
    return {"tools": result}


# ============================================================================
# Legacy Endpoints (kept for compatibility)
# ============================================================================


@router.post("/tech-stack", response_model=List[TechStackOption])
async def suggest_tech_stack(request: TechStackRequest):
    draft = await generate_tech_stack_draft(request)
    return draft.options


@router.post("/roadmap", response_model=List[RoadmapPhase])
async def generate_roadmap(request: PlanRequest):
    draft = await generate_plan_draft(request)
    return [
        RoadmapPhase(
            phase=phase.name, description=phase.description, goals=phase.goals
        )
        for phase in draft.phases
    ]


@router.post("/tasks", response_model=List[TaskItem])
async def generate_tasks(request: TaskGenerationRequest):
    draft = await generate_plan_draft(
        PlanRequest(idea=request.idea, techStack=request.techStack)
    )
    phase_key = request.phase.strip().lower()
    phase = next(
        (
            p
            for p in draft.phases
            if p.id.lower() == phase_key or p.name.lower() == phase_key
        ),
        None,
    )
    if not phase:
        raise HTTPException(status_code=400, detail="Unknown phase")

    title_lookup = {task.id: task.title for task in phase.tasks}
    tasks = []
    for task in phase.tasks:
        dependencies = [title_lookup.get(dep, dep) for dep in task.dependsOn]
        tasks.append(
            TaskItem(
                title=task.title,
                description=task.description,
                dependencies=dependencies,
            )
        )
    return tasks
