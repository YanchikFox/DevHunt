"use client"

import { NewsCard, NewsCardSkeleton, NewsCardProps } from "./NewsCard"
import { Button } from "@/components/ui/button"
import { Newspaper, RefreshCw } from "lucide-react"
import { cn } from "@/lib/utils"
import { useTranslations } from "next-intl"

export interface NewsFeedProps {
  readonly news: readonly Omit<NewsCardProps, "onLike" | "onBookmark" | "onEdit" | "onDelete">[]
  readonly isLoading?: boolean
  readonly hasMore?: boolean
  readonly onLoadMore?: () => void
  readonly isLoadingMore?: boolean
  readonly onRefresh?: () => void
  readonly isRefreshing?: boolean
  readonly canManage?: boolean
  readonly onEdit?: (id: string) => void
  readonly onDelete?: (id: string) => void
  readonly onTogglePin?: (id: string, nextValue: boolean) => void
  readonly onChangeVisibility?: (id: string, nextVisibility: NewsCardProps["visibility"]) => void
  readonly emptyMessage?: string
  readonly emptyIcon?: React.ReactNode
  readonly className?: string
  readonly variant?: "default" | "compact" | "featured"
  readonly columns?: 1 | 2 | 3
}

function RefreshIconButton({ onClick, disabled, label, className, variant }: {
  onClick: () => void
  disabled?: boolean
  label: string
  className?: string
  variant?: "outline" | "ghost"
}) {
  const spinning = disabled
  return (
    <Button variant={variant ?? "ghost"} size="sm" onClick={onClick} disabled={disabled} className={className}>
      <RefreshCw className={cn("mr-2 h-4 w-4", spinning && "animate-spin")} />
      {label}
    </Button>
  )
}

export function NewsFeed({
  news,
  isLoading,
  hasMore,
  onLoadMore,
  isLoadingMore,
  onRefresh,
  isRefreshing,
  canManage,
  onEdit,
  onDelete,
  onTogglePin,
  onChangeVisibility,
  emptyMessage,
  emptyIcon,
  className,
  variant = "default",
  columns = 1,
}: NewsFeedProps) {
  const t = useTranslations()
  const defaultEmptyMsg = t("news.noNews")

  if (isLoading) {
    return (
      <div
        className={cn("space-y-4", columns > 1 && `grid gap-4 md:grid-cols-${columns}`, className)}
      >
        {[...Array(3)].map((_, i) => (
          <NewsCardSkeleton key={`skeleton-${i}`} variant={variant} />
        ))}
      </div>
    )
  }

  if (!news || news.length === 0) {
    return (
      <div
        className={cn(
          "flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-muted/50 p-12 text-center",
          className
        )}
      >
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-muted">
          {emptyIcon || <Newspaper className="h-8 w-8 text-muted-foreground" />}
        </div>
        <p className="text-lg font-medium text-foreground">{emptyMessage || defaultEmptyMsg}</p>
        <p className="mt-1 text-sm text-muted-foreground">{t("news.newPublicationsWillAppear")}</p>
        {onRefresh && (
          <RefreshIconButton
            onClick={onRefresh}
            disabled={isRefreshing}
            label={t("news.refresh")}
            variant="outline"
            className="mt-4 border-border text-muted-foreground hover:bg-accent hover:text-foreground"
          />
        )}
      </div>
    )
  }

  // Sort news: pinned first, then by date
  const sortedNews = [...news].sort((a, b) => {
    if (a.isPinned && !b.isPinned) return -1
    if (!a.isPinned && b.isPinned) return 1
    return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  })

  return (
    <div className={cn("space-y-4", className)}>
      {onRefresh && (
        <div className="flex justify-end">
          <RefreshIconButton
            onClick={onRefresh}
            disabled={isRefreshing}
            label={t("news.refresh")}
            className="h-8 text-muted-foreground hover:text-foreground"
          />
        </div>
      )}

      <div className={cn(columns > 1 ? `grid gap-4 md:grid-cols-${columns}` : "space-y-4")}>
        {sortedNews.map((item, index) => (
          <NewsCard
            key={item.id}
            {...item}
            variant={variant === "featured" && index === 0 ? "featured" : variant}
            canManage={canManage}
            onEdit={onEdit ? () => onEdit(item.id) : undefined}
            onDelete={onDelete ? () => onDelete(item.id) : undefined}
            onTogglePin={onTogglePin ? () => onTogglePin(item.id, !item.isPinned) : undefined}
            onChangeVisibility={
              onChangeVisibility ? (visibility) => onChangeVisibility(item.id, visibility) : undefined
            }
          />
        ))}
      </div>

      {hasMore && (
        <div className="flex justify-center pt-4">
          <Button
            variant="outline"
            onClick={onLoadMore}
            disabled={isLoadingMore}
            className="border-border text-muted-foreground hover:bg-accent hover:text-foreground"
          >
            {isLoadingMore ? (
              <>
                <RefreshCw className="mr-2 h-4 w-4 animate-spin" />
                {t("news.loading")}
              </>
            ) : (
              t("news.showMore")
            )}
          </Button>
        </div>
      )}
    </div>
  )
}
