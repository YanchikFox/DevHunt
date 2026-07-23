import { useMemo } from "react"
import { useProject } from "./projects"
import { useTasks } from "./tasks"
import { useTeamMembers } from "./teams"
import { useColumns } from "./columns"
import { useProjectDocuments, isAiDocument } from "./documents"

/**
 * Detect project/application type from description and tech stack.
 */
function detectProjectType(description: string, techStack: string[]): string {
    const text = `${description} ${techStack.join(" ")}`.toLowerCase()

    const patterns: Array<{ type: string; tokens: string[] }> = [
        {
            type: "Mobile Application",
            tokens: [
                "mobile", "mobiln", "ios", "android", "react native", "flutter",
                "swift", "kotlin", "app store", "google play", "telefon",
                "smartfon", "tablet",
            ],
        },
        {
            type: "Game",
            tokens: [
                "game", "gra", "gier", "unity", "unreal", "godot", "gameplay",
                "gameplay", "multiplayer", "wieloosobowa",
            ],
        },
        {
            type: "Data Science / ML",
            tokens: [
                "machine learning", "ml", "data science", "sieć neuronowa", "neural",
                "tensorflow", "pytorch", "deep learning",
            ],
        },
        {
            type: "Desktop Application",
            tokens: [
                "desktop", "desktopow", "biurkow", "electron", "tauri",
                ".net maui", "wpf", "winforms",
            ],
        },
        {
            type: "IoT / Embedded",
            tokens: [
                "iot", "embedded", "wbudowan", "mikrokontroler", "arduino",
                "raspberry", "czujnik", "sensor",
            ],
        },
        {
            type: "CLI Tool",
            tokens: ["cli", "command line", "terminal", "konsolow"],
        },
    ]

    for (const { type, tokens } of patterns) {
        if (tokens.some((t) => text.includes(t))) {
            return type
        }
    }
    return "Web Application"
}

/**
 * Full task data available for on-demand AI tool resolution.
 * Sent as toolData (not in the system prompt).
 */
export interface ToolDataTask {
    id: string
    title: string
    description?: string
    status: string
    columnId?: string
    priority: string
    assignee?: string
    assigneeId?: string
    isCompleted?: boolean
    dueDate?: string
    estimatedHours?: number
    actualHours?: number
    tags?: string
    linkCount?: number
    attachmentCount?: number
    gitHubIssueNumber?: number
    gitHubIssueUrl?: string
    createdAt?: string
    updatedAt?: string
}

/**
 * Data payload sent alongside the chat request for on-demand tool resolution.
 * This data is NOT included in the system prompt — tools fetch from it on demand.
 */
export interface ToolData {
    tasks: ToolDataTask[]
    taskIdMap: Record<string, string>       // title → id
    teamMemberIdMap: Record<string, string> // name → userId
    columns: Array<{ id: string; name: string; isCompleted: boolean }>
    passport: Array<{ type: string; title: string; content: string }>
}

/**
 * Aggregated project context for AI assistant.
 * Contains all relevant project information in a structured format.
 */
export interface ProjectContext {
    /** Project basic info */
    project: {
        id: string
        title: string
        description: string
        techStack: string[]
        status: string
        visibility: string
        teamSize: number
        maxTeamSize?: number | null
        createdAt: string
        updatedAt: string
    } | null

    /** Team members */
    team: {
        total: number
        members: Array<{
            name: string
            role: string
            isLeader: boolean
        }>
    }

    /** Tasks summary */
    tasks: {
        total: number
        byStatus: Record<string, number>
        byPriority: Record<string, number>
        recentTasks: ToolDataTask[]
    }

    /** Compact context string for AI system prompt (~3KB) */
    contextString: string

    /** Full data payload for on-demand tool resolution */
    toolData: ToolData

    /** Loading state */
    isLoading: boolean

    /** Error state */
    error: string | null
}

type ColumnInfoMap = Record<string, { name: string; isCompleted: boolean }>

