"use client"

import { useCallback } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { ActivityCard } from "./ActivityCard"
import { useGlobalFeed, useProfile } from "@/lib/api/queries/profile"
import { useTranslations } from "next-intl"
import { AlertCircle, Loader2, RefreshCw, Sparkles } from "lucide-react"

interface GlobalFeedProps {
  /** When "bare", the component renders just the feed items (no Card wrapper / title). */
  variant?: "card" | "bare"
}

function FeedErrorState({ onRefetch, label, className }: { onRefetch: () => void; label: string; className?: string }) {
  const t = useTranslations()
  return (
    <div className={`flex flex-col items-center gap-3 py-6 text-center ${className ?? ""}`}>
      <AlertCircle className="h-7 w-7 text-muted-foreground/50" />
      <p className="text-sm text-muted-foreground">{t("common.failedToLoad")}</p>
      <Button variant="outline" size="sm" onClick={onRefetch}>
        <RefreshCw className="mr-2 h-3.5 w-3.5" />
        {label}
      </Button>
    </div>
  )
}

function FeedLoadMoreButton({ hasNextPage, isFetchingNextPage, onClick, loadMoreLabel, loadingLabel, className }: {
  hasNextPage: boolean; isFetchingNextPage: boolean; onClick: () => void; loadMoreLabel: string; loadingLabel: string; className?: string
}) {
  if (!hasNextPage) return null
  return (
    <div className="flex justify-center pt-2">
      <Button variant="outline" size="sm" className={className} onClick={onClick} disabled={isFetchingNextPage}>
        {isFetchingNextPage ? loadingLabel : loadMoreLabel}
      </Button>
    </div>
  )
}

export function GlobalFeed({ variant = "card" }: GlobalFeedProps = {}) {
  const t = useTranslations()
  const profileQuery = useProfile()
  const viewerId = profileQuery.data?.id
  const feedResult = useGlobalFeed(8, Boolean(viewerId))

  const {
    data,
    fetchNextPage,
    hasNextPage,
    isFetching,
    isFetchingNextPage,
    refetch,
    isRefetching,
    isError,
  } = feedResult

  // P2-12: Use useCallback to avoid inline arrow functions in props
  const handleRefetch = useCallback(() => { refetch() }, [refetch])
  const handleFetchNextPage = useCallback(() => { fetchNextPage() }, [fetchNextPage])

  if (!viewerId) {
    return null
  }

  // Filter out media_uploaded events - they should be part of news, not separate activity entries
  // P2-11: data is correctly typed as InfiniteData<UserActivityFeedPage> | undefined (no cast needed)
  const activities = data?.pages
    .flatMap((page) => page.data ?? [])
    .filter((activity) => activity.eventType !== "project.media_uploaded") ?? []

  if (variant === "bare") {
    return (
      <div className="flex flex-col gap-2.5">
        {isError && <FeedErrorState onRefetch={handleRefetch} label={t("common.tryAgain")} className="rounded-[14px] border border-border bg-card" />}
        {!isError && isFetching && activities.length === 0 && (
          <div className="space-y-2.5">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-24 w-full rounded-[14px]" />
            ))}
          </div>
        )}
        {!isError && activities.length === 0 && !isFetching && (
          <p className="rounded-[14px] border border-dashed border-border bg-card/60 px-4 py-6 text-[13px] text-muted-foreground">
            {t("dashboard.globalActivityEmpty")}
          </p>
        )}
        {activities.map((activity) => (
          <ActivityCard key={activity.id} activity={activity} context="global" />
        ))}
        <FeedLoadMoreButton hasNextPage={hasNextPage} isFetchingNextPage={isFetchingNextPage} onClick={handleFetchNextPage} loadMoreLabel={t("dashboard.loadMore")} loadingLabel={t("common.loading")} className="h-8 px-3 text-[12px]" />
      </div>
    )
  }

  return (
    <Card variant="elevated" className="rounded-xl">
      <CardHeader className="p-6 pb-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Sparkles className="h-5 w-5" />
            </div>
            <div className="space-y-1">
              <CardTitle className="text-lg font-semibold leading-tight text-foreground">
                {t("dashboard.globalActivityTitle")}
              </CardTitle>
              <p className="text-sm leading-relaxed text-muted-foreground">{t("dashboard.globalActivity")}</p>
            </div>
          </div>
          <Button
            variant="ghost"
            size="sm"
            aria-label={t("dashboard.refreshFeed")}
            className="h-9 w-9 p-0 text-muted-foreground hover:text-foreground hover:bg-accent focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:ring-offset-2"
            onClick={handleRefetch}
            disabled={isRefetching}
          >
            {isRefetching ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <RefreshCw className="h-4 w-4" />
            )}
          </Button>
        </div>
      </CardHeader>
      <CardContent className="space-y-3 px-6 pb-6">
        {isError && <FeedErrorState onRefetch={handleRefetch} label={t("common.tryAgain")} />}
        {!isError && isFetching && activities.length === 0 && (
          <div className="space-y-3">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-24 w-full rounded-xl" />
            ))}
          </div>
        )}
        {!isError && activities.length === 0 && !isFetching ? (
          <p className="text-sm leading-relaxed text-muted-foreground">{t("dashboard.globalActivityEmpty")}</p>
        ) : (
          activities.map((activity) => <ActivityCard key={activity.id} activity={activity} context="global" />)
        )}
        <FeedLoadMoreButton hasNextPage={hasNextPage} isFetchingNextPage={isFetchingNextPage} onClick={handleFetchNextPage} loadMoreLabel={t("dashboard.loadMore")} loadingLabel={t("common.loading")} className="h-9 px-4" />
      </CardContent>
    </Card>
  )
}
