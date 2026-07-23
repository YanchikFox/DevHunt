"""
AI Assistant Tools Definitions.

Defines available tools (functions) that the AI can call to modify project data.
These follow OpenAI function calling format compatible with Groq API.

NOTE: Keep descriptions SHORT — every byte counts toward Groq's payload limit.
"""

from typing import Any

# =============================================================================
# Tool Definitions (OpenAI Function Calling Format)
# =============================================================================

TOOLS: list[dict[str, Any]] = [
    # ── Task Management ──
    {
        "type": "function",
        "function": {
            "name": "create_task",
            "description": "Create a new task.",
            "parameters": {
                "type": "object",
                "properties": {
                    "title": {"type": "string"},
                    "description": {"type": "string"},
                    "priority": {"type": "string", "enum": ["low", "medium", "high", "critical"]},
                    "columnId": {"type": "string"},
                    "assigneeId": {"type": "string"},
                    "tags": {"type": "array", "items": {"type": "string"}},
                    "deadline": {"type": "string", "description": "ISO 8601 (YYYY-MM-DD)"},
                    "estimatedHours": {"type": "number"},
                },
                "required": ["title"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "update_task",
            "description": "Update an existing task by ID.",
            "parameters": {
                "type": "object",
                "properties": {
                    "taskId": {"type": "string"},
                    "title": {"type": "string"},
                    "description": {"type": "string"},
                    "priority": {"type": "string", "enum": ["low", "medium", "high", "critical"]},
                    "assigneeId": {"type": "string"},
                    "tags": {"type": "array", "items": {"type": "string"}},
                    "deadline": {"type": "string"},
                    "estimatedHours": {"type": "number"},
                    "actualHours": {"type": "number"},
                },
                "required": ["taskId"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "delete_task",
            "description": "Delete a task by ID.",
            "parameters": {
                "type": "object",
                "properties": {
                    "taskId": {"type": "string"},
                },
                "required": ["taskId"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "move_task",
            "description": "Move task to another column.",
            "parameters": {
                "type": "object",
                "properties": {
                    "taskId": {"type": "string"},
                    "columnId": {"type": "string"},
                    "columnName": {"type": "string"},
                    "position": {"type": "integer"},
                },
                "required": ["taskId"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "move_multiple_tasks",
            "description": "Bulk move tasks. Use filter='all' or filter='column' with sourceColumnName.",
            "parameters": {
                "type": "object",
                "properties": {
                    "taskIds": {"type": "array", "items": {"type": "string"}},
                    "filter": {"type": "string", "enum": ["all", "column"]},
                    "sourceColumnName": {"type": "string"},
                    "targetColumnName": {"type": "string"},
                    "targetColumnId": {"type": "string"},
                },
                "required": ["targetColumnName"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "create_multiple_tasks",
            "description": "Create multiple tasks at once.",
            "parameters": {
                "type": "object",
                "properties": {
                    "tasks": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "title": {"type": "string"},
                                "description": {"type": "string"},
                                "priority": {"type": "string", "enum": ["low", "medium", "high", "critical"]},
                                "tags": {"type": "array", "items": {"type": "string"}},
                            },
                            "required": ["title"],
                        },
                    },
                    "columnId": {"type": "string"},
                },
                "required": ["tasks"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "delete_multiple_tasks",
            "description": "Bulk delete. Use filter='all' or filter='column' with columnName.",
            "parameters": {
                "type": "object",
                "properties": {
                    "taskIds": {"type": "array", "items": {"type": "string"}},
                    "filter": {"type": "string", "enum": ["all", "column"]},
                    "columnName": {"type": "string"},
                },
                "required": [],
            },
        },
    },

    # ── Project Management ──
    {
        "type": "function",
        "function": {
            "name": "update_project",
            "description": "Update project settings (description, tech stack, status, etc).",
            "parameters": {
                "type": "object",
                "properties": {
                    "description": {"type": "string"},
                    "shortDescription": {"type": "string"},
                    "techStack": {"type": "array", "items": {"type": "string"}},
                    "status": {"type": "string", "enum": ["draft", "recruiting", "in_progress", "completed", "on_hold", "cancelled"]},
                    "visibility": {"type": "string", "enum": ["public", "private", "team_only"]},
                    "difficultyLevel": {"type": "string", "enum": ["beginner", "intermediate", "advanced"]},
                    "maxTeamSize": {"type": "integer"},
                },
                "required": [],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "update_looking_for",
            "description": "Update recruitment roles.",
            "parameters": {
                "type": "object",
                "properties": {
                    "roles": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "role": {"type": "string"},
                                "count": {"type": "integer"},
                                "skills": {"type": "array", "items": {"type": "string"}},
                                "description": {"type": "string"},
                            },
                            "required": ["role"],
                        },
                    },
                },
                "required": ["roles"],
            },
        },
    },

    # ── Content ──
    {
        "type": "function",
        "function": {
            "name": "generate_news_draft",
            "description": "Signal intent to generate news. Write the actual news text in your message.",
            "parameters": {
                "type": "object",
                "properties": {
                    "type": {"type": "string", "enum": ["release", "update", "milestone", "announcement", "welcome", "recruitment"]},
                    "topic": {"type": "string"},
                    "tone": {"type": "string", "enum": ["professional", "casual", "excited", "formal"]},
                    "includeCallToAction": {"type": "boolean"},
                },
                "required": ["type", "topic"],
            },
        },
    },

    # ── Query Tools (auto-executed server-side) ──
    {
        "type": "function",
        "function": {
            "name": "get_task_details",
            "description": "Get task details by title.",
            "parameters": {
                "type": "object",
                "properties": {
                    "taskTitle": {"type": "string"},
                },
                "required": ["taskTitle"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_tasks_by_column",
            "description": "List tasks in a column.",
            "parameters": {
                "type": "object",
                "properties": {
                    "columnName": {"type": "string"},
                },
                "required": ["columnName"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_project_summary",
            "description": "Get project summary with passport, team, task stats.",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "suggest_next_task",
            "description": "Suggest next task by priority/deadline/quick_wins.",
            "parameters": {
                "type": "object",
                "properties": {
                    "criteria": {"type": "string", "enum": ["priority", "deadline", "dependencies", "quick_wins"]},
                },
                "required": [],
            },
        },
    },

    # ── Code Analysis (auto-executed, DB queries) ──
    {
        "type": "function",
        "function": {
            "name": "get_code_analysis_summary",
            "description": "Get code analysis summary: severity/category counts, top rules, worst files.",
            "parameters": {"type": "object", "properties": {}, "required": []},
        },
    },
    {
        "type": "function",
        "function": {
            "name": "search_code_issues",
            "description": "Search code issues by severity/category/file/rule/text.",
            "parameters": {
                "type": "object",
                "properties": {
                    "severity": {"type": "string", "enum": ["critical", "major", "minor", "info"]},
                    "category": {"type": "string"},
                    "file": {"type": "string"},
                    "ruleId": {"type": "string"},
                    "q": {"type": "string"},
                    "limit": {"type": "integer"},
                },
                "required": [],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "semantic_search_issues",
            "description": "Natural language search across code issues via embeddings.",
            "parameters": {
                "type": "object",
                "properties": {
                    "query": {"type": "string"},
                },
                "required": ["query"],
            },
        },
    },
]


def get_tools_for_context(has_project: bool = True) -> list[dict[str, Any]]:
    """Get tools for the current context."""
    if not has_project:
        return [t for t in TOOLS if t["function"]["name"] == "generate_news_draft"]
    return TOOLS


# Set of tools that are auto-executed server-side (no user confirmation needed)
QUERY_TOOLS: set[str] = {
    "get_task_details",
    "get_tasks_by_column",
    "get_project_summary",
    "suggest_next_task",
    "get_code_analysis_summary",
    "search_code_issues",
    "semantic_search_issues",
}


def is_query_tool(tool_name: str) -> bool:
    """Check if a tool is a query/read-only tool (auto-executed server-side)."""
    return tool_name in QUERY_TOOLS


def get_tool_names() -> list[str]:
    """Get list of all tool names."""
    return [t["function"]["name"] for t in TOOLS]


# Human-readable descriptions for UI
TOOL_DISPLAY_INFO: dict[str, dict[str, str]] = {
    "create_task": {"icon": "➕", "label_en": "Create Task", "label_pl": "Utwórz zadanie", "category": "tasks"},
    "update_task": {"icon": "✏️", "label_en": "Update Task", "label_pl": "Zaktualizuj zadanie", "category": "tasks"},
    "delete_task": {"icon": "🗑️", "label_en": "Delete Task", "label_pl": "Usuń zadanie", "category": "tasks"},
    "move_task": {"icon": "📦", "label_en": "Move Task", "label_pl": "Przenieś zadanie", "category": "tasks"},
    "create_multiple_tasks": {"icon": "📋", "label_en": "Create Multiple Tasks", "label_pl": "Utwórz wiele zadań", "category": "tasks"},
    "delete_multiple_tasks": {"icon": "🗑️", "label_en": "Delete Multiple Tasks", "label_pl": "Usuń wiele zadań", "category": "tasks"},
    "move_multiple_tasks": {"icon": "📦", "label_en": "Move Multiple Tasks", "label_pl": "Przenieś wiele zadań", "category": "tasks"},
    "update_project": {"icon": "⚙️", "label_en": "Update Project", "label_pl": "Zaktualizuj projekt", "category": "project"},
    "update_looking_for": {"icon": "👥", "label_en": "Update Recruitment", "label_pl": "Zaktualizuj wymagania", "category": "project"},
    "generate_news_draft": {"icon": "📰", "label_en": "Generate News", "label_pl": "Wygeneruj wiadomość", "category": "content"},
    "suggest_next_task": {"icon": "💡", "label_en": "Suggest Next Task", "label_pl": "Zaproponuj zadanie", "category": "query"},
    "get_task_details": {"icon": "🔍", "label_en": "Get Task Details", "label_pl": "Szczegóły zadania", "category": "query"},
    "get_tasks_by_column": {"icon": "📊", "label_en": "Get Tasks by Column", "label_pl": "Zadania wg kolumny", "category": "query"},
    "get_project_summary": {"icon": "📋", "label_en": "Project Summary", "label_pl": "Podsumowanie projektu", "category": "query"},
    "get_code_analysis_summary": {"icon": "🔬", "label_en": "Code Analysis Summary", "label_pl": "Podsumowanie analizy kodu", "category": "query"},
    "search_code_issues": {"icon": "🔍", "label_en": "Search Code Issues", "label_pl": "Szukaj problemow w kodzie", "category": "query"},
    "semantic_search_issues": {"icon": "🧠", "label_en": "Semantic Search Issues", "label_pl": "Semantyczne wyszukiwanie", "category": "query"},
}
