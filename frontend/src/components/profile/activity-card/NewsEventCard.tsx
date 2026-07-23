"use client"

import { useState, useCallback } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import Image from "next/image"
import { Globe, Lock, Users, Pin, MoreHorizontal, ImageOff, Heart, MessageSquare, Share2, Check } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import type { NewsContent } from "./types"
import {
    normalizeImageUrl,
    getVisibilityType,
} from "./helpers"
import { ImageLightbox } from "./ImageLightbox"
import { NewsComments } from "@/components/news/NewsComments"
import { useToggleNewsLike } from "@/lib/api/queries/news"
import { cn } from "@/lib/utils"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"

/**
 * Props for NewsContentText component
 */
interface NewsContentTextProps {
    content: string
    maxLength?: number
    t: (key: string, values?: Record<string, string | number>) => string
}

/**
 * Component to display news content with "show more" functionality
 */
function NewsContentText({ content, maxLength = 300, t }: Readonly<NewsContentTextProps>) {
    const [isExpanded, setIsExpanded] = useState(false)
    // Normalize newlines so single \n becomes a Markdown line break (two trailing spaces + \n)
    const normalizedContent = content.replace(/(?<!\n)\n(?!\n)/g, "  \n")
    const shouldTruncate = normalizedContent.length > maxLength
    const displayContent = shouldTruncate && !isExpanded ? normalizedContent.slice(0, maxLength) + "..." : normalizedContent
    const handleToggleExpanded = useCallback(() => setIsExpanded(prev => !prev), [])

    return (
        <>
            <ReactMarkdown remarkPlugins={[remarkGfm]}>
                {displayContent}
            </ReactMarkdown>
            {shouldTruncate && (
                <button
                    onClick={handleToggleExpanded}
                    className="block mt-2 text-primary hover:text-primary/80 text-sm font-medium"
                >
                    {isExpanded ? t("news.collapse") : t("news.readMore")}
                </button>
            )}
        </>
    )
}

/**
 * Props for NewsCardHeader
 */
interface NewsCardHeaderProps {
    activity: UserActivityFeedItem
    actorName: string
    avatarInitial: string
    timeAgo: string
    visibilityType: "members" | "subscribers" | "public"
    t: (key: string) => string
}

/**
 * Renders the header section of a news card
 */
function NewsCardHeader({
    activity,
    actorName,
    avatarInitial,
    timeAgo,
    visibilityType,
    t,
}: Readonly<NewsCardHeaderProps>) {
    const VisibilityIcon = visibilityType === "members" ? Lock : visibilityType === "subscribers" ? Users : Globe

    return (
        <div className="p-4 pb-3">
            <div className="flex items-start gap-3">
                <Avatar className="h-12 w-12 ring-2 ring-border shadow-lg">
                    {activity.actorAvatarUrl ? (
                        <AvatarImage src={activity.actorAvatarUrl} alt={actorName} />
                    ) : (
                        <AvatarFallback className="bg-primary/10 text-base font-bold text-primary">
                            {avatarInitial}
                        </AvatarFallback>
                    )}
                </Avatar>
                <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2">
                        <Link
                            href={`/dashboard/profile/${activity.actorId}`}
                            className="font-bold text-foreground hover:underline"
                        >
                            {actorName}
                        </Link>
                        {activity.projectId && activity.projectTitle && (
                            <span className="text-muted-foreground">→</span>
                        )}
                        {activity.projectId && activity.projectTitle && (
                            <Link
                                href={`/dashboard/projects/${activity.projectId}`}
                                className="font-semibold text-primary hover:underline"
                            >
                                {activity.projectTitle}
                            </Link>
                        )}
                    </div>
                    <div className="flex items-center gap-2 text-xs text-muted-foreground mt-0.5">
                        <span>{timeAgo}</span>
                        <span>·</span>
                        <div className="flex items-center gap-1">
                            <VisibilityIcon className="h-3 w-3" />
                        </div>
                    </div>
                </div>
                <Button variant="ghost" size="sm" aria-label={t("common.actions")} className="h-8 w-8 p-0 text-muted-foreground hover:text-foreground hover:bg-accent rounded-full">
                    <MoreHorizontal className="h-5 w-5" aria-hidden="true" />
                </Button>
            </div>
        </div>
    )
}

/**
 * Props for NewsCardContent
 */
interface NewsCardContentProps {
    title?: string
    content?: string
    t: (key: string, values?: Record<string, string | number>) => string
}

/**
 * Renders the content section of a news card
 */
