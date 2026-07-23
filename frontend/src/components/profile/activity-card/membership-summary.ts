import type { UserActivityFeedItem } from "@/lib/api/queries/profile"

/** Handlers for project membership event summaries */
const MEMBERSHIP_SUMMARY_HANDLERS: Record<
    string,
    (activity: UserActivityFeedItem, t: (key: string, values?: Record<string, string | number>) => string) => string
> = {
    "user.joined_project": (activity, t) =>
        activity.projectTitle
            ? t("activity.joinedProject", { project: activity.projectTitle })
            : t("activity.joinedProjectNoName"),
    "user.left_project": (activity, t) =>
        activity.projectTitle
            ? t("activity.leftProject", { project: activity.projectTitle })
            : t("activity.leftProjectNoName"),
}

/**
 * Builds a summary string for project membership events
 */
export function buildProjectMembershipSummary(
    activity: UserActivityFeedItem,
    t: (key: string, values?: Record<string, string | number>) => string
): string | null {
    const handler = MEMBERSHIP_SUMMARY_HANDLERS[activity.eventType]
    return handler ? handler(activity, t) : null
}
