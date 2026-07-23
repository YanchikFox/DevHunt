import { defaultLocale, type AppLocale } from "@/i18n/locale-utils"

/**
 * AI Agent Types - Function Calling / Tool Calls
 *
 * Types for the agentic AI chat with function calling capabilities.
 * The AI can call tools to modify project data (tasks, settings, etc).
 */

/** Helper: pick a locale string from a translations map, falling back to defaultLocale. */
export function t(map: Record<string, string>, locale: string): string {
    return map[locale] ?? map[defaultLocale] ?? Object.values(map)[0] ?? ""
}

// =============================================================================
// Tool Definitions
// =============================================================================

export type ToolCategory = "tasks" | "project" | "content" | "query"

export interface ToolDefinition {
    name: string
    description: string
    category: ToolCategory
    icon: string
    /** Locale → label mapping, e.g. { en: "Create Task", pl: "Utwórz zadanie" } */
    labels: Record<string, string>
    parameters: {
        type: "object"
        properties: Record<string, ToolParameterDef>
        required?: string[]
    }
}

export interface ToolParameterDef {
    type: "string" | "number" | "integer" | "boolean" | "array" | "object"
    description?: string
    enum?: string[]
    items?: { type: string }
    default?: unknown
}

// =============================================================================
// Tool Calls (from AI response)
// =============================================================================

export interface ToolCallFunction {
    name: string
    arguments: string // JSON string
    arguments_parsed?: Record<string, unknown> | null
}

export interface ToolCall {
    id: string
    type: "function"
    function: ToolCallFunction
}

// =============================================================================
// Agent Chat API
// =============================================================================

export interface AgentChatRequest {
    message: string
    history?: Array<{ role: "user" | "assistant"; content: string }>
    context?: string
    projectId?: string
    enableTools?: boolean
    language?: AppLocale
}

export interface AgentChatResponse {
    message: string | null
    toolCalls?: ToolCall[] | null
    hasToolCalls: boolean
    provider?: string
    model?: string
    promptTokens?: number
    completionTokens?: number
    totalTokens?: number
}

// =============================================================================
// Tool Arguments (parsed)
// =============================================================================

/** Arguments for create_task tool */
export interface CreateTaskArgs {
    title: string
    description?: string
    priority?: "low" | "medium" | "high" | "critical"
    columnId?: string
    assigneeId?: string
    tags?: string[]
    deadline?: string
    estimatedHours?: number
}

/** Arguments for update_task tool */
export interface UpdateTaskArgs {
    taskId: string
    title?: string
    description?: string
    priority?: "low" | "medium" | "high" | "critical"
    assigneeId?: string
    tags?: string[]
    deadline?: string
    estimatedHours?: number
    actualHours?: number
}

/** Arguments for delete_task tool */
export interface DeleteTaskArgs {
    taskId: string
    reason?: string
}

/** Arguments for move_task tool */
export interface MoveTaskArgs {
    taskId: string
    columnId?: string
    columnName?: string
    position?: number
}

/** Arguments for create_multiple_tasks tool */
export interface CreateMultipleTasksArgs {
    tasks: Array<{
        title: string
        description?: string
        priority?: "low" | "medium" | "high" | "critical"
        tags?: string[]
    }>
    columnId?: string
}

/** Arguments for delete_multiple_tasks tool */
export interface DeleteMultipleTasksArgs {
    taskIds?: string[]
    filter?: "all" | "column"
    columnName?: string
    reason?: string
}

/** Arguments for move_multiple_tasks tool */
export interface MoveMultipleTasksArgs {
    taskIds?: string[]
    filter?: "all" | "column"
    sourceColumnName?: string
    targetColumnName: string
    targetColumnId?: string
}

/** Arguments for update_project tool */
export interface UpdateProjectArgs {
    description?: string
    shortDescription?: string
    techStack?: string[]
    status?: "draft" | "recruiting" | "in_progress" | "completed" | "on_hold" | "cancelled"
    visibility?: "public" | "private" | "team_only"
    difficultyLevel?: "beginner" | "intermediate" | "advanced"
    maxTeamSize?: number
}

/** Arguments for update_looking_for tool */
export interface UpdateLookingForArgs {
    roles: Array<{
        role: string
        count?: number
        skills?: string[]
        description?: string
    }>
}

/** Arguments for generate_news_draft tool */
export interface GenerateNewsDraftArgs {
    type: "release" | "update" | "milestone" | "announcement" | "welcome" | "recruitment"
    topic: string
    tone?: "professional" | "casual" | "excited" | "formal"
    includeCallToAction?: boolean
}

/** Arguments for suggest_next_task tool */
export interface SuggestNextTaskArgs {
    criteria?: "priority" | "deadline" | "dependencies" | "quick_wins"
    assigneeId?: string
}

// =============================================================================
// Tool Execution Results
// =============================================================================

export interface ToolExecutionResult {
    toolCallId: string
    toolName: string
    success: boolean
    result?: unknown
    error?: string
}

// =============================================================================
// Display Info
// =============================================================================

export const TOOL_DISPLAY_INFO: Record<
    string,
    { icon: string; labels: Record<string, string>; category: ToolCategory }
