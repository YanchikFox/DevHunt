import type { ActivityPayload } from "./types"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import { safeJsonParse } from "./utils"
import { buildTaskSummary } from "./task-summary"
import { buildProjectMembershipSummary } from "./membership-summary"

/**
 * Builds a summary string for news events
 */
function buildNewsSummary(
    activity: UserActivityFeedItem,
    payload: ActivityPayload | null,
    t: (key: string, values?: Record<string, string | number>) => string
): string | null {
    if (activity.eventType !== "project.news_published") return null

    const title = payload?.title || payload?.Title
    return title ? t("activity.newsPublished", { title }) : t("activity.newsPublishedNoTitle")
}

/**
 * Builds a human-readable summary for the given activity.
 * Delegates to specialized builders for each event category.
 */
export function buildSummary(
    activity: UserActivityFeedItem,
    t: (key: string, values?: Record<string, string | number>) => string
): string {
    const payload = safeJsonParse(activity.payloadJson) as ActivityPayload | null

    // Try each specialized builder in order
    const membershipSummary = buildProjectMembershipSummary(activity, t)
    if (membershipSummary) return membershipSummary

    const newsSummary = buildNewsSummary(activity, payload, t)
    if (newsSummary) return newsSummary

    const taskSummary = buildTaskSummary(activity, payload, t)
    if (taskSummary) return taskSummary

    return activity.summary || activity.eventType
}
