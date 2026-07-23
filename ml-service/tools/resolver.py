"""
Server-side query tool resolver.

Resolves query tools from the toolData payload or database.
- Task/project tools: resolved from toolData (no DB calls)
- Code analysis tools: resolved via asyncpg queries to PostgreSQL
Results are injected back into the conversation for the AI to reason about.
"""

import json
import logging
import uuid
from typing import Any

logger = logging.getLogger(__name__)


# Issue JSON field names (from analyzer output)
# {"file_path": "...", "line": 12, "end_line": null, "rule_id": "SP-S03",
#  "rule_name": "...", "severity": "critical", "category": "security",
#  "message": "...", "snippet": "...", "suggestion": "...", "cwe_id": "CWE-798"}

def _get_file(issue: dict) -> str:
    """Extract file path from issue, stripping analyzer temp prefix."""
    raw = issue.get("file_path") or issue.get("file") or issue.get("location", {}).get("path", "?")
    # Strip /tmp/analyzer_*/repo/ prefix
    if "/repo/" in raw:
        raw = raw.split("/repo/", 1)[1]
    return raw


def _get_line(issue: dict) -> str:
    return str(issue.get("line") or issue.get("location", {}).get("startLine", "?"))


# =============================================================================
# Async code analysis resolvers (DB-powered)
# =============================================================================

_CODE_ANALYSIS_TOOLS = {
    "get_code_analysis_summary",
    "search_code_issues",
    "semantic_search_issues",
}


def is_async_tool(tool_name: str) -> bool:
    """Check if a tool requires async resolution (DB queries)."""
    return tool_name in _CODE_ANALYSIS_TOOLS


async def resolve_query_tool_async(
    tool_name: str,
    arguments: dict[str, Any],
    tool_data: dict[str, Any] | None,
    project_id: str | None = None,
) -> str:
    """Resolve a query tool — async version supporting DB queries.

    Code analysis tools query the database directly.
    Task/project tools resolve from toolData payload.
    """
    if tool_name in _CODE_ANALYSIS_TOOLS:
        return await _resolve_code_analysis_tool(tool_name, arguments, project_id)

    # Sync tools — delegate to original resolvers
    return resolve_query_tool(tool_name, arguments, tool_data)


def resolve_query_tool(
    tool_name: str,
    arguments: dict[str, Any],
    tool_data: dict[str, Any] | None,
) -> str:
    """Resolve a query tool call using the toolData payload (sync)."""
    if tool_data is None:
        return "No project data available."

    resolvers = {
        "get_task_details": _resolve_task_details,
        "get_tasks_by_column": _resolve_tasks_by_column,
        "get_project_summary": _resolve_project_summary,
        "suggest_next_task": _resolve_suggest_next_task,
    }

    resolver = resolvers.get(tool_name)
    if not resolver:
        return f"Unknown query tool: {tool_name}"

    try:
        return resolver(arguments, tool_data)
    except Exception as e:
        logger.exception("Error resolving tool %s", tool_name)
        return f"Error resolving {tool_name}: {str(e)}"


# =============================================================================
# Code Analysis Resolvers (async, DB-powered)
# =============================================================================

async def _resolve_code_analysis_tool(
    tool_name: str,
    args: dict[str, Any],
    project_id: str | None,
) -> str:
    """Route code analysis tool to the appropriate DB resolver."""
    if not project_id:
        return "No project context — cannot query code analysis data."

    try:
        pid = uuid.UUID(project_id)
    except (ValueError, AttributeError):
        return f"Invalid project ID format: {project_id}"

    try:
        from deps import get_shared_pool
        pool = await get_shared_pool()
    except Exception as e:
        logger.exception("Failed to get DB pool for code analysis tool")
        return f"Database unavailable: {e}"

    try:
        async with pool.acquire() as conn:
            if tool_name == "get_code_analysis_summary":
                return await _resolve_code_summary(conn, pid)
            elif tool_name == "search_code_issues":
                return await _resolve_search_issues(conn, pid, args)
            elif tool_name == "semantic_search_issues":
                return await _resolve_semantic_search(conn, pid, args)
            else:
                return f"Unknown code analysis tool: {tool_name}"
    except Exception as e:
        logger.exception("Error resolving code analysis tool %s", tool_name)
        return f"Error querying code analysis: {e}"


