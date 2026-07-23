"""
Agent chat service — system prompts, content sanitization, tool orchestration.

Extracted from routers/ai.py to improve cohesion and reduce complexity.
The `chat_agent` endpoint had cc=18 and 125 LoC; this module breaks it
into focused, independently testable functions.
"""

import json as _json
import logging
import re
import time
from dataclasses import dataclass, field
from typing import Any, List

from fastapi import HTTPException

from models.ai_models import (
    AgentChatRequest,
    AgentChatResponse,
    ChatMessage,
    ToolCall,
    ToolCallFunction,
)
from metrics import AI_MODEL_LATENCY
from services.ai_service import (
    DEFAULT_PROVIDER,
    require_groq_client,
    emit_token_metrics,
)

logger = logging.getLogger(__name__)

# ============================================================================
# Constants
# ============================================================================

_MAX_CHAT_HISTORY = 8
_MAX_CONTEXT_CHARS = 8_000  # Safety limit to avoid 413 Payload Too Large
_MAX_TOOL_LOOPS = 3  # Prevent infinite query-tool loops

# ============================================================================
# Language & System Prompt Sections
# ============================================================================

_LANG_INSTRUCTIONS: dict[str, str] = {
    "ru": "Отвечай на русском языке.",
    "pl": "Odpowiadaj po polsku.",
    "en": "Respond in English.",
}

_AGENT_SECTIONS: list[str] = [
    """RULES:
- NEVER show UUIDs or raw JSON. Refer to tasks by TITLE.
- YOU call tools — never suggest user to "run" a tool.
- Actions: call directly, UI shows confirmation. No text confirmation needed.
- Bulk: delete_multiple_tasks/move_multiple_tasks with filter="all"/"column".
- Modify task: get_task_details first (for ID), then action tool.
- Be concise. For generate_news_draft: write news in your message.
- Code questions: ALWAYS call analysis tools first, then answer with real data (files, lines).
- After get_code_analysis_summary: chain search_code_issues for specifics.""",

    """TASK RECOMMENDATIONS:
When asked which task to start or what to do next:
1. Filter out tasks with status done/completed — only recommend tasks that still need work.
2. Check dependsOn — skip tasks whose dependencies are not yet done/completed.
3. Consider project stage: if no tasks are completed yet, the project is in early stage — prefer foundational tasks (environment setup, architecture, core data models, basic API) over QA, security audits, or performance optimization.
4. Only recommend urgent/high-priority tasks if their prerequisites are met and they are appropriate for the current stage.
5. If multiple valid tasks exist, prefer the one that unblocks the most other tasks.
6. Explain your reasoning briefly: why this task is the right next step given what's done and what's blocked.""",
]


def build_agent_system_prompt(context: str | None, language: str = "ru") -> str:
    """Build system prompt for agentic chat."""
    lang = _LANG_INSTRUCTIONS.get(language, _LANG_INSTRUCTIONS["en"])
    header = f"You are an AI Architect Assistant that can perform actions in the project. {lang}"
    prompt = "\n\n".join([header, *_AGENT_SECTIONS])

    if context:
        ctx = context
        if len(ctx) > _MAX_CONTEXT_CHARS:
            ctx = ctx[:_MAX_CONTEXT_CHARS] + "\n\n... (context truncated to fit model limits)"
        prompt += f"\n\nCURRENT PROJECT CONTEXT:\n{ctx}"

    return prompt


# ============================================================================
# Chat Message Helpers
# ============================================================================


def build_chat_messages(
    history: List[ChatMessage], current_message: str
) -> list[dict]:
    """Build message list with sliding window for chat history."""
    trimmed = (
        history[-_MAX_CHAT_HISTORY:]
        if len(history) > _MAX_CHAT_HISTORY
        else history
    )
    messages = [{"role": msg.role, "content": msg.content} for msg in trimmed]
    messages.append({"role": "user", "content": current_message})
    return messages


def convert_tool_calls(
    tool_calls_raw: List[dict] | None,
) -> List[ToolCall] | None:
    """Convert raw tool calls from API to response format."""
    if not tool_calls_raw:
        return None
    return [
        ToolCall(
            id=tc["id"],
            type=tc["type"],
            function=ToolCallFunction(
                name=tc["function"]["name"],
                arguments=tc["function"]["arguments"],
                arguments_parsed=tc["function"].get("arguments_parsed"),
            ),
        )
        for tc in tool_calls_raw
    ]


# ============================================================================
# Content Sanitization
# ============================================================================