> = {
    create_task: {
        icon: "➕",
        labels: { pl: "Utwórz zadanie", en: "Create Task" },
        category: "tasks",
    },
    update_task: {
        icon: "✏️",
        labels: { pl: "Zaktualizuj zadanie", en: "Update Task" },
        category: "tasks",
    },
    delete_task: {
        icon: "🗑️",
        labels: { pl: "Usuń zadanie", en: "Delete Task" },
        category: "tasks",
    },
    move_task: {
        icon: "📦",
        labels: { pl: "Przenieś zadanie", en: "Move Task" },
        category: "tasks",
    },
    create_multiple_tasks: {
        icon: "📋",
        labels: { pl: "Utwórz wiele zadań", en: "Create Multiple Tasks" },
        category: "tasks",
    },
    delete_multiple_tasks: {
        icon: "🗑️",
        labels: { pl: "Usuń wiele zadań", en: "Delete Multiple Tasks" },
        category: "tasks",
    },
    move_multiple_tasks: {
        icon: "📦",
        labels: { pl: "Przenieś wiele zadań", en: "Move Multiple Tasks" },
        category: "tasks",
    },
    update_project: {
        icon: "⚙️",
        labels: { pl: "Zaktualizuj projekt", en: "Update Project" },
        category: "project",
    },
    update_looking_for: {
        icon: "👥",
        labels: { pl: "Zaktualizuj wymagania", en: "Update Recruitment" },
        category: "project",
    },
    generate_news_draft: {
        icon: "📰",
        labels: { pl: "Wygeneruj wiadomość", en: "Generate News" },
        category: "content",
    },
    suggest_next_task: {
        icon: "💡",
        labels: { pl: "Zaproponuj zadanie", en: "Suggest Next Task" },
        category: "query",
    },
}

/**
 * Get human-readable label for a tool
 */
export function getToolLabel(toolName: string, locale: string = defaultLocale): string {
    const info = TOOL_DISPLAY_INFO[toolName]
    if (!info) return toolName
    return t(info.labels, locale)
}

/**
 * Get icon for a tool
 */
export function getToolIcon(toolName: string): string {
    return TOOL_DISPLAY_INFO[toolName]?.icon ?? "🔧"
}

/**
 * Format tool arguments for display (brief one-liner)
 */
export function formatToolArgsForDisplay(
    toolName: string,
    args: Record<string, unknown>,
    locale: string = defaultLocale
): string {
    /** Shorthand for picking from a {pl, en} map. */
    const l = (map: Record<string, string>) => t(map, locale)

    switch (toolName) {
        case "create_task":
            return l({ pl: `Utwórz zadanie: "${args.title}"`, en: `Create task: "${args.title}"` })

        case "update_task":
            return l({ pl: "Zaktualizuj zadanie", en: "Update task" })

        case "delete_task":
            return l({ pl: "Usuń zadanie", en: "Delete task" })

        case "delete_multiple_tasks": {
            const filter = args.filter as string | undefined
            if (filter === "all") {
                return l({ pl: "Usuń wszystkie zadania", en: "Delete all tasks" })
            }
            if (filter === "column") {
                return l({
                    pl: `Usuń zadania z kolumny "${args.columnName}"`,
                    en: `Delete tasks from column "${args.columnName}"`,
                })
            }
            const ids = args.taskIds as string[] | undefined
            return l({ pl: `Usuń ${ids?.length || 0} zadań`, en: `Delete ${ids?.length || 0} tasks` })
        }

        case "move_task":
            return l({
                pl: `Przenieś zadanie do kolumny "${args.columnName || args.columnId}"`,
                en: `Move task to column "${args.columnName || args.columnId}"`,
            })

        case "move_multiple_tasks": {
            const moveFilter = args.filter as string | undefined
            const target = (args.targetColumnName || args.targetColumnId) as string
            if (moveFilter === "all") {
                return l({
                    pl: `Przenieś wszystkie zadania do kolumny "${target}"`,
                    en: `Move all tasks to column "${target}"`,
                })
            }
            if (moveFilter === "column") {
                return l({
                    pl: `Przenieś zadania z "${args.sourceColumnName}" do "${target}"`,
                    en: `Move tasks from "${args.sourceColumnName}" to "${target}"`,
                })
            }
            const moveIds = args.taskIds as string[] | undefined
            return l({
                pl: `Przenieś ${moveIds?.length || 0} zadań do "${target}"`,
                en: `Move ${moveIds?.length || 0} tasks to "${target}"`,
            })
        }

        case "create_multiple_tasks": {
            const tasksArr = args.tasks as Array<{ title: string }>
            return l({
                pl: `Utwórz ${tasksArr?.length || 0} zadań`,
                en: `Create ${tasksArr?.length || 0} tasks`,
            })
        }

        case "update_project": {
            const fields = Object.keys(args).filter(k => args[k] !== undefined)
            return l({
                pl: `Zaktualizuj projekt (${fields.join(", ")})`,
                en: `Update project (${fields.join(", ")})`,
            })
        }

        case "generate_news_draft":
            return l({
                pl: `Wygeneruj wiadomość: ${args.topic}`,
                en: `Generate news: ${args.topic}`,
            })

        default:
            return l({ pl: `Wykonaj ${toolName}`, en: `Execute ${toolName}` })
    }
}