async def _resolve_code_summary(conn, project_id: uuid.UUID) -> str:
    """Get compact code analysis summary from the latest analysis result."""
    # Find latest analysis for this project
    result = await conn.fetchrow("""
        SELECT "Id", "TotalIssues", "TotalFiles", "SeverityCountsJson",
               "CategoryCountsJson", "CreatedAt", "Repository", "Branch"
        FROM "CodeAnalysisResults"
        WHERE "ProjectId" = $1 AND "Status" = 'completed'
        ORDER BY "CreatedAt" DESC
        LIMIT 1
    """, project_id)

    if not result:
        return "No code analysis results found for this project. Run a code analysis first."

    analysis_id = result["Id"]  # UUID from asyncpg

    # Parse severity counts from JSONB
    sev_counts = {}
    if result["SeverityCountsJson"]:
        try:
            sev_counts = json.loads(result["SeverityCountsJson"]) if isinstance(result["SeverityCountsJson"], str) else result["SeverityCountsJson"]
        except (json.JSONDecodeError, TypeError):
            pass

    cat_counts = {}
    if result["CategoryCountsJson"]:
        try:
            cat_counts = json.loads(result["CategoryCountsJson"]) if isinstance(result["CategoryCountsJson"], str) else result["CategoryCountsJson"]
        except (json.JSONDecodeError, TypeError):
            pass

    lines = [
        "=== CODE ANALYSIS SUMMARY ===",
        f"Repository: {result['Repository']}" + (f" ({result['Branch']})" if result.get('Branch') else ""),
        f"Analyzed: {result['CreatedAt']}",
        f"Files analyzed: {result['TotalFiles']}",
        f"Total issues: {result['TotalIssues']}",
    ]

    if sev_counts:
        lines.append("Severity breakdown:")
        for sev in ["critical", "major", "minor", "info"]:
            count = sev_counts.get(sev, 0)
            if count:
                lines.append(f"  {sev.capitalize()}: {count}")

    if cat_counts:
        lines.append("Category breakdown:")
        for cat, count in sorted(cat_counts.items(), key=lambda x: -x[1]):
            lines.append(f"  {cat}: {count}")

    # Top 15 rules by frequency
    top_rules = await conn.fetch("""
        SELECT "RuleId", "RuleName", "Severity", "Category", "IssueCount"
        FROM "CodeAnalysisEmbeddings"
        WHERE "AnalysisResultId" = $1
        ORDER BY "IssueCount" DESC
        LIMIT 15
    """, analysis_id)

    if top_rules:
        lines.append("\nTop issues by frequency:")
        for r in top_rules:
            lines.append(
                f"  [{r['Severity']}] {r['RuleId']}: {r['RuleName'] or 'N/A'} "
                f"({r['IssueCount']} occurrences, {r['Category']})"
            )

    # Worst files (from IssuesJson)
    issues_json = await conn.fetchval("""
        SELECT "IssuesJson" FROM "CodeAnalysisResults"
        WHERE "Id" = $1
    """, analysis_id)

    if issues_json:
        try:
            issues = json.loads(issues_json)
            file_counts: dict[str, int] = {}
            for issue in issues:
                f = _get_file(issue)
                file_counts[f] = file_counts.get(f, 0) + 1
            worst_files = sorted(file_counts.items(), key=lambda x: -x[1])[:10]
            if worst_files:
                lines.append("\nWorst files by issue count:")
                for fpath, count in worst_files:
                    lines.append(f"  {fpath}: {count} issues")
        except (json.JSONDecodeError, TypeError):
            pass

    return "\n".join(line for line in lines if line)