_UUID_PATTERN = re.compile(
    r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
    re.IGNORECASE,
)
_JSON_BLOCK_PATTERN = re.compile(
    r'```(?:json)?\s*\[\s*\{.*?\}\s*\]\s*```', re.DOTALL,
)
_RAW_JSON_ARRAY_PATTERN = re.compile(
    r'\[\s*\{\s*"name"\s*:\s*"\w+".*?\}\s*\]', re.DOTALL,
)
_ID_LINE_PATTERN = re.compile(
    r'\s*\(?ID:\s*[0-9a-f-]{36}\)?', re.IGNORECASE,
)


def sanitize_ai_content(content: str | None) -> str | None:
    """Remove UUIDs, raw JSON tool calls, and ID references from AI text."""
    if not content:
        return content

    text = content
    text = _JSON_BLOCK_PATTERN.sub('', text)
    text = _RAW_JSON_ARRAY_PATTERN.sub('', text)
    text = _ID_LINE_PATTERN.sub('', text)
    text = _UUID_PATTERN.sub('', text)

    # Clean up leftover artifacts
    text = text.replace('()', '').replace('  ', ' ')
    text = re.sub(r'\n{3,}', '\n\n', text)
    return text.strip() or None


# ============================================================================
# Response Building
# ============================================================================


def build_agent_response(
    message: str | None,
    tool_calls: List[ToolCall] | None,
    usage: dict,
    model_name: str,
) -> AgentChatResponse:
    """Build standardized agent chat response with content sanitization."""
    clean_message = sanitize_ai_content(message)
    return AgentChatResponse(
        message=clean_message,
        toolCalls=tool_calls,
        hasToolCalls=bool(tool_calls),
        provider=DEFAULT_PROVIDER,
        model=model_name,
        promptTokens=usage.get("promptTokens"),
        completionTokens=usage.get("completionTokens"),
        totalTokens=usage.get("totalTokens"),
    )


# ============================================================================
# Tool Call Separation & Resolution
# ============================================================================


def separate_tool_calls(
    tool_calls_raw: list[dict] | None,
) -> tuple[list[dict], list[dict]]:
    """Separate tool calls into (query_calls, action_calls)."""
    from tools import is_query_tool

    if not tool_calls_raw:
        return [], []

    query_calls: list[dict] = []
    action_calls: list[dict] = []
    for tc in tool_calls_raw:
        name = tc["function"]["name"]
        (query_calls if is_query_tool(name) else action_calls).append(tc)

    return query_calls, action_calls


async def resolve_query_tools(
    query_calls: list[dict],
    tool_data_dict: dict | None,
    project_id: str | None = None,
) -> list[dict]:
    """Execute query tools server-side and return message dicts for the conversation."""
    from tools.resolver import resolve_query_tool_async

    results: list[dict] = []
    for tc in query_calls:
        name = tc["function"]["name"]
        args_str = tc["function"].get("arguments", "{}")
        try:
            args = _json.loads(args_str)
        except _json.JSONDecodeError:
            args = {}

        result_text = await resolve_query_tool_async(name, args, tool_data_dict, project_id)

        # Assistant message with the tool call
        results.append({
            "role": "assistant",
            "content": None,
            "tool_calls": [{
                "id": tc["id"],
                "type": "function",
                "function": {"name": name, "arguments": args_str},
            }],
        })
        # Tool result message
        results.append({
            "role": "tool",
            "tool_call_id": tc["id"],
            "name": name,
            "content": result_text,
        })

    return results


def _accumulate_usage(total: dict, delta: dict) -> None:
    """Merge token usage counters into the running total (mutates `total`)."""
    for key in ("promptTokens", "completionTokens", "totalTokens"):
        total[key] = total.get(key, 0) + delta.get(key, 0)


# ============================================================================
# Chat Session — groups shared state to reduce argument passing
# ============================================================================