function buildColumnInfoMap(columns: Array<{ id: string; name: string; isCompleted?: boolean | null }>): ColumnInfoMap {
    const map: ColumnInfoMap = {}
    for (const col of columns) map[col.id] = { name: col.name, isCompleted: col.isCompleted ?? false }
    return map
}

function computeTasksByColumn(tasks: Array<{ columnId?: string | null; status?: string | null }>, colMap: ColumnInfoMap): Record<string, number> {
    const result: Record<string, number> = {}
    for (const task of tasks) {
        const colInfo = task.columnId ? colMap[task.columnId] : null
        const name = colInfo?.name || task.status || "unknown"
        result[name] = (result[name] || 0) + 1
    }
    return result
}

function computeTasksByPriority(tasks: Array<{ priority?: string | null }>): Record<string, number> {
    const result: Record<string, number> = {}
    for (const task of tasks) result[task.priority || "medium"] = (result[task.priority || "medium"] || 0) + 1
    return result
}

function formatTeamMembers(teamMembers: Array<{ user?: { name?: string } | null; role?: string | null }>) {
    return teamMembers.map((m) => ({
        name: m.user?.name || "Unknown",
        role: m.role || "Member",
        isLeader: m.role === "owner" || m.role === "lead",
    }))
}

type RawTask = {
    id: string; title: string; description?: string | null; columnId?: string | null
    priority?: string | null; assigneeName?: string | null; assigneeId?: string | null
    dueDate?: string | null; estimatedHours?: number | null; actualHours?: number | null
    tags?: string | null; linkCount?: number | null; attachmentCount?: number | null
    gitHubIssueNumber?: number | null; gitHubIssueUrl?: string | null
    createdAt?: string | null; updatedAt?: string | null; status?: string | null
}

function mapTasksToContext(
    tasks: RawTask[], colMap: ColumnInfoMap
): ToolDataTask[] {
    return tasks.slice(0, 100).map((task) => {
        const colInfo = task.columnId ? colMap[task.columnId] : null
        const columnName = colInfo?.name || task.status || "unknown"
        return {
            id: task.id, title: task.title, description: task.description || undefined,
            status: columnName, columnId: task.columnId || undefined,
            priority: task.priority || "medium", assignee: task.assigneeName || undefined,
            assigneeId: task.assigneeId || undefined, isCompleted: colInfo?.isCompleted ?? false,
            dueDate: task.dueDate || undefined, estimatedHours: task.estimatedHours || undefined,
            actualHours: task.actualHours || undefined, tags: task.tags || undefined,
            linkCount: task.linkCount || 0, attachmentCount: task.attachmentCount || 0,
            gitHubIssueNumber: task.gitHubIssueNumber || undefined,
            gitHubIssueUrl: task.gitHubIssueUrl || undefined,
            createdAt: task.createdAt || undefined, updatedAt: task.updatedAt || undefined,
        }
    })
}

type ProjectData = { title: string; status: string; description?: string | null; technologies?: unknown }