async def _resolve_search_issues(conn, project_id: uuid.UUID, args: dict[str, Any]) -> str:
    """Search code analysis issues with filters."""
    # Get latest analysis
    row = await conn.fetchrow("""
        SELECT "Id", "IssuesJson" FROM "CodeAnalysisResults"
        WHERE "ProjectId" = $1 AND "Status" = 'completed'
        ORDER BY "CreatedAt" DESC
        LIMIT 1
    """, project_id)

    if not row:
        return "No code analysis results found for this project."

    issues_json = row["IssuesJson"]

    if not issues_json:
        return "Analysis found but no issues data available."

    try:
        issues = json.loads(issues_json)
    except (json.JSONDecodeError, TypeError):
        return "Failed to parse issues data."

    # Apply filters
    severity = args.get("severity")
    category = args.get("category")
    file_filter = (args.get("file") or "").lower()
    rule_id = (args.get("ruleId") or "").lower()
    q = (args.get("q") or "").lower()
    limit = min(args.get("limit", 20), 50)

    filtered = issues
    if severity:
        filtered = [i for i in filtered if (i.get("severity") or "").lower() == severity.lower()]
    if category:
        filtered = [i for i in filtered if (i.get("category") or "").lower() == category.lower()]
    if file_filter:
        filtered = [i for i in filtered if file_filter in _get_file(i).lower()]
    if rule_id:
        filtered = [i for i in filtered if (i.get("rule_id") or "").lower() == rule_id]
    if q:
        filtered = [i for i in filtered
                    if q in (i.get("message") or "").lower()
                    or q in (i.get("rule_id") or "").lower()
                    or q in _get_file(i).lower()
                    or q in (i.get("rule_name") or "").lower()]

    total_matched = len(filtered)
    filtered = filtered[:limit]

    if not filtered:
        return f"No issues found matching the given filters. Total issues in analysis: {len(issues)}."

    lines = [f"Found {total_matched} issues (showing {len(filtered)}):"]

    # Group similar issues
    seen_rules: dict[str, list] = {}
    for issue in filtered:
        rid = issue.get("rule_id") or "unknown"
        if rid not in seen_rules:
            seen_rules[rid] = []
        seen_rules[rid].append(issue)

    for rid, group in seen_rules.items():
        first = group[0]
        sev = first.get("severity", "?")
        cat = first.get("category", "?")
        msg = first.get("message", "No message")
        rule_name = first.get("rule_name") or rid

        lines.append(f"\n[{sev}] {rule_name} ({rid}) — {cat}")
        if len(group) == 1:
            lines.append(f"  File: {_get_file(first)}:{_get_line(first)}")
            lines.append(f"  Message: {msg[:200]}")
            if first.get("suggestion"):
                lines.append(f"  Fix: {first['suggestion'][:200]}")
        else:
            lines.append(f"  Message: {msg[:200]}")
            lines.append(f"  Affected files ({len(group)}):")
            for item in group[:10]:
                lines.append(f"    - {_get_file(item)}:{_get_line(item)}")
            if len(group) > 10:
                lines.append(f"    ... and {len(group) - 10} more files")
            if first.get("suggestion"):
                lines.append(f"  Fix: {first['suggestion'][:200]}")

    return "\n".join(lines)


async def _resolve_semantic_search(conn, project_id: uuid.UUID, args: dict[str, Any]) -> str:
    """Semantic search using pgvector embeddings."""
    query = (args.get("query") or "").strip()
    if not query:
        return "No search query provided."

    # Get latest analysis
    analysis_id = await conn.fetchval("""
        SELECT "Id" FROM "CodeAnalysisResults"
        WHERE "ProjectId" = $1 AND "Status" = 'completed'
        ORDER BY "CreatedAt" DESC
        LIMIT 1
    """, project_id)

    if not analysis_id:
        return "No code analysis results found for this project."

    # Check if embeddings exist
    emb_count = await conn.fetchval("""
        SELECT COUNT(*) FROM "CodeAnalysisEmbeddings"
        WHERE "AnalysisResultId" = $1 AND "Embedding" IS NOT NULL
    """, analysis_id)

    if not emb_count:
        return "No embeddings generated yet. Trigger embedding generation first."

    # Generate query embedding via ml-service's own embedding model
    try:
        from routers.embeddings import _get_model
        model = _get_model()
        query_vectors = list(model.embed([query]))
        query_embedding = query_vectors[0].tolist()
    except Exception as e:
        logger.exception("Failed to generate query embedding")
        return f"Embedding model unavailable: {e}"

    # pgvector cosine similarity search
    embedding_str = "[" + ",".join(str(v) for v in query_embedding) + "]"
    rows = await conn.fetch("""
        SELECT "RuleId", "RuleName", "Severity", "Category", "IssueCount",
               "EmbeddingText", "SampleMessage", "TopFilesJson", "CweId",
               "SampleSuggestion",
               1 - ("Embedding" <=> $1::vector) AS similarity
        FROM "CodeAnalysisEmbeddings"
        WHERE "AnalysisResultId" = $2 AND "Embedding" IS NOT NULL
        ORDER BY "Embedding" <=> $1::vector
        LIMIT 10
    """, embedding_str, analysis_id)

    if not rows:
        return "No relevant issues found for this query."

    lines = [f"Semantic search results for: \"{query}\"", ""]
    for i, row in enumerate(rows, 1):
        sim = row["similarity"]
        if sim < 0.3:
            continue  # Skip low-relevance results

        lines.append(f"{i}. [{row['Severity']}] {row['RuleName'] or row['RuleId']} (similarity: {sim:.2f})")
        lines.append(f"   Category: {row['Category']} | Occurrences: {row['IssueCount']}")
        if row.get("CweId"):
            lines.append(f"   CWE: {row['CweId']}")
        if row.get("SampleMessage"):
            lines.append(f"   Example: {row['SampleMessage'][:200]}")
        if row.get("TopFilesJson"):
            try:
                files = json.loads(row["TopFilesJson"]) if isinstance(row["TopFilesJson"], str) else row["TopFilesJson"]
                if files:
                    top = files[:5] if isinstance(files, list) else list(files.items())[:5]
                    lines.append(f"   Top files: {', '.join(str(f) for f in top)}")
            except (json.JSONDecodeError, TypeError):
                pass
        if row.get("SampleSuggestion"):
            lines.append(f"   Suggestion: {row['SampleSuggestion'][:200]}")
        lines.append("")

    # Filter out empty results
    if len(lines) <= 2:
        return f"No sufficiently relevant issues found for: \"{query}\". Try a more specific query."

    return "\n".join(lines)


