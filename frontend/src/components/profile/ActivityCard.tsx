"use client"

import { useState } from "react"
import { useTranslations, useLocale } from "next-intl"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import {
  getEventVisuals,
  getDefaultVisual,
  buildSummary,
  extractNewsContent,
  deriveEventGroup,
  formatTimeAgo,
} from "./activity-card"
import { NewsEventCard } from "./activity-card/NewsEventCard"
import { RegularActivityCard } from "./activity-card/RegularActivityCard"

/**
 * Props for the ActivityCard component.
 */
export type ActivityCardProps = {
  /** The activity item to display */
  activity: UserActivityFeedItem
  /** Context where the card is displayed - affects pin visibility */
  context?: "global" | "project" | "user"
}

/**
 * Displays a single activity item in the user's activity feed.
 * Renders different icons and styles based on the event type.
 *
 * @example
 * ```tsx
 * <ActivityCard
 *   activity={{
 *     id: "1",
 *     eventType: "user.created_project",
 *     actorName: "John Doe",
 *     createdAt: "2023-01-01T00:00:00Z",
 *     ...
 *   }}
 * />
 * ```
 */
export function ActivityCard({ activity, context = "global" }: Readonly<ActivityCardProps>) {
  const t = useTranslations()
  const locale = useLocale()

  // Compute common values
  const actorName = activity.actorName ?? t("common.unknownUser")
  const avatarInitial = (actorName.trim().charAt(0) || "U").toUpperCase()
  const summary = buildSummary(activity, t)
  const timeAgo = formatTimeAgo(activity.createdAt, locale, t)

  const eventVisuals = getEventVisuals(t)
  const visual = eventVisuals[activity.eventType] ?? getDefaultVisual(t)

  const projectLabel = activity.projectTitle ? t("activity.projectLabel", { project: activity.projectTitle }) : null
  const userLabel = activity.targetUserName ? t("activity.userLabel", { user: activity.targetUserName }) : null
  const targetLine = [projectLabel, userLabel].filter(Boolean).join(" · ")

  const eventGroup = activity.eventGroup || deriveEventGroup(activity.eventType)
  const newsContent = extractNewsContent(activity)
  const isNewsEvent = activity.eventType === "project.news_published" && newsContent

  // State for image lightbox
  const [lightboxOpen, setLightboxOpen] = useState(false)
  const [lightboxIndex, setLightboxIndex] = useState(0)

  if (isNewsEvent) {
    return (
      <NewsEventCard
        activity={activity}
        newsContent={newsContent}
        actorName={actorName}
        avatarInitial={avatarInitial}
        timeAgo={timeAgo}
        context={context}
        t={t}
        lightboxOpen={lightboxOpen}
        lightboxIndex={lightboxIndex}
        setLightboxOpen={setLightboxOpen}
        setLightboxIndex={setLightboxIndex}
      />
    )
  }

  return (
    <RegularActivityCard
      activity={activity}
      actorName={actorName}
      avatarInitial={avatarInitial}
      summary={summary}
      timeAgo={timeAgo}
      targetLine={targetLine}
      eventGroup={eventGroup}
      visual={visual}
    />
  )
}
