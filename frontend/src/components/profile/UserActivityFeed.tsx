"use client"

import { useCallback } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { ActivityCard } from "./ActivityCard"
import type { InfiniteData } from "@tanstack/react-query"
import { useUserActivityFeed } from "@/lib/api/queries/profile";
import { useTranslations } from "next-intl"

/**
 * Props for the UserActivityFeed component.
 */
export type UserActivityFeedProps = {
  /** ID of the user whose activity to display */
  userId: string
  /** Visibility setting for the activity feed */
  visibility?: string
  /** Whether the feed is visible */
  isVisible?: boolean
}

/**
 * Displays a paginated feed of user activities.
 * Fetches data using `useUserActivityFeed` hook.
 *
 * @example
 * ```tsx
 * <UserActivityFeed userId="123" visibility="public" isVisible={true} />
 * ```
 */
export function UserActivityFeed({ userId, visibility, isVisible = true }: UserActivityFeedProps) {
  const t = useTranslations("activityFeed")
  const feedResult = useUserActivityFeed(userId, visibility, Boolean(isVisible && visibility))
  const {
    fetchNextPage,
    hasNextPage,
    isFetching,
    isFetchingNextPage,
  } = feedResult

  const data = feedResult.data
  const handleFetchNextPage = useCallback(() => fetchNextPage(), [fetchNextPage])

  if (!isVisible || !visibility) {
    return null
  }

  const activities = data?.pages.flatMap((page) => page.data) ?? []

  return (
    <Card className="border-border/50 bg-card/40 backdrop-blur-sm shadow-sm h-full flex flex-col">
      <CardHeader className="pb-3 border-b border-border/40 shrink-0">
        <CardTitle className="text-lg font-semibold leading-tight text-foreground">{t("title")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 px-6 pt-6 flex-1 min-h-0 overflow-visible">
        {activities.length === 0 && !isFetching ? (
          <div className="flex flex-col items-center justify-center p-8 text-center border border-dashed border-border/60 rounded-xl bg-muted/10">
            <p className="text-sm text-muted-foreground">{t("noActivity")}</p>
          </div>
        ) : (
          <div className="space-y-4">
            {activities.map((activity) => <ActivityCard key={activity.id} activity={activity} context="user" />)}
          </div>
        )}
        {hasNextPage && (
          <div className="flex justify-center pt-2">
            <Button
              variant="outline"
              onClick={handleFetchNextPage}
              disabled={isFetchingNextPage}
              className="h-9 px-6 hover:bg-muted/50 transition-all border-dashed"
            >
              {isFetchingNextPage ? t("loadMore") + "..." : t("loadMore")}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