# =============================================================================
# Task / Project Resolvers (sync, from toolData)
# =============================================================================

def _resolve_task_details(
    args: dict[str, Any], data: dict[str, Any]
) -> str:
    """Find a task by title and return full details."""
    query = (args.get("taskTitle") or "").strip().lower()
    if not query:
        return "No task title provided."

    tasks: list[dict] = data.get("tasks", [])

    # Exact match first, then fuzzy
    match = None
    for task in tasks:
        title = task.get("title", "")
        if title.lower() == query:
            match = task
            break

    if not match:
        # Partial / substring match
        for task in tasks:
            title = task.get("title", "")
            if query in title.lower() or title.lower() in query:
                match = task
                break

    if not match:
        available = [t.get("title", "?") for t in tasks[:10]]
        return f"Task not found: '{args.get('taskTitle')}'. Available tasks: {', '.join(available)}"

    # Build detailed response (P1-04: exclude internal IDs from LLM context)
    lines = [f"Task: {match.get('title', '?')}"]
    lines.append(f"  Column: {match.get('status', 'N/A')}")
    lines.append(f"  Priority: {match.get('priority', 'medium')}")

    if match.get("description"):
        lines.append(f"  Description: {match['description']}")
    if match.get("assignee"):
        lines.append(f"  Assignee: {match['assignee']}")
    if match.get("dueDate"):
        lines.append(f"  Due: {match['dueDate']}")
    if match.get("estimatedHours"):
        lines.append(f"  Estimated: {match['estimatedHours']}h")
    if match.get("actualHours"):
        lines.append(f"  Actual: {match['actualHours']}h")
    if match.get("tags"):
        lines.append(f"  Tags: {match['tags']}")
    if match.get("isCompleted"):
        lines.append("  Status: COMPLETED")
    if match.get("linkCount"):
        lines.append(f"  Links: {match['linkCount']}")
    if match.get("attachmentCount"):
        lines.append(f"  Attachments: {match['attachmentCount']}")
    if match.get("gitHubIssueUrl"):
        lines.append(f"  GitHub: {match['gitHubIssueUrl']}")

    return "\n".join(lines)


