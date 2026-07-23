import { defaultLocale } from "@/i18n/locale-utils"
import { t } from "./agent-types"

// Tool Details (structured key-value pairs for ToolConfirmDialog)

export interface ToolDetail {
    label: string
    value: string
}

/**
 * Label maps for tool detail fields.
 * Each key maps to a Record<locale, label> — add new locales by adding entries.
 */
const DETAIL_LABELS: Record<string, Record<string, string>> = {
    title: { en: "Title", pl: "Tytuł" },
    description: { en: "Description", pl: "Opis" },
    priority: { en: "Priority", pl: "Priorytet" },
    tags: { en: "Tags", pl: "Tagi" },
    deadline: { en: "Deadline", pl: "Termin" },
    estimatedHours: { en: "Estimated hours", pl: "Szacowane godziny" },
    actualHours: { en: "Actual hours", pl: "Rzeczywiste godziny" },
    column: { en: "Column", pl: "Kolumna" },
    assignee: { en: "Assignee", pl: "Przypisany" },
    reason: { en: "Reason", pl: "Powód" },
    filter: { en: "Filter", pl: "Filtr" },
    sourceColumn: { en: "From column", pl: "Z kolumny" },
    targetColumn: { en: "To column", pl: "Do kolumny" },
    status: { en: "Status", pl: "Status" },
    techStack: { en: "Tech Stack", pl: "Stos" },
    visibility: { en: "Visibility", pl: "Widoczność" },
    difficulty: { en: "Difficulty", pl: "Trudność" },
    maxTeamSize: { en: "Max team size", pl: "Maks. zespół" },
    shortDescription: { en: "Short description", pl: "Krótki opis" },
    roles: { en: "Roles", pl: "Role" },
    type: { en: "Type", pl: "Typ" },
    topic: { en: "Topic", pl: "Temat" },
    tone: { en: "Tone", pl: "Ton" },
    criteria: { en: "Criteria", pl: "Kryterium" },
    position: { en: "Position", pl: "Pozycja" },
    callToAction: { en: "Call to action", pl: "Wezwanie do działania" },
}

function detailLabel(key: string, locale: string): string {
    const entry = DETAIL_LABELS[key]
    if (!entry) return key
    return t(entry, locale)
}

function pushDetail(
    details: ToolDetail[],
    key: string,
    value: unknown,
    locale: string
): void {
    if (value === undefined || value === null || value === "") return
    const label = detailLabel(key, locale)
    if (Array.isArray(value)) {
        details.push({ label, value: value.join(", ") })
    } else {
        details.push({ label, value: String(value) })
    }
}

/**
 * Get structured details for a tool call's arguments.
 * Returns key-value pairs suitable for displaying in ToolConfirmDialog.
 */
export function getToolDetails(
    toolName: string,
    args: Record<string, unknown>,
    locale: string = defaultLocale
): ToolDetail[] {
    const details: ToolDetail[] = []

    switch (toolName) {
        case "create_task":
            pushDetail(details, "title", args.title, locale)
            pushDetail(details, "description", args.description, locale)
            pushDetail(details, "priority", args.priority, locale)
            pushDetail(details, "tags", args.tags, locale)
            pushDetail(details, "deadline", args.deadline, locale)
            pushDetail(details, "estimatedHours", args.estimatedHours, locale)
            pushDetail(details, "column", args.columnId, locale)
            pushDetail(details, "assignee", args.assigneeId, locale)
            break

        case "update_task":
            pushDetail(details, "title", args.title, locale)
            pushDetail(details, "description", args.description, locale)
            pushDetail(details, "priority", args.priority, locale)
            pushDetail(details, "tags", args.tags, locale)
            pushDetail(details, "deadline", args.deadline, locale)
            pushDetail(details, "estimatedHours", args.estimatedHours, locale)
            pushDetail(details, "actualHours", args.actualHours, locale)
            pushDetail(details, "assignee", args.assigneeId, locale)
            break

        case "delete_task":
            pushDetail(details, "reason", args.reason, locale)
            break

        case "delete_multiple_tasks": {
            const filter = args.filter as string | undefined
            if (filter) {
                let filterLabel = filter
                if (filter === "all") {
                    filterLabel = t({ pl: "Wszystkie zadania", en: "All tasks" }, locale)
                }
                pushDetail(details, "filter", filterLabel, locale)
            }
            if (filter === "column") {
                pushDetail(details, "column", args.columnName, locale)
            }
            const ids = args.taskIds as string[] | undefined
            if (ids && ids.length > 0) {
                details.push({ label: t({ pl: "Ilość", en: "Count" }, locale), value: String(ids.length) })
            }
            pushDetail(details, "reason", args.reason, locale)
            break
        }

        case "move_task":
            pushDetail(details, "column", args.columnName || args.columnId, locale)
            pushDetail(details, "position", args.position, locale)
            break

        case "move_multiple_tasks": {
            const mvFilter = args.filter as string | undefined
            if (mvFilter) {
                let mvFilterLabel = mvFilter
                if (mvFilter === "all") {
                    mvFilterLabel = t({ pl: "Wszystkie zadania", en: "All tasks" }, locale)
                }
                pushDetail(details, "filter", mvFilterLabel, locale)
            }
            if (mvFilter === "column") {
                pushDetail(details, "sourceColumn", args.sourceColumnName, locale)
            }
            pushDetail(details, "targetColumn", args.targetColumnName || args.targetColumnId, locale)
            const mvIds = args.taskIds as string[] | undefined
            if (mvIds && mvIds.length > 0) {
                details.push({ label: t({ pl: "Ilość", en: "Count" }, locale), value: String(mvIds.length) })
            }
            break
        }

        case "create_multiple_tasks": {
            const tasks = args.tasks as Array<{ title: string; priority?: string; description?: string }> | undefined
            pushDetail(details, "column", args.columnId, locale)
            if (tasks) {
                for (const task of tasks) {
                    const parts = [task.title]
                    if (task.priority) parts.push(`[${task.priority}]`)
                    details.push({ label: "•", value: parts.join(" ") })
                }
            }
            break
        }

        case "update_project":
            pushDetail(details, "description", args.description, locale)
            pushDetail(details, "shortDescription", args.shortDescription, locale)
            pushDetail(details, "techStack", args.techStack, locale)
            pushDetail(details, "status", args.status, locale)
            pushDetail(details, "visibility", args.visibility, locale)
            pushDetail(details, "difficulty", args.difficultyLevel, locale)
            pushDetail(details, "maxTeamSize", args.maxTeamSize, locale)
            break

        case "update_looking_for": {
            const roles = args.roles as Array<{ role: string; count?: number; skills?: string[] }> | undefined
            if (roles) {
                for (const r of roles) {
                    const parts = [r.role]
                    if (r.count) parts.push(`(x${r.count})`)
                    if (r.skills?.length) parts.push(`— ${r.skills.join(", ")}`)
                    details.push({ label: "•", value: parts.join(" ") })
                }
            }
            break
        }

        case "generate_news_draft":
            pushDetail(details, "type", args.type, locale)
            pushDetail(details, "topic", args.topic, locale)
            pushDetail(details, "tone", args.tone, locale)
            pushDetail(details, "callToAction", args.includeCallToAction, locale)
            break

        case "suggest_next_task":
            pushDetail(details, "criteria", args.criteria, locale)
            pushDetail(details, "assignee", args.assigneeId, locale)
            break
    }

    return details
}
