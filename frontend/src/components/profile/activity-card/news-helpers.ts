import type { ActivityPayload, NewsContent } from "./types"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import { safeJsonParse } from "./utils"

/**
 * Creates a default news content object with optional overrides
 */
function createNewsContent(overrides: Partial<NewsContent> = {}): NewsContent {
  return {
    title: undefined,
    content: undefined,
    images: [],
    isPinned: false,
    visibility: "public",
    ...overrides,
  }
}

/**
 * Gets a value from payload, checking both camelCase and PascalCase variants
 */
function getPayloadValue<T>(
  payload: ActivityPayload,
  camelKey: keyof ActivityPayload,
  pascalKey: keyof ActivityPayload,
  defaultValue: T
): T {
  const value = payload[camelKey] ?? payload[pascalKey]
  return (value as T) ?? defaultValue
}

/**
 * Extracts news content from a parsed payload
 */
function extractNewsFromPayload(payload: ActivityPayload): NewsContent | null {
  const title = getPayloadValue(payload, "title", "Title", undefined)
  const content = getPayloadValue(payload, "content", "Content", undefined)

  if (!title && !content) return null

  return createNewsContent({
    title,
    content,
    images: getPayloadValue(payload, "images", "Images", []),
    isPinned: getPayloadValue(payload, "isPinned", "IsPinned", false),
    visibility: getPayloadValue(payload, "visibility", "Visibility", "public"),
  })
}

/**
 * Extracts news title from summary text using pattern matching
 */
function extractTitleFromSummary(summary: string | undefined): string | null {
  if (!summary) return null
  const pattern = /(?:Published news|Opublikowano wiadomość)[:\s]+(.+)/i
  const match = pattern.exec(summary)
  return match ? match[1] : null
}

/**
 * Get payload JSON string from activity (supports both camelCase and PascalCase)
 */
function getPayloadJson(activity: UserActivityFeedItem): string | undefined {
  return (
    activity.payloadJson ??
    ((activity as unknown as Record<string, unknown>).PayloadJson as string | undefined)
  )
}

/**
 * Extracts image URLs from enriched news attachments
 */
function extractImagesFromAttachments(
  attachments?: Array<{
    type: string
    url?: string
    downloadUrl?: string
    contentType?: string
    fileName?: string
  }>
): string[] {
  if (!attachments || attachments.length === 0) return []
  return attachments
    .filter(
      (a) =>
        a.type === "image" ||
        a.contentType?.startsWith("image/") ||
        a.url?.match(/\.(jpg|jpeg|png|gif|webp)$/i) ||
        a.fileName?.match(/\.(jpg|jpeg|png|gif|webp)$/i)
    )
    .map((a) => a.url || a.downloadUrl || "")
    .filter(Boolean)
}

/**
 * Extract news content from activity payload.
 * Prefers enriched fields from feed endpoint over PayloadJson fallback.
 */
export function extractNewsContent(activity: UserActivityFeedItem): NewsContent | null {
  if (activity.eventType !== "project.news_published") return null

  // Prefer enriched fields from feed endpoint (full content, fresh data)
  if (activity.newsContent || activity.newsTitle) {
    const enrichedImages = extractImagesFromAttachments(activity.newsAttachments)
    // Also try payload for images if enriched attachments are empty
    const payload = safeJsonParse(getPayloadJson(activity)) as ActivityPayload | null
    const payloadImages = payload
      ? getPayloadValue(payload, "images", "Images", [] as string[])
      : []
    const images = enrichedImages.length > 0 ? enrichedImages : payloadImages

    return createNewsContent({
      newsPostId: activity.newsPostId,
      projectId: activity.projectId,
      title: activity.newsTitle,
      content: activity.newsContent,
      images,
      isPinned: payload ? getPayloadValue(payload, "isPinned", "IsPinned", false) : false,
      visibility: activity.visibility || "public",
      likesCount: activity.likesCount,
      commentsCount: activity.commentsCount,
      isLikedByCurrentUser: activity.isLikedByCurrentUser,
    })
  }

  // Fallback: parse from PayloadJson
  const payload = safeJsonParse(getPayloadJson(activity)) as ActivityPayload | null

  if (payload) {
    const payloadContent = extractNewsFromPayload(payload)
    if (payloadContent) {
      payloadContent.projectId = activity.projectId
      return payloadContent
    }
  }

  const extractedTitle = extractTitleFromSummary(activity.summary)
  if (extractedTitle) {
    return createNewsContent({ title: extractedTitle, projectId: activity.projectId })
  }

  if (activity.summary) {
    return createNewsContent({ content: activity.summary, projectId: activity.projectId })
  }

  return createNewsContent({ projectId: activity.projectId })
}
