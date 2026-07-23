"use client"

import Image from "next/image"
import { useCallback } from "react"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Upload, Plus, Image as ImageIcon } from "lucide-react"
import { useTranslations } from "next-intl"
import { NewsFeed } from "@/components/news"
import type { ProjectMediaItem } from "@/hooks/use-gallery-lightbox"
import type { Project } from "@/lib/api/schema"
import type { ProjectNewsItem } from "@/lib/api/queries/news"

type ViewProject = Omit<Project, "maxTeamSize"> & { maxTeamSize?: number | null }

interface MediaThumbnailProps {
  media: ProjectMediaItem
  index: number
  onOpen: (media: ProjectMediaItem, index: number) => void
  t: ReturnType<typeof useTranslations>
}

function MediaThumbnail({ media, index, onOpen, t }: MediaThumbnailProps) {
  const handleClick = useCallback(() => onOpen(media, index), [onOpen, media, index])
  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault()
      onOpen(media, index)
    }
  }, [onOpen, media, index])

  return (
    <div
      className="relative aspect-video rounded-lg overflow-hidden border bg-muted group cursor-pointer"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
      aria-label={t("news.viewImage", { name: media.name || "" })}
    >
      {media.url ? (
        <Image fill src={media.url} alt={media.name || ""} className="object-cover group-hover:scale-105 transition-transform" />
      ) : (
        <div className="flex items-center justify-center w-full h-full text-muted-foreground">
          <ImageIcon className="h-8 w-8" />
        </div>
      )}
    </div>
  )
}

interface OverviewTabContentProps {
  readonly project: ViewProject
  readonly teamSize: number
  readonly projectMedia: ProjectMediaItem[]
  readonly projectNewsData: ProjectNewsItem[] | null
  readonly newsLoading: boolean
  readonly newsRefetching: boolean
  readonly permissions: {
    readonly canManageGallery?: boolean
    readonly canManageFiles?: boolean
    readonly canPublishNews?: boolean
  } | null
  readonly onOpenGalleryImage: (media: ProjectMediaItem, index: number) => void
  readonly onOpenUploadMediaDialog: () => void
  readonly onOpenAddNewsDialog: () => void
  readonly onRefreshNews: () => void
  readonly onDeleteNews: (id: string) => void
  readonly onTogglePinNews: (id: string, nextValue: boolean) => void
  readonly onChangeNewsVisibility: (id: string, visibility: ProjectNewsItem["visibility"] | undefined) => void
}