@dataclass
class _ChatSession:
    """Encapsulates shared mutable state for one agent chat conversation."""

    client: Any
    messages: list[dict]
    tools: list
    system_prompt: str
    model_name: str
    tool_data_dict: dict | None
    project_id: str | None = None
    total_usage: dict = field(default_factory=dict)
    start: float = field(default_factory=time.perf_counter)

    def accumulate(self, usage: dict) -> None:
        _accumulate_usage(self.total_usage, usage)

    def emit_metrics(self) -> None:
        elapsed = time.perf_counter() - self.start
        AI_MODEL_LATENCY.labels(
            model=self.model_name, endpoint="chat_agent",
        ).observe(elapsed)
        emit_token_metrics(self.total_usage, "chat_agent", self.model_name)

    def response(self, message: str | None, tool_calls: List[ToolCall] | None = None) -> AgentChatResponse:
        self.emit_metrics()
        return build_agent_response(message, tool_calls, self.total_usage, self.model_name)

    async def call_llm(self):
        """Call LLM with tools and accumulate usage."""
        content, tool_calls_raw, usage = await self.client.chat_with_tools(
            messages=self.messages, tools=self.tools,
            system_prompt=self.system_prompt, model="smart",
        )
        self.accumulate(usage)
        return content, tool_calls_raw


# ============================================================================
# Agent Chat Orchestration (decomposed from the 125-LoC endpoint)
# ============================================================================


async def _simple_chat(session: _ChatSession, context: str | None) -> AgentChatResponse:
    """Handle agent chat without tools — plain text response."""
    result, usage = await session.client.chat(
        messages=session.messages, context=context, model="smart",
    )
    session.accumulate(usage)
    return session.response(result)


async def _handle_action_with_queries(
    session: _ChatSession, query_calls: list[dict], action_calls: list[dict],
) -> AgentChatResponse:
    """Resolve query tools, then re-call LLM to finalize action tools."""
    session.messages.extend(await resolve_query_tools(query_calls, session.tool_data_dict, session.project_id))

    content2, tool_calls_raw2 = await session.call_llm()

    _, remaining_actions = separate_tool_calls(tool_calls_raw2)
    all_actions = action_calls + remaining_actions if remaining_actions else action_calls
    return session.response(content2, convert_tool_calls(all_actions))


async def _force_text_response(session: _ChatSession, last_content: str | None) -> str | None:
    """Force a final text-only response after query loop exhaustion."""
    logger.info(
        "Tool loop exhausted (%d iterations), forcing text-only response",
        _MAX_TOOL_LOOPS,
    )
    try:
        final_content, _, final_usage = await session.client.chat_with_tools(
            messages=session.messages, tools=session.tools,
            system_prompt=session.system_prompt, model="smart",
            tool_choice="none",
        )
        session.accumulate(final_usage)
        return final_content or last_content
    except Exception:
        logger.warning("Forced text-only call failed, using last content")
        return last_content


async def _run_tool_loop(session: _ChatSession) -> AgentChatResponse:
    """Execute the query-tool resolution loop.

    Returns an AgentChatResponse when done — either text, action tools, or forced text.
    """
    last_content: str | None = None

    for loop_idx in range(_MAX_TOOL_LOOPS):
        content, tool_calls_raw = await session.call_llm()
        last_content = content or last_content

        if not tool_calls_raw:
            break

        query_calls, action_calls = separate_tool_calls(tool_calls_raw)

        # Only action tools — return for frontend confirmation
        if not query_calls:
            return session.response(content, convert_tool_calls(tool_calls_raw))

        # Mixed query + action tools
        if action_calls:
            return await _handle_action_with_queries(session, query_calls, action_calls)

        # Only query tools — resolve and loop
        logger.info(
            "Auto-executing %d query tools (loop %d): %s",
            len(query_calls), loop_idx + 1,
            [tc["function"]["name"] for tc in query_calls],
        )
        session.messages.extend(await resolve_query_tools(query_calls, session.tool_data_dict, session.project_id))
    else:
        last_content = await _force_text_response(session, last_content)

    return session.response(last_content)


async def handle_agent_chat(request: AgentChatRequest) -> AgentChatResponse:
    """Agentic AI Chat with server-side tool execution loop."""
    from tools import get_tools_for_context

    client = require_groq_client()
    has_project = bool(request.projectId or request.context)

    session = _ChatSession(
        client=client,
        messages=build_chat_messages(request.history, request.message),
        tools=get_tools_for_context(has_project) if request.enableTools else [],
        system_prompt=build_agent_system_prompt(request.context, request.language),
        model_name=client.MODELS.get("smart", "smart"),
        tool_data_dict=request.toolData.model_dump() if request.toolData else None,
        project_id=request.projectId,
    )

    try:
        if not session.tools:
            return await _simple_chat(session, request.context)
        return await _run_tool_loop(session)
    except Exception as e:
        logger.exception("Agent chat failed")
        raise HTTPException(
            status_code=500,
            detail=f"Agent chat request failed: {str(e)}",
        ) from e
