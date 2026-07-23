"use client"

import type { PriorityConfigMap, TaskId, TasksByColumn, Priority } from "./types"

// Priority config - will be localized in component.
// `color` is the solid dot class, `chipTint` is used as the filled-pill background,
// and `chipText` is the foreground color of the chip label (kept as literals so
// Tailwind's JIT can detect them).
export const getPriorityConfig = (t: (key: string) => string): PriorityConfigMap => ({
    urgent: {
        color: "bg-red-500",
        chipTint: "bg-red-500/15",
        chipText: "text-red-600 dark:text-red-400",
        label: t("tasks.urgent"),
        order: 0,
    },
    high: {
        color: "bg-orange-500",
        chipTint: "bg-orange-500/15",
        chipText: "text-orange-600 dark:text-orange-400",
        label: t("tasks.high"),
        order: 1,
    },
    medium: {
        color: "bg-blue-500",
        chipTint: "bg-muted",
        chipText: "text-muted-foreground",
        label: t("tasks.medium"),
        order: 2,
    },
    low: {
        color: "bg-emerald-500",
        chipTint: "bg-muted",
        chipText: "text-muted-foreground",
        label: t("tasks.low"),
        order: 3,
    },
})

export const DEFAULT_TAG_ORDER = ["frontend", "backend", "design", "devops", "qa", "sysadmin"] as const

export const splitTags = (raw?: string): string[] =>
    raw
        ? raw
            .split(",")
            .map((tag) => tag.trim().toLowerCase())
            .filter(Boolean)
        : []

// Task field accessor type for type-safe access
interface TaskFields {
    Id?: TaskId
    id?: TaskId
    Title?: string
    title?: string
    Description?: string
    description?: string
    Priority?: string
    priority?: string
    Deadline?: string
    deadline?: string
    dueDate?: string
    Tags?: string
    tags?: string
    GitHubIssueUrl?: string
    gitHubIssueUrl?: string
    GitHubIssueNumber?: number
    gitHubIssueNumber?: number
    assigneeName?: string
    assigneeAvatarUrl?: string
    attachmentsCount?: number
    commentsCount?: number
}

export const getTaskId = (task: { Id?: TaskId; id?: TaskId }): TaskId =>
    task.Id || task.id || ""

export const getTaskTitle = (task: TaskFields): string =>
    task.Title || task.title || ""

export const getTaskDescription = (task: TaskFields): string | undefined =>
    task.Description || task.description

export const getTaskPriority = (task: TaskFields): Priority =>
    (task.Priority || task.priority || "medium").toLowerCase() as Priority

export const getTaskDueDate = (task: TaskFields): string | undefined =>
    task.Deadline || task.deadline || task.dueDate

export const getTaskTags = (task: TaskFields): string | undefined =>
    task.Tags || task.tags

export const getGitHubIssueUrl = (task: TaskFields): string | undefined =>
    task.GitHubIssueUrl || task.gitHubIssueUrl

export const getGitHubIssueNumber = (task: TaskFields): number | undefined =>
    task.GitHubIssueNumber || task.gitHubIssueNumber

export const hasTaskMetadata = (task: TaskFields): boolean => {
    const attachmentsCount = task.attachmentsCount || 0
    const commentsCount = task.commentsCount || 0
    const gitHubIssueUrl = getGitHubIssueUrl(task)
    const dueDate = getTaskDueDate(task)
    return attachmentsCount > 0 || commentsCount > 0 || Boolean(dueDate) || Boolean(task.assigneeName) || Boolean(gitHubIssueUrl)
}

export const isTaskOverdue = (dueDate?: string): boolean =>
    Boolean(dueDate && new Date(dueDate) < new Date())

export const formatTaskDate = (dateString: string, locale = "en"): string =>
    new Date(dateString).toLocaleDateString(locale, { day: "numeric", month: "short" })

// Collect unique tags from tasks, sorted by default order then alphabetically
export const collectAvailableTags = (
    tasksByColumn: TasksByColumn
): string[] => {
    const allTags = Object.values(tasksByColumn)
        .flatMap(tasks => tasks.flatMap(task => splitTags(getTaskTags(task))))
    const tagSet = new Set(allTags)

    const orderedTags = DEFAULT_TAG_ORDER.filter(tag => tagSet.delete(tag))

    const remainingTags = [...tagSet].sort((a, b) => a.localeCompare(b))
    return [...orderedTags, ...remainingTags]
}