function buildContextString(
    project: ProjectData | null | undefined,
    projectId: string | null | undefined,
    teamMembers: Array<{ user?: { name?: string } | null; role?: string | null }>,
    formattedTeam: Array<{ name: string; role: string; isLeader: boolean }>,
    tasksLength: number,
    columns: Array<{ id: string; name: string; isCompleted?: boolean | null }>,
    tasksByColumn: Record<string, number>,
    recentTasks: ToolDataTask[],
): string {
    if (!project || !projectId) return ""
    const lines: string[] = []
    const techList = Array.isArray(project.technologies) ? project.technologies as string[] : []
    const appType = detectProjectType(project.description || "", techList)
    const desc = project.description || "Not provided"

    lines.push("=== PROJECT ===")
    lines.push(`Name: ${project.title}`)
    lines.push(`Status: ${project.status}`)
    lines.push(`Description: ${desc.length > 300 ? desc.substring(0, 300) + "..." : desc}`)
    lines.push(`Type: ${appType}`)
    if (techList.length > 0) lines.push(`Tech Stack: ${techList.join(", ")}`)

    lines.push("", `=== TEAM (${teamMembers.length}) ===`)
    for (const m of formattedTeam) lines.push(`- ${m.name} (${m.role})${m.isLeader ? " [Lead]" : ""}`)

    lines.push("", `=== TASKS (${tasksLength} total) ===`)
    for (const [colName, count] of Object.entries(tasksByColumn)) {
        const done = columns.find(c => c.name === colName)?.isCompleted ? " [DONE]" : ""
        lines.push(`${colName}: ${count}${done}`)
    }
    if (columns.length > 0) {
        lines.push("", "Columns (ID | name):")
        for (const col of columns) lines.push(`  ${col.id} | ${col.name}${col.isCompleted ? " [done]" : ""}`)
    }
    if (recentTasks.length > 0) {
        lines.push("", "=== TASK LIST (titles only — use get_task_details for full info on a specific task) ===")
        for (const task of recentTasks) lines.push(`• ${task.title} [${task.status}/${task.priority}]${task.isCompleted ? " ✓" : ""}`)
        if (tasksLength > recentTasks.length) lines.push(`... and ${tasksLength - recentTasks.length} more tasks not shown.`)
    }
    lines.push("", "NOTE: For task details, descriptions, assignees, or passport — use query tools (get_task_details, get_tasks_by_column, get_project_summary).")
    return lines.join("\n")
}

function buildToolData(
    recentTasks: ToolDataTask[],
    teamMembers: Array<{ user?: { name?: string; id?: string } | null; userId?: string | null; role?: string | null }>,
    columns: Array<{ id: string; name: string; isCompleted?: boolean | null }>,
    allDocuments: Array<{ documentType?: string | null; title: string; contentPreview?: string | null; content?: string | null }>,
): ToolData {
    const taskIdMap: Record<string, string> = {}
    for (const task of recentTasks) taskIdMap[task.title] = task.id
    const teamMemberIdMap: Record<string, string> = {}
    for (const m of teamMembers) {
        const name = m.user?.name || "Unknown"
        const id = m.userId || m.user?.id
        if (id) teamMemberIdMap[name] = id
    }
    return {
        tasks: recentTasks,
        taskIdMap,
        teamMemberIdMap,
        columns: columns.map(c => ({ id: c.id, name: c.name, isCompleted: c.isCompleted ?? false })),
        passport: allDocuments.filter(d => isAiDocument(d.documentType ?? null))
            .map(d => ({ type: d.documentType || "unknown", title: d.title, content: d.contentPreview || d.content || "" })),
    }
}

/**
 * Hook to aggregate all project context for AI assistant.
 * Combines project, team, and tasks data into a structured context.
 *
 * @param projectId - The project ID to load context for
 * @returns ProjectContext object with all aggregated data
 */
