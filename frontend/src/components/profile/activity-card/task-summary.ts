import type { ActivityPayload } from "./types"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"

/** Map of status values to their translation keys */
const STATUS_TRANSLATION_KEYS: Record<string, string> = {
    todo: "tasks.todo",
    doing: "tasks.inProgress",
    in_progress: "tasks.inProgress",
    review: "tasks.review",
    done: "tasks.done",
    archived: "tasks.archived",
    cancelled: "tasks.cancelled",
}

/**
 * Returns a localized status label
 */
function getStatusLabel(status: string, t: (key: string) => string): string {
    const normalizedStatus = (status || "").toLowerCase()
    const translationKey = STATUS_TRANSLATION_KEYS[normalizedStatus]
    return translationKey ? t(translationKey) : status
}

/**
 * Formats the task status change summary with from/to labels
 */
function formatTaskStatusChange(
    payload: ActivityPayload | null,
    title: string,
    t: (key: string, values?: Record<string, string | number>) => string
): string {
    const fromVal = (payload?.from || payload?.From) ?? ""
    const toVal = (payload?.to || payload?.To) ?? ""
    const from = getStatusLabel(String(fromVal), t)
    const to = getStatusLabel(String(toVal), t)
    return t("activity.taskStatusChanged", { title, from, to })
}

/** Map of task event types to their translation keys */
const TASK_EVENT_TRANSLATION_KEYS: Record<string, string> = {
    "task.created": "activity.taskCreated",
    "task.updated": "activity.taskUpdated",
    "task.deleted": "activity.taskDeleted",
    "task.restored": "activity.taskRestored",
}

/** Gets the task title from payload or activity */
function getTaskTitle(payload: ActivityPayload | null, activity: UserActivityFeedItem): string {
    return payload?.title || payload?.Title || activity.summary || ""
}

/**
 * Builds a summary string for task-related events
 */
export function buildTaskSummary(
    activity: UserActivityFeedItem,
    payload: ActivityPayload | null,
    t: (key: string, values?: Record<string, string | number>) => string
): string | null {
    if (!activity.eventType.startsWith("task.")) return null

    const title = getTaskTitle(payload, activity)

    if (activity.eventType === "task.status_changed") {
        return formatTaskStatusChange(payload, title, t)
    }

    const translationKey = TASK_EVENT_TRANSLATION_KEYS[activity.eventType]
    return translationKey ? t(translationKey, { title }) : null
}
