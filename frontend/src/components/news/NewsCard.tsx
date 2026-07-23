"use client"

import { formatDistanceToNow, format } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import { useTranslations, useLocale } from "next-intl"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardFooter, CardHeader } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import {
  Calendar,
  Eye,
  Globe,
  Image as ImageIcon,
  MessageSquare,
  MoreHorizontal,
  Pin,
  Share2,
  Users,
  Lock,
  Heart,
  Bookmark,
  Check,
} from "lucide-react";
import { cn } from "@/lib/utils"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"
import { useToast } from "@/hooks/use-toast"
import { useState, useCallback } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import NextImage from "next/image"
import { ImageLightbox } from "@/components/profile/activity-card/ImageLightbox"
import { NewsComments } from "./NewsComments"
import { useToggleNewsLike, useNewsLikeStatus } from "@/lib/api/queries/news"

export type NewsAttachment = {
  type: string
  fileId?: string
  url?: string
  fileName?: string
  contentType?: string
  downloadUrl?: string
}

/**
 * Get the display URL for an attachment
 * Handles both external URLs and API file paths
 */
function getAttachmentUrl(att: NewsAttachment): string {
  // External URL takes priority
  if (att.url && att.url.startsWith("http")) {
    return att.url
  }

  // For API paths, prefix with proxy path
  if (att.downloadUrl) {
    // If already starts with /api/proxy-core, use as-is
    if (att.downloadUrl.startsWith("/api/proxy-core")) {
      return att.downloadUrl
    }
    // If starts with /api/, add proxy prefix
    if (att.downloadUrl.startsWith("/api/")) {
      return `/api/proxy-core${att.downloadUrl.slice(4)}`
    }
    return att.downloadUrl
  }

  return ""
}

function ShareButton({ title }: { readonly title: string }) {
  const t = useTranslations()
  const { toast } = useToast()
  const [copied, setCopied] = useState(false)

  const handleShare = useCallback(async () => {
    const url = window.location.href
    if (typeof navigator.share === "function") {
      try {
        await navigator.share({ title, url })
        return
      } catch { /* user cancelled */ }
    }
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
      toast({ title: t("news.linkCopied") })
      setTimeout(() => setCopied(false), 2000)
    } catch { /* clipboard denied */ }
  }, [title, toast, t])

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={handleShare}
      className="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-accent"
    >
      {copied ? <Check className="h-4 w-4 text-green-500" /> : <Share2 className="h-4 w-4" />}
    </Button>
  )
}

export interface NewsCardProps {
  id: string
  projectId?: string
  title: string
  content: string
  authorName: string
  authorAvatarUrl?: string
  projectName?: string
  projectSlug?: string
  visibility?: "public" | "subscribers" | "members"
  isPinned?: boolean
  createdAt: string
  attachments?: NewsAttachment[]
  likesCount?: number
  commentsCount?: number
  viewsCount?: number
  isLiked?: boolean
  isBookmarked?: boolean
  onLike?: () => void
  onBookmark?: () => void
  onEdit?: () => void
  onDelete?: () => void
  onTogglePin?: () => void
  onChangeVisibility?: (visibility: "public" | "subscribers" | "members") => void
  canManage?: boolean
  variant?: "default" | "compact" | "featured"
}

const getVisibilityConfig = (t: (key: string) => string) => ({
  public: { icon: Globe, label: t("news.public"), className: "text-emerald-500" },
  subscribers: { icon: Users, label: t("news.subscribers"), className: "text-blue-500" },
  members: { icon: Lock, label: t("news.members"), className: "text-amber-500" },
})

interface GridImageButtonProps {
  att: NewsAttachment
  idx: number
  totalCount: number
  imageError: Record<string, boolean>
  onOpenLightbox: (idx: number) => void
  onImageError: (url: string) => void
  alt: string
}

