"use client"

import { memo, useCallback } from "react"
import { Bell, GitMerge, UserPlus, MessageSquare, Award, AlertCircle, Calendar, Shield, Heart, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils"
import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import { Link } from "@/i18n/routing"
import type { Notification } from "@/lib/api/schema"

// ── Lookup maps ──────────────────────────────────────────────────

const ICON_MAP: Record<string, LucideIcon> = {
  invitation: UserPlus,
  team: UserPlus,
  taskassigned: Calendar,
  task: Calendar,
  chatmessage: MessageSquare,
  message: MessageSquare,
  projectstatuschanged: GitMerge,
  project: GitMerge,
  applicationaccepted: AlertCircle,
  applicationrejected: AlertCircle,
  achievement: Award,
  follow: Heart,
  moderation: Shield,
  admin: Shield,
  general: Bell,
}

const COLOR_MAP: Record<string, string> = {
  invitation: "text-primary",
  team: "text-primary",
  taskassigned: "text-primary",
  task: "text-primary",
  chatmessage: "text-primary",
  message: "text-primary",
  projectstatuschanged: "text-primary",
  project: "text-primary",
  applicationaccepted: "text-success",
  applicationrejected: "text-destructive",
  achievement: "text-warning",
  follow: "text-primary",
  moderation: "text-warning",
  admin: "text-warning",
  general: "text-muted-foreground",
}

const URL_ENTITY_MAP: Record<string, (id: string) => string> = {
  project: (id) => `/dashboard/projects/${id}`,
  invitation: () => "/dashboard/teams",
  message: () => "/dashboard/messages",
  conversation: () => "/dashboard/messages",
  user: (id) => `/profile/${id}`,
  achievement: () => "/dashboard/profile",
  moderationreport: () => "/admin",
}

// ── Public helper functions ──────────────────────────────────────

export const getNotificationIcon = (type: string): LucideIcon =>
  ICON_MAP[type.toLowerCase()] ?? Bell

export const getNotificationColor = (type: string): string =>
  COLOR_MAP[type.toLowerCase()] ?? "text-muted-foreground"

export const getNotificationUrl = (notification: Notification): string | undefined => {
  const entityType = notification.metadata?.relatedEntityType
  const entityId = notification.metadata?.relatedEntityId
  if (!entityType || !entityId || typeof entityType !== "string") return undefined
  return URL_ENTITY_MAP[entityType.toLowerCase()]?.(String(entityId))
}

export function hasDataProperty(value: unknown): value is { data: unknown } {
  return value != null && typeof value === "object" && "data" in value
}

export function extractNotificationList(data: unknown): Notification[] {
  if (hasDataProperty(data) && Array.isArray(data.data)) return data.data
  if (Array.isArray(data)) return data
  return []
}

export function countUnread(allData: unknown): number {
  const list = extractNotificationList(allData)
  return list.filter((n: Notification) => !n.read).length
}

export const TAB_KEYS = ["all", "unread", "project", "team", "message"] as const

export function getTabLabel(tab: string): string {
  if (tab === "unread") return "unreadTab"
  if (tab === "all") return "all"
  return `${tab}Tab`
}

// ── NotificationItem (memoised) ──────────────────────────────────

export const NotificationItem = memo(function NotificationItem({
  notif,
  onMarkAsRead,
  locale,
}: {
  notif: Notification
  onMarkAsRead: (id: string) => void
  locale: string
}) {
  const Icon = getNotificationIcon(notif.type)
  const color = getNotificationColor(notif.type)
  const url = getNotificationUrl(notif)
  const dateLocale = getDateFnsLocale(locale)

  const markUnread = useCallback(() => {
    if (!notif.read) onMarkAsRead(notif.id)
  }, [notif.read, notif.id, onMarkAsRead])

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault()
      markUnread()
    }
  }, [markUnread])

  const content = (
    <div
      className={cn(
        "group flex cursor-pointer items-start gap-[14px] border-b border-border px-[18px] py-4 transition-colors last:border-b-0 hover:bg-bg-hover",
        !notif.read ? "bg-bg-subtle" : "bg-transparent"
      )}
      onClick={markUnread}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
    >
      <div
        className={cn(
          "flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-[8px] bg-primary/10",
          color
        )}
      >
        <Icon className="h-[15px] w-[15px]" />
      </div>

      <div className="min-w-0 flex-1">
        <p
          className={cn(
            "text-[13px] leading-tight text-foreground",
            !notif.read ? "font-medium" : "font-normal"
          )}
        >
          {notif.title}
        </p>
        {notif.message && (
          <p className="mt-1 line-clamp-2 text-[12px] leading-snug text-muted-foreground">
            {notif.message}
          </p>
        )}
        <p className="mt-1 font-mono text-[10px] text-muted-foreground">
          {formatDistanceToNow(new Date(notif.createdAt), { addSuffix: false, locale: dateLocale })}
          {" · "}
          {notif.type}
        </p>
      </div>

      {!notif.read && (
        <span className="mt-[10px] h-2 w-2 shrink-0 rounded-full bg-primary" />
      )}
    </div>
  )

  if (url) {
    return <Link href={url} className="block">{content}</Link>
  }

  return <div>{content}</div>
})