export function OverviewTabContent({
  project: _project,
  teamSize: _teamSize,
  projectMedia,
  projectNewsData,
  newsLoading,
  newsRefetching,
  permissions,
  onOpenGalleryImage,
  onOpenUploadMediaDialog,
  onOpenAddNewsDialog,
  onRefreshNews,
  onDeleteNews,
  onTogglePinNews,
  onChangeNewsVisibility,
}: OverviewTabContentProps) {
  const t = useTranslations()

  return (
    <div className="space-y-4">

      {/* Gallery Section */}
      <Card className="border border-border/70 bg-card/80 shadow-sm">
        <CardHeader className="flex flex-row items-center justify-between">
          <div className="space-y-1.5">
            <CardTitle className="text-sm font-semibold uppercase">{t("news.projectGallery")}</CardTitle>
            <CardDescription>{t("news.projectImagesAndMedia")}</CardDescription>
          </div>
          {(permissions?.canManageGallery || permissions?.canManageFiles) && (
            <Button size="sm" variant="outline" onClick={onOpenUploadMediaDialog}>
              <Upload className="h-4 w-4 mr-2" />
              {t("news.upload")}
            </Button>
          )}
        </CardHeader>
        <CardContent className="space-y-3">
          {Array.isArray(projectMedia) && projectMedia.length > 0 ? (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              {projectMedia.slice(0, 8).map((media, index) => (
                <MediaThumbnail
                  key={media.id ?? media.name}
                  media={media}
                  index={index}
                  onOpen={onOpenGalleryImage}
                  t={t}
                />
              ))}
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-muted/30 p-6 text-center">
              <ImageIcon className="h-8 w-8 text-muted-foreground mb-2" />
              <p className="text-sm text-muted-foreground">{t("news.mediaNotUploaded")}</p>
              {(permissions?.canManageGallery || permissions?.canManageFiles) && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={onOpenUploadMediaDialog}
                  className="mt-3"
                >
                  <Upload className="h-4 w-4 mr-2" />
                  {t("news.uploadFirstPhoto")}
                </Button>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* News Section */}
      <Card className="border border-border bg-card shadow-lg">
        <CardHeader className="flex flex-row items-center justify-between border-b border-border pb-4">
          <div className="space-y-1">
            <CardTitle className="text-lg font-semibold text-foreground">{t("news.projectNews")}</CardTitle>
            <CardDescription className="text-muted-foreground">
              {t("news.latestUpdatesAndAnnouncements")}
            </CardDescription>
          </div>
          {permissions?.canPublishNews && (
            <Button
              size="sm"
              onClick={onOpenAddNewsDialog}
              className="bg-primary hover:bg-primary/90 text-primary-foreground"
            >
              <Plus className="h-4 w-4 mr-2" />
              {t("news.publish")}
            </Button>
          )}
        </CardHeader>
        <CardContent className="pt-4">
          {(() => {
            if (newsLoading) {
              return (
                <div className="space-y-4">
                  {[1, 2].map((i) => (
                    <div key={i} className="animate-pulse space-y-3">
                      <div className="flex items-center gap-3">
                        <div className="h-10 w-10 rounded-full bg-muted" />
                        <div className="space-y-2">
                          <div className="h-4 w-32 rounded bg-muted" />
                          <div className="h-3 w-24 rounded bg-muted" />
                        </div>
                      </div>
                      <div className="h-5 w-3/4 rounded bg-muted" />
                      <div className="space-y-2">
                        <div className="h-4 w-full rounded bg-muted" />
                        <div className="h-4 w-2/3 rounded bg-muted" />
                      </div>
                    </div>
                  ))}
                </div>
              )
            }

            if (!projectNewsData || projectNewsData.length === 0) {
              return (
                <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-border bg-muted/50 p-8 text-center">
                  <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-full bg-muted">
                    <svg
                      xmlns="http://www.w3.org/2000/svg"
                      className="h-6 w-6 text-muted-foreground"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth={1.5}
                        d="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z"
                      />
                    </svg>
                  </div>
                  <p className="font-medium text-foreground">{t("news.noNews")}</p>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {permissions?.canPublishNews
                      ? t("news.publishFirstNews")
                      : t("news.newsWillAppearHere")}
                  </p>
                  {permissions?.canPublishNews && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={onOpenAddNewsDialog}
                      className="mt-4 border-border text-muted-foreground hover:bg-accent hover:text-foreground"
                    >
                      <Plus className="h-4 w-4 mr-2" />
                      {t("news.addNews")}
                    </Button>
                  )}
                </div>
              )
            }

            const sortedNews = [...projectNewsData].sort((a, b) => {
              if (a.isPinned && !b.isPinned) return -1
              if (!a.isPinned && b.isPinned) return 1
              return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
            }).map((item) => ({
              id: item.id,
              projectId: item.projectId,
              title: item.title,
              content: item.content,
              authorName: item.authorName ?? t("news.author"),
              authorAvatarUrl: item.authorAvatarUrl,
              visibility: item.visibility,
              isPinned: item.isPinned,
              createdAt: item.createdAt,
              attachments: item.attachments,
            }))

            return (
              <NewsFeed
                news={sortedNews}
                onRefresh={onRefreshNews}
                isRefreshing={newsRefetching}
                canManage={permissions?.canPublishNews}
                onDelete={onDeleteNews}
                onTogglePin={onTogglePinNews}
                onChangeVisibility={onChangeNewsVisibility}
                variant="default"
              />
            )
          })()}
        </CardContent>
      </Card>
    </div>
  )
}