export function useProjectContext(projectId: string | null | undefined): ProjectContext {
    const { data: project, isLoading: projectLoading, error: projectError } = useProject(projectId ?? "")
    const { data: tasks = [], isLoading: tasksLoading, error: tasksError } = useTasks(projectId ?? "")
    const { data: teamMembers = [], isLoading: teamLoading, error: teamError } = useTeamMembers(projectId ?? "", { includePermissions: false })
    const { data: columns = [], isLoading: columnsLoading } = useColumns(projectId ?? "")
    const { data: allDocuments = [] } = useProjectDocuments(projectId ?? "")

    const isLoading = projectLoading || tasksLoading || teamLoading || columnsLoading
    const error = projectError?.message || tasksError?.message || teamError?.message || null

    const columnInfoById = useMemo(() => buildColumnInfoMap(columns), [columns])
    const tasksByColumn = useMemo(() => computeTasksByColumn(tasks, columnInfoById), [tasks, columnInfoById])
    const tasksByPriority = useMemo(() => computeTasksByPriority(tasks), [tasks])
    const formattedTeam = useMemo(() => formatTeamMembers(teamMembers), [teamMembers])
    const recentTasks = useMemo(() => mapTasksToContext(tasks, columnInfoById), [tasks, columnInfoById])
    const contextString = useMemo(
        () => buildContextString(project, projectId, teamMembers, formattedTeam, tasks.length, columns, tasksByColumn, recentTasks),
        [project, projectId, teamMembers, formattedTeam, tasks.length, columns, tasksByColumn, recentTasks]
    )
    const toolData = useMemo(
        () => buildToolData(recentTasks, teamMembers, columns, allDocuments),
        [recentTasks, teamMembers, columns, allDocuments]
    )

    return {
        project: project ? {
            id: project.id, title: project.title, description: project.description || "",
            techStack: Array.isArray(project.technologies) ? project.technologies as string[] : [],
            status: project.status, visibility: project.visibility || "private",
            teamSize: project.teamSize || teamMembers.length, maxTeamSize: project.maxTeamSize,
            createdAt: project.createdAt, updatedAt: project.updatedAt,
        } : null,
        team: { total: teamMembers.length, members: formattedTeam },
        tasks: { total: tasks.length, byStatus: tasksByColumn, byPriority: tasksByPriority, recentTasks },
        contextString, toolData, isLoading, error,
    }
}

/**
 * Format project context as a system prompt for AI.
 *
 * @param context - The project context
 * @param language - User's preferred language locale code
 * @param isMember - Whether the current user is a project member with edit rights
 * @returns Formatted system prompt string
 */
export function formatProjectContextForAI(context: ProjectContext, language: string = "pl", isMember: boolean = false): string {
    /** Language-specific instructions for the AI — add new locales here. */
    const LANG_INSTRUCTIONS: Record<string, string> = {
        pl: "WAŻNE: Odpowiadaj TYLKO po polsku. WSZYSTKIE odpowiedzi muszą być po polsku, bez wyjątków.",
        en: "CRITICAL: Respond in English only. ALL responses must be in English.",
    }
    const langInstruction = LANG_INSTRUCTIONS[language] ?? LANG_INSTRUCTIONS["en"]

    if (!context.project) {
        return `You are an AI Architect Assistant. ${langInstruction}

No project context available. User is asking general questions about software architecture.
Be concise and practical in your answers. Use markdown formatting for better readability:
- Use **bold** for emphasis
- Use bullet points for lists
- Use headers (##) for sections when appropriate`
    }

    // Security instruction based on membership
    const securityInstruction = isMember
        ? "User is a PROJECT MEMBER. You may assist with project modifications using tools."
        : "User is a GUEST (READ-ONLY). You MUST NOT execute any tools or modify the project. Answer questions only."

    // Detect app type for system prompt
    const contextTechStack = context.project.techStack || []
    const contextAppType = detectProjectType(context.project.description || "", contextTechStack)

    return `You are an AI Architect Assistant for the project "${context.project.title}".
This is a ${contextAppType} project.${contextTechStack.length > 0 ? ` Tech stack: ${contextTechStack.join(", ")}.` : ""}

${langInstruction}
${securityInstruction}

FORMATTING INSTRUCTIONS:
- Use markdown formatting for better readability
- Use **bold** for task names and important terms
- Use bullet points (-) for lists
- Keep responses concise but well-structured

IMPORTANT — CONTEXT & TOOLS:
- The context below includes ALL task titles with status and priority.
- For simple questions like "list my tasks" or "what tasks are there" → answer directly from the context below. Do NOT call tools.
- Use query tools ONLY when you need EXTRA details not in the context:
  • get_task_details(taskTitle) — description, assignee, ID, tags, deadline
  • get_tasks_by_column(columnName) — detailed list for a specific column
  • get_project_summary() — project passport, architecture, tech decisions
- After receiving tool results, IMMEDIATELY answer the question. Do NOT call more tools.
- To modify a task, FIRST call get_task_details to get its ID, then call the action tool.

⚠️ NEVER SHOW TASK IDs TO USER — refer to tasks by title only.

${context.contextString}`
}
