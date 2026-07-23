import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"

/**
 * Safely parses JSON, returning undefined on failure
 */
export function safeJsonParse(value: unknown): unknown {
    if (typeof value !== "string" || value.trim().length === 0) return undefined
    try {
        return JSON.parse(value)
    } catch {
        return undefined
    }
}

/**
 * Normalize image URL for display
 */
export function normalizeImageUrl(url: string): string {
    if (!url) return ""
    if (url.startsWith("http")) return url
    if (url.startsWith("/api/") && !url.startsWith("/api/proxy-core")) {
        return `/api/proxy-core${url.slice(4)}`
    }
    return url
}

/**
 * Computes grid class based on image count
 */
export function getImageGridClass(imageCount: number): string {
    if (imageCount === 1) return ""
    if (imageCount === 2) return "grid grid-cols-2 gap-1"
    return "grid grid-cols-3 gap-1"
}

/**
 * Computes image container class based on image count
 */
export function getImageContainerClass(imageCount: number): string {
    if (imageCount === 1) return "aspect-video max-h-[300px]"
    if (imageCount === 2) return "aspect-square max-h-[200px]"
    return "aspect-square max-h-[150px]"
}

/**
 * Computes the visibility icon type based on visibility setting
 */
export function getVisibilityType(visibility: string): "members" | "subscribers" | "public" {
    if (visibility === "members") return "members"
    if (visibility === "subscribers") return "subscribers"
    return "public"
}

/**
 * Formats a date string as a relative time (e.g., "2 hours ago")
 */
export function formatTimeAgo(
    createdAt: unknown,
    locale: string,
    t: (key: string) => string
): string {
    if (typeof createdAt !== "string" || createdAt.trim().length === 0) {
        return t("activity.justNow")
    }

    const date = new Date(createdAt)
    if (Number.isNaN(date.getTime())) {
        return t("activity.recently")
    }

    try {
        const dateLocale = getDateFnsLocale(locale)
        return formatDistanceToNow(date, { addSuffix: true, locale: dateLocale })
    } catch {
        return t("activity.justNow")
    }
}