def _resolve_tasks_by_column(
    args: dict[str, Any], data: dict[str, Any]
) -> str:
    """Return all tasks in a specific column."""
    column_query = (args.get("columnName") or "").strip().lower()
    if not column_query:
        return "No column name provided."

    tasks: list[dict] = data.get("tasks", [])
    columns: list[dict] = data.get("columns", [])

    # Find matching column name (fuzzy)
    matching_col = None
    for col in columns:
        col_name = col.get("name", "")
        if col_name.lower() == column_query or column_query in col_name.lower():
            matching_col = col_name
            break

    if not matching_col:
        available = [c.get("name", "?") for c in columns]
        return f"Column not found: '{args.get('columnName')}'. Available: {', '.join(available)}"

    # Filter tasks by column
    matched_tasks = [
        t for t in tasks if t.get("status", "").lower() == matching_col.lower()
    ]

    if not matched_tasks:
        return f"No tasks in column '{matching_col}'."

    lines = [f"Tasks in '{matching_col}' ({len(matched_tasks)}):"]
    for task in matched_tasks:
        priority = task.get("priority", "medium")
        assignee = task.get("assignee", "")
        assignee_str = f" → {assignee}" if assignee else ""
        desc = task.get("description", "")
        desc_str = f"\n    {desc[:120]}..." if desc and len(desc) > 120 else (f"\n    {desc}" if desc else "")
        # P1-04: Exclude internal IDs from LLM context
        lines.append(
            f"  • [{priority}] {task.get('title', '?')}{assignee_str}"
            f"{desc_str}"
        )

    return "\n".join(lines)


def _resolve_project_summary(
    args: dict[str, Any], data: dict[str, Any]
) -> str:
    """Return project passport and summary."""
    passport: list[dict] = data.get("passport", [])
    tasks: list[dict] = data.get("tasks", [])
    team_map: dict[str, str] = data.get("teamMemberIdMap", {})

    lines = ["=== PROJECT SUMMARY ==="]

    # Team (P1-04: Exclude internal user IDs from LLM context)
    if team_map:
        lines.append(f"\nTeam ({len(team_map)} members):")
        for name in team_map.keys():
            lines.append(f"  - {name}")

    # Task distribution
    col_counts: dict[str, int] = {}
    for task in tasks:
        col = task.get("status", "unknown")
        col_counts[col] = col_counts.get(col, 0) + 1

    if col_counts:
        lines.append(f"\nTasks ({len(tasks)} total):")
        for col_name, count in col_counts.items():
            lines.append(f"  {col_name}: {count}")

    # Passport sections
    if passport:
        lines.append("\n=== PROJECT PASSPORT ===")
        for section in passport:
            title = section.get("title") or section.get("type", "Unknown")
            content = section.get("content", "")
            # Limit each section to prevent huge payloads
            if len(content) > 1500:
                content = content[:1500] + "... (truncated)"
            lines.append(f"\n--- {title} ---")
            lines.append(content)
    else:
        lines.append("\nNo passport generated yet. Generate one via the Passport feature.")

    return "\n".join(lines)


def _resolve_suggest_next_task(
    args: dict[str, Any], data: dict[str, Any]
) -> str:
    """Suggest next task to work on based on priority and status."""
    tasks: list[dict] = data.get("tasks", [])
    criteria = args.get("criteria", "priority")

    # Filter out completed tasks
    active_tasks = [t for t in tasks if not t.get("isCompleted", False)]

    if not active_tasks:
        return "No active tasks to suggest. All tasks are completed!"

    # Sort by priority
    priority_order = {"critical": 0, "high": 1, "medium": 2, "low": 3}

    if criteria == "deadline":
        # Tasks with deadlines first, sorted by date
        with_deadline = [t for t in active_tasks if t.get("dueDate")]
        with_deadline.sort(key=lambda t: t.get("dueDate", ""))
        candidates = with_deadline or active_tasks
    elif criteria == "quick_wins":
        # Low estimated hours first
        candidates = sorted(
            active_tasks,
            key=lambda t: t.get("estimatedHours") or 999,
        )
    else:
        # Default: priority
        candidates = sorted(
            active_tasks,
            key=lambda t: priority_order.get(t.get("priority", "medium"), 2),
        )

    top = candidates[:5]
    lines = [f"Top {len(top)} suggested tasks (by {criteria}):"]
    for i, task in enumerate(top, 1):
        lines.append(
            f"  {i}. [{task.get('priority', 'medium')}] {task.get('title', '?')}"
            f" — {task.get('status', '?')}"
        )
        if task.get("dueDate"):
            lines.append(f"     Due: {task['dueDate']}")
        if task.get("description"):
            desc = task["description"][:100]
            lines.append(f"     {desc}")

    return "\n".join(lines)