function NewsCardContent({ title, content, t }: Readonly<NewsCardContentProps>) {
    if (!title && !content) return null

    return (
        <div className="px-4 pb-3">
            {title && (
                <h2 className="text-xl font-bold text-foreground mb-2 leading-tight">
                    {title}
                </h2>
            )}
            {content && (
                <div className="prose prose-sm prose-invert max-w-none text-muted-foreground prose-p:my-1 prose-ul:my-1 prose-li:my-0.5 prose-table:my-2 prose-th:px-3 prose-th:py-1.5 prose-td:px-3 prose-td:py-1.5">
                    <NewsContentText content={content} t={t} />
                </div>
            )}
        </div>
    )
}

/**
 * Props for NewsImageGallery
 */
interface NewsImageGalleryProps {
    images: string[]
    onImageClick: (index: number) => void
    t: (key: string, values?: Record<string, string | number>) => string
}

/**
 * Single image item with error handling
 */
function NewsImageItem({
    imageUrl,
    index,
    totalImages,
    onImageClick,
    t,
}: {
    imageUrl: string
    index: number
    totalImages: number
    onImageClick: (index: number) => void
    t: (key: string, values?: Record<string, string | number>) => string
}) {
    const [hasError, setHasError] = useState(false)

    const handleError = useCallback(() => setHasError(true), [])
    const handleClick = useCallback(() => {
        if (!hasError) onImageClick(index)
    }, [hasError, onImageClick, index])

    return (
        <button
            type="button"
            className="relative h-full w-full cursor-pointer overflow-hidden group border-0 p-0"
            onClick={handleClick}
        >
            {hasError ? (
                <div className="absolute inset-0 flex items-center justify-center bg-muted">
                    <ImageOff className="h-8 w-8 text-muted-foreground" />
                </div>
            ) : (
                <Image
                    src={normalizeImageUrl(imageUrl)}
                    alt={t("activity.imageAlt", { current: index + 1, total: totalImages })}
                    fill
                    className="object-cover group-hover:scale-105 transition-transform duration-300"
                    sizes="(max-width: 768px) 50vw, 300px"
                    unoptimized
                    onError={handleError}
                />
            )}
        </button>
    )
}

/**
 * Renders the image gallery section of a news card
 */
function NewsImageGallery({ images, onImageClick, t }: Readonly<NewsImageGalleryProps>) {
    if (images.length === 0) return null

    const displayImages = images.slice(0, 4)
    const isSingle = displayImages.length === 1
    const hiddenCount = images.length > 4 ? images.length - 4 : 0

    return (
        <div className={cn(
            "relative mx-4 mb-3 overflow-hidden rounded-xl bg-muted/50",
            isSingle ? "h-64" : "h-48"
        )}>
            {isSingle ? (
                <NewsImageItem
                    imageUrl={displayImages[0]}
                    index={0}
                    totalImages={images.length}
                    onImageClick={onImageClick}
                    t={t}
                />
            ) : (
                <div className="grid h-full grid-cols-2 gap-0.5">
                    {displayImages.map((imageUrl, index) => (
                        <div key={imageUrl || index} className="relative overflow-hidden">
                            <NewsImageItem
                                imageUrl={imageUrl}
                                index={index}
                                totalImages={images.length}
                                onImageClick={onImageClick}
                                t={t}
                            />
                            {index === displayImages.length - 1 && hiddenCount > 0 && (
                                <div className="absolute inset-0 flex items-center justify-center bg-black/60 pointer-events-none">
                                    <span className="text-2xl font-bold text-white">+{hiddenCount}</span>
                                </div>
                            )}
                        </div>
                    ))}
                </div>
            )}
        </div>
    )
}

/**
 * Props for NewsCardFooter
 */
interface NewsCardFooterProps {
    newsContent: NewsContent
    onToggleLike?: () => void
    onToggleComments: () => void
    commentsOpen: boolean
    t: (key: string, values?: Record<string, string | number>) => string
}

/**
 * Renders the social footer section of a news card
 */
