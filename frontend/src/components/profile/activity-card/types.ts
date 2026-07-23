import type { LucideIcon } from "lucide-react"

/**
 * Payload types for different activity events
 */
export interface ActivityPayload {
    title?: string
    Title?: string
    content?: string
    Content?: string
    images?: string[]
    Images?: string[]
    isPinned?: boolean
    IsPinned?: boolean
    visibility?: string
    Visibility?: string
    from?: string
    From?: string
    to?: string
    To?: string
    [key: string]: unknown
}

/**
 * Visual configuration for an event type
 */
export type EventVisual = {
    icon: LucideIcon
    label: string
    accentClass: string
    backgroundClass: string
}

/**
 * News content structure
 */
export interface NewsContent {
    newsPostId?: string
    projectId?: string
    title?: string
    content?: string
    images: string[]
    isPinned: boolean
    visibility: string
    likesCount?: number
    commentsCount?: number
    isLikedByCurrentUser?: boolean
}