function GridImageButton({ att, idx, totalCount, imageError, onOpenLightbox, onImageError, alt }: GridImageButtonProps) {
  const imgUrl = getAttachmentUrl(att)
  const handleClick = useCallback(() => onOpenLightbox(idx), [onOpenLightbox, idx])
  const handleError = useCallback(() => onImageError(imgUrl), [onImageError, imgUrl])

  return (
    <button
      type="button"
      key={att.fileId || imgUrl || idx}
      className={cn("relative cursor-pointer border-0 p-0", totalCount === 3 && idx === 0 && "row-span-2")}
      onClick={handleClick}
    >
      {!imageError[imgUrl] ? (
        <NextImage src={imgUrl} alt={alt} fill className="object-cover" onError={handleError} unoptimized />
      ) : (
        <div className="flex h-full w-full items-center justify-center bg-muted">
          <ImageIcon className="h-8 w-8 text-muted-foreground" />
        </div>
      )}
      {idx === 3 && totalCount > 4 && (
        <div className="absolute inset-0 flex items-center justify-center bg-black/60">
          <span className="text-2xl font-bold text-white">+{totalCount - 4}</span>
        </div>
      )}
    </button>
  )
}

export function NewsCard({
  id,
  projectId,
  title,
  content,
  authorName,
  authorAvatarUrl,
  projectName,
  projectSlug: _projectSlug,
  visibility = "public",
  isPinned,
  createdAt,
  attachments = [],
  likesCount = 0,
  commentsCount = 0,
  viewsCount = 0,
  isLiked = false,
  isBookmarked = false,
  onLike,
  onBookmark,
  onEdit,
  onDelete,
  onTogglePin,
  onChangeVisibility,
  canManage,
  variant = "default",
}: NewsCardProps) {
  const t = useTranslations()
  const locale = useLocale()
  const [isExpanded, setIsExpanded] = useState(false)
  const [imageError, setImageError] = useState<Record<string, boolean>>({})
  const [lightboxOpen, setLightboxOpen] = useState(false)
  const [lightboxIndex, setLightboxIndex] = useState(0)
  const [commentsOpen, setCommentsOpen] = useState(false)

  // Like state: use API status when projectId is available, fallback to props
  const likeStatus = useNewsLikeStatus(projectId ?? "", id)
  const toggleLike = useToggleNewsLike()

  const effectiveLiked = projectId ? (likeStatus.data?.isLiked ?? isLiked) : isLiked
  const effectiveLikesCount = projectId ? (likeStatus.data?.likesCount ?? likesCount) : likesCount

  const handleLike = useCallback(() => {
    if (onLike) {
      onLike()
    } else if (projectId) {
      toggleLike.mutate({ projectId, newsId: id })
    }
  }, [onLike, projectId, id, toggleLike])

  const avatarInitial = (authorName?.trim().charAt(0) || "U").toUpperCase()
  const visibilityConfig = getVisibilityConfig(t)
  const VisibilityIcon = visibilityConfig[visibility].icon

  const timeAgo = (() => {
    try {
      const date = new Date(createdAt)
      if (isNaN(date.getTime())) return t("news.recently")
      return formatDistanceToNow(date, { addSuffix: true, locale: getDateFnsLocale(locale) })
    } catch {
      return t("news.recently")
    }
  })()

  const formattedDate = (() => {
    try {
      const date = new Date(createdAt)
      if (isNaN(date.getTime())) return ""
      return format(date, "d MMMM yyyy, HH:mm", { locale: getDateFnsLocale(locale) })
    } catch {
      return ""
    }
  })()

  // Filter image attachments - check contentType for file attachments
  const imageAttachments = attachments.filter(
    (att) =>
      att.type === "image" ||
      att.contentType?.startsWith("image/") ||
      att.url?.match(/\.(jpg|jpeg|png|gif|webp)$/i) ||
      att.fileName?.match(/\.(jpg|jpeg|png|gif|webp)$/i)
  )

  const imageUrls = imageAttachments.map(att => getAttachmentUrl(att)).filter(Boolean)
  const imageFileIds = imageAttachments.map(att => att.fileId).filter((id): id is string => Boolean(id))

  const openLightbox = useCallback((index: number) => {
    setLightboxIndex(index)
    setLightboxOpen(true)
  }, [])

  const toggleComments = useCallback(() => {
    setCommentsOpen(prev => !prev)
  }, [])

  const handleImageError = useCallback((url: string) => {
    setImageError(prev => ({ ...prev, [url]: true }))
  }, [])

  const firstImageUrl = imageUrls[0]

  function handleFirstImageError() {
    if (firstImageUrl) handleImageError(firstImageUrl)
  }

  const handleOpenFirstImage = useCallback(() => openLightbox(0), [openLightbox])

  const handleToggleExpanded = useCallback(() => setIsExpanded(prev => !prev), [])

  const handleCloseLightbox = useCallback(() => setLightboxOpen(false), [])

  const handleChangeVisibilityPublic = useCallback(() => onChangeVisibility?.("public"), [onChangeVisibility])
  const handleChangeVisibilitySubscribers = useCallback(() => onChangeVisibility?.("subscribers"), [onChangeVisibility])
  const handleChangeVisibilityMembers = useCallback(() => onChangeVisibility?.("members"), [onChangeVisibility])

  // Normalize newlines so single \n becomes a Markdown line break (two trailing spaces + \n)
  // This prevents content from collapsing into a single line
  const normalizedContent = content.replace(/(?<!\n)\n(?!\n)/g, "  \n")

  // Truncate content for compact view
  const shouldTruncate = normalizedContent.length > 300 && variant !== "featured"
  const displayContent = shouldTruncate && !isExpanded ? normalizedContent.slice(0, 300) + "..." : normalizedContent

  const isFeatured = variant === "featured"
  const isCompact = variant === "compact"

  return (
    <Card
      className={cn(
        "group relative overflow-hidden transition-all duration-300",
        "border border-border bg-card",
        "hover:border-border/80 hover:shadow-xl hover:shadow-primary/5",
        isPinned && "ring-1 ring-amber-500/30",
        isFeatured && "md:col-span-2"
      )}
    >
      {/* Pinned indicator */}
      {isPinned && (
        <div className="absolute top-0 right-0 z-10">
          <div className="flex items-center gap-1.5 rounded-bl-lg bg-amber-500/90 px-3 py-1.5 text-xs font-medium text-black">
            <Pin className="h-3 w-3" />
            {t("news.pinned")}
          </div>
        </div>
      )}

      {/* Image gallery */}
      {imageAttachments.length > 0 && (
        <div className={cn(
          "relative overflow-hidden bg-muted/50",
          isFeatured ? "h-64 md:h-80" : "h-48"
        )}>
          {imageAttachments.length === 1 ? (
            (() => {
              const imgUrl = getAttachmentUrl(imageAttachments[0])
              return (
                <button
                  type="button"
                  className="relative h-full w-full cursor-pointer border-0 p-0"
                  onClick={handleOpenFirstImage}
                >
                  {!imageError[imgUrl] ? (
                    <NextImage
                      src={imgUrl}
                      alt={imageAttachments[0].fileName || title}
                      fill
                      className="object-cover transition-transform duration-500 group-hover:scale-105"
                      onError={handleFirstImageError}
                      unoptimized
                    />
                  ) : (
                    <div className="flex h-full w-full items-center justify-center bg-muted">
                      <ImageIcon className="h-12 w-12 text-muted-foreground" />
                    </div>
                  )}
                  <div className="absolute inset-0 bg-gradient-to-t from-background/80 via-transparent to-transparent" />
                </button>
              )
            })()
          ) : (
            <div className="grid h-full grid-cols-2 gap-0.5">
              {imageAttachments.slice(0, 4).map((att, idx) => (
                <GridImageButton
                  key={att.fileId || getAttachmentUrl(att) || idx}
                  att={att}
                  idx={idx}
                  totalCount={imageAttachments.length}
                  imageError={imageError}
                  onOpenLightbox={openLightbox}
                  onImageError={handleImageError}
                  alt={att.fileName || t("news.image", { index: idx + 1 })}
                />
              ))}
            </div>
          )}
        </div>
      )}

      <CardHeader className={cn("pb-3", isCompact && "pb-2")}>
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3">
            <Avatar className={cn(
              "ring-2 ring-border transition-all group-hover:ring-primary/30",
              isCompact ? "h-8 w-8" : "h-10 w-10"
            )}>
              {authorAvatarUrl ? (
                <AvatarImage src={authorAvatarUrl} alt={authorName} />
              ) : (
                <AvatarFallback className="bg-gradient-to-br from-blue-500 to-purple-600 text-sm font-semibold text-white">
                  {avatarInitial}
                </AvatarFallback>
              )}
            </Avatar>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <span className={cn(
                  "font-semibold text-foreground truncate",
                  isCompact ? "text-sm" : "text-base"
                )}>
                  {authorName}
                </span>
                {projectName && (
                  <>
                    <span className="text-muted-foreground">→</span>
                    <Badge
                      variant="secondary"
                      className="bg-blue-500/10 text-blue-400 border-blue-500/20 hover:bg-blue-500/20 cursor-pointer"
                    >
                      {projectName}
                    </Badge>
                  </>
                )}
              </div>
              <div className="mt-0.5 flex items-center gap-2 text-xs text-muted-foreground">
                <Calendar className="h-3 w-3" />
                <time dateTime={createdAt} title={formattedDate}>
                  {timeAgo}
                </time>
                <span className="text-muted-foreground/50">•</span>
                <div className={cn("flex items-center gap-1", visibilityConfig[visibility].className)}>
                  <VisibilityIcon className="h-3 w-3" />
                  <span>{visibilityConfig[visibility].label}</span>
                </div>
              </div>
            </div>
          </div>

          {canManage && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-accent"
                >
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="bg-card border-border">
                {onEdit && (
                  <DropdownMenuItem onClick={onEdit} className="text-muted-foreground hover:text-foreground">
                    {t("news.edit")}
                  </DropdownMenuItem>
                )}
                {onTogglePin && (
                  <DropdownMenuItem onClick={onTogglePin} className="text-muted-foreground hover:text-foreground">
                    {isPinned ? t("news.unpin") : t("news.pin")}
                  </DropdownMenuItem>
                )}
                {onChangeVisibility && (
                  <>
                    <DropdownMenuItem
                      onClick={handleChangeVisibilityPublic}
                      className="text-muted-foreground hover:text-foreground"
                    >
                      {t("news.makePublic")}
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={handleChangeVisibilitySubscribers}
                      className="text-muted-foreground hover:text-foreground"
                    >
                      {t("news.onlySubscribers")}
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={handleChangeVisibilityMembers}
                      className="text-muted-foreground hover:text-foreground"
                    >
                      {t("news.onlyTeam")}
                    </DropdownMenuItem>
                  </>
                )}
                {onDelete && (
                  <DropdownMenuItem onClick={onDelete} className="text-red-400 hover:text-red-300">
                    {t("news.delete")}
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          )}
        </div>
      </CardHeader>

      <CardContent className={cn("space-y-3", isCompact && "pb-3")}>
        <h3 className={cn(
          "font-bold text-foreground leading-tight",
          isFeatured ? "text-xl md:text-2xl" : isCompact ? "text-base" : "text-lg"
        )}>
          {title}
        </h3>

        <div className={cn(
          "prose prose-sm prose-invert max-w-none text-muted-foreground",
          "prose-p:my-1 prose-ul:my-1 prose-li:my-0.5 prose-table:my-2",
          "prose-th:px-3 prose-th:py-1.5 prose-td:px-3 prose-td:py-1.5",
          isCompact && "text-sm"
        )}>
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{displayContent}</ReactMarkdown>
        </div>

        {shouldTruncate && (
          <Button
            variant="link"
            size="sm"
            onClick={handleToggleExpanded}
            className="h-auto p-0 text-blue-400 hover:text-blue-300"
          >
            {isExpanded ? t("news.collapse") : t("news.readMore")}
          </Button>
        )}
      </CardContent>

      {!isCompact && (
        <CardFooter className="border-t border-border pt-3">
          <div className="flex w-full items-center justify-between">
            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                onClick={handleLike}
                className={cn(
                  "h-8 gap-1.5 text-muted-foreground hover:text-rose-400 hover:bg-rose-500/10",
                  effectiveLiked && "text-rose-500"
                )}
              >
                <Heart className={cn("h-4 w-4", effectiveLiked && "fill-current")} />
                <span className="text-xs">{effectiveLikesCount > 0 ? effectiveLikesCount : ""}</span>
              </Button>

              <Button
                variant="ghost"
                size="sm"
                onClick={toggleComments}
                className={cn(
                  "h-8 gap-1.5 text-muted-foreground hover:text-primary hover:bg-primary/10",
                  commentsOpen && "text-primary"
                )}
              >
                <MessageSquare className="h-4 w-4" />
                <span className="text-xs">{commentsCount > 0 ? commentsCount : ""}</span>
              </Button>

              {viewsCount > 0 && (
                <div className="flex items-center gap-1.5 px-2 text-xs text-muted-foreground">
                  <Eye className="h-3.5 w-3.5" />
                  <span>{viewsCount}</span>
                </div>
              )}
            </div>

            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="icon"
                onClick={onBookmark}
                className={cn(
                  "h-8 w-8 text-muted-foreground hover:text-amber-400 hover:bg-amber-500/10",
                  isBookmarked && "text-amber-500"
                )}
              >
                <Bookmark className={cn("h-4 w-4", isBookmarked && "fill-current")} />
              </Button>

              <ShareButton title={title} />

              <ReportDialog targetType={REPORT_TARGET_TYPES.NEWS_POST} targetId={id} />
            </div>
          </div>
        </CardFooter>
      )}

      {/* Comments section */}
      {commentsOpen && projectId && (
        <NewsComments projectId={projectId} newsId={id} />
      )}

      {/* Image lightbox */}
      {imageUrls.length > 0 && (
        <ImageLightbox
          images={imageUrls}
          imageIds={imageFileIds.length === imageUrls.length ? imageFileIds : undefined}
          initialIndex={lightboxIndex}
          isOpen={lightboxOpen}
          onClose={handleCloseLightbox}
          t={t}
        />
      )}
    </Card>
  )
}

/**
 * Skeleton loader for NewsCard
 */
export function NewsCardSkeleton({ variant = "default" }: { variant?: "default" | "compact" | "featured" }) {
  const isFeatured = variant === "featured"
  const isCompact = variant === "compact"

  return (
    <Card className={cn(
      "overflow-hidden border border-border bg-card",
      isFeatured && "md:col-span-2"
    )}>
      {!isCompact && (
        <div className={cn(
          "bg-muted/50 animate-pulse",
          isFeatured ? "h-64 md:h-80" : "h-48"
        )} />
      )}
      <CardHeader className="pb-3">
        <div className="flex items-start gap-3">
          <div className={cn(
            "rounded-full bg-muted animate-pulse",
            isCompact ? "h-8 w-8" : "h-10 w-10"
          )} />
          <div className="flex-1 space-y-2">
            <div className="h-4 w-32 bg-muted rounded animate-pulse" />
            <div className="h-3 w-24 bg-muted rounded animate-pulse" />
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="h-6 w-3/4 bg-muted rounded animate-pulse" />
        <div className="space-y-2">
          <div className="h-4 w-full bg-muted rounded animate-pulse" />
          <div className="h-4 w-full bg-muted rounded animate-pulse" />
          <div className="h-4 w-2/3 bg-muted rounded animate-pulse" />
        </div>
      </CardContent>
      {!isCompact && (
        <CardFooter className="border-t border-border pt-3">
          <div className="flex w-full gap-4">
            <div className="h-8 w-16 bg-muted rounded animate-pulse" />
            <div className="h-8 w-16 bg-muted rounded animate-pulse" />
          </div>
        </CardFooter>
      )}
    </Card>
  )
}