function NewsCardFooter({ newsContent, onToggleLike, onToggleComments, commentsOpen, t }: Readonly<NewsCardFooterProps>) {
    const liked = newsContent.isLikedByCurrentUser ?? false
    const likes = newsContent.likesCount ?? 0
    const comments = newsContent.commentsCount ?? 0
    const { toast } = useToast()
    const [copied, setCopied] = useState(false)

    const handleShare = useCallback(async () => {
        const url = window.location.href
        const title = newsContent.title || ""
        if (typeof navigator.share === "function") {
            try {
                await navigator.share({ title, url })
                return
            } catch { /* user cancelled or unsupported */ }
        }
        try {
            await navigator.clipboard.writeText(url)
            setCopied(true)
            toast({ title: t("news.linkCopied") })
            setTimeout(() => setCopied(false), 2000)
        } catch { /* clipboard denied */ }
    }, [newsContent.title, toast, t])

    return (
        <div className="px-4 py-2 border-t border-border">
            <div className="flex items-center justify-between">
                <div className="flex items-center gap-1">
                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={onToggleLike}
                        className={cn(
                            "h-8 gap-1.5 text-muted-foreground hover:text-rose-400 hover:bg-rose-500/10",
                            liked && "text-rose-500"
                        )}
                    >
                        <Heart className={cn("h-4 w-4", liked && "fill-current")} />
                        <span className="text-xs">{likes > 0 ? likes : ""}</span>
                    </Button>

                    <Button
                        variant="ghost"
                        size="sm"
                        onClick={onToggleComments}
                        className={cn(
                            "h-8 gap-1.5 text-muted-foreground hover:text-primary hover:bg-primary/10",
                            commentsOpen && "text-primary"
                        )}
                    >
                        <MessageSquare className="h-4 w-4" />
                        <span className="text-xs">{comments > 0 ? comments : ""}</span>
                    </Button>
                </div>

                <div className="flex items-center gap-1">
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={handleShare}
                        className="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-accent"
                    >
                        {copied ? <Check className="h-4 w-4 text-green-500" /> : <Share2 className="h-4 w-4" />}
                    </Button>

                    {newsContent.newsPostId && (
                        <ReportDialog targetType={REPORT_TARGET_TYPES.NEWS_POST} targetId={newsContent.newsPostId} />
                    )}
                </div>
            </div>
        </div>
    )
}

/**
 * Props for the NewsEventCard component
 */
export interface NewsEventCardProps {
    activity: UserActivityFeedItem
    newsContent: NewsContent
    actorName: string
    avatarInitial: string
    timeAgo: string
    context: "global" | "project" | "user"
    t: (key: string, values?: Record<string, string | number>) => string
    lightboxOpen: boolean
    lightboxIndex: number
    setLightboxOpen: (open: boolean) => void
    setLightboxIndex: (index: number) => void
}

/**
 * Renders a social-media style news post card
 */
export function NewsEventCard({
    activity,
    newsContent,
    actorName,
    avatarInitial,
    timeAgo,
    context,
    t,
    lightboxOpen,
    lightboxIndex,
    setLightboxOpen,
    setLightboxIndex,
}: Readonly<NewsEventCardProps>) {
    const images = newsContent.images
    const visibilityType = getVisibilityType(newsContent.visibility)
    const [commentsOpen, setCommentsOpen] = useState(false)

    const toggleLike = useToggleNewsLike()

    const openLightbox = useCallback((index: number) => {
        setLightboxIndex(index)
        setLightboxOpen(true)
    }, [setLightboxIndex, setLightboxOpen])

    const handleToggleLike = useCallback(() => {
        if (newsContent.projectId && newsContent.newsPostId) {
            toggleLike.mutate({ projectId: newsContent.projectId, newsId: newsContent.newsPostId })
        }
    }, [newsContent.projectId, newsContent.newsPostId, toggleLike])

    const handleToggleComments = useCallback(() => {
        setCommentsOpen(prev => !prev)
    }, [])
    const handleCloseLightbox = useCallback(() => setLightboxOpen(false), [setLightboxOpen])

    return (
        <>
            <ImageLightbox
                images={images}
                initialIndex={lightboxIndex}
                isOpen={lightboxOpen}
                onClose={handleCloseLightbox}
                t={t}
            />

            <article className="rounded-2xl bg-card/80 backdrop-blur-sm border border-border overflow-hidden transition-all hover:border-border/80 hover:shadow-xl">
                {/* Pinned indicator */}
                {newsContent.isPinned && context === "project" && (
                    <div className="border-b border-warning/20 bg-warning/10 px-4 py-2">
                        <div className="flex items-center gap-2 text-amber-400 text-xs font-medium">
                            <Pin className="h-3.5 w-3.5" />
                            <span>{t("news.pinned")}</span>
                        </div>
                    </div>
                )}

                <NewsCardHeader
                    activity={activity}
                    actorName={actorName}
                    avatarInitial={avatarInitial}
                    timeAgo={timeAgo}
                    visibilityType={visibilityType}
                    t={t}
                />

                <NewsCardContent
                    title={newsContent.title}
                    content={newsContent.content}
                    t={t}
                />

                <NewsImageGallery
                    images={images}
                    onImageClick={openLightbox}
                    t={t}
                />

                <NewsCardFooter
                    newsContent={newsContent}
                    onToggleLike={handleToggleLike}
                    onToggleComments={handleToggleComments}
                    commentsOpen={commentsOpen}
                    t={t}
                />

                {/* Comments section */}
                {commentsOpen && newsContent.projectId && newsContent.newsPostId && (
                    <NewsComments projectId={newsContent.projectId} newsId={newsContent.newsPostId} />
                )}
            </article>
        </>
    )
}
