"use client"

import { useTranslations, useLocale } from "next-intl"
import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { UserActivityFeedItem } from "@/lib/api/queries/profile"
import {
  Activity,
  Camera,
  CheckSquare,
  FileText,
  FolderPlus,
  RefreshCw,
  Sparkles,
  UserCheck,
  UserMinus,
  UserPlus,
  UserX,
  Columns,
  Trash2,
  RotateCcw,
} from "lucide-react"
import type { LucideIcon } from "lucide-react"
import { cn } from "@/lib/utils"

type EventConfig = {
  icon: LucideIcon
  color: string
  bgColor: string
}

const eventConfigMap: Record<string, EventConfig> = {
  "user.follow": { icon: UserPlus, color: "text-primary", bgColor: "bg-primary/10" },
  "user.unfollow": { icon: UserMinus, color: "text-muted-foreground", bgColor: "bg-muted" },
  "user.created_project": { icon: FolderPlus, color: "text-emerald-500", bgColor: "bg-emerald-500/10" },
  "user.joined_project": { icon: UserPlus, color: "text-primary", bgColor: "bg-primary/10" },
  "user.left_project": { icon: UserX, color: "text-orange-500", bgColor: "bg-orange-500/10" },
  "user.updated_profile": { icon: UserCheck, color: "text-primary", bgColor: "bg-primary/10" },
  "project.news_published": { icon: FileText, color: "text-primary", bgColor: "bg-primary/10" },
  "project.media_uploaded": { icon: Camera, color: "text-primary", bgColor: "bg-primary/10" },
  "project.state_changed": { icon: RefreshCw, color: "text-emerald-500", bgColor: "bg-emerald-500/10" },
  "task.created": { icon: Sparkles, color: "text-emerald-500", bgColor: "bg-emerald-500/10" },
  "task.updated": { icon: RefreshCw, color: "text-primary", bgColor: "bg-primary/10" },
  "task.status_changed": { icon: CheckSquare, color: "text-amber-500", bgColor: "bg-amber-500/10" },
  "task.deleted": { icon: Trash2, color: "text-red-500", bgColor: "bg-red-500/10" },
  "task.restored": { icon: RotateCcw, color: "text-green-500", bgColor: "bg-green-500/10" },
  "task.attachment_added": { icon: FileText, color: "text-primary", bgColor: "bg-primary/10" },
  "column.created": { icon: Columns, color: "text-teal-500", bgColor: "bg-teal-500/10" },
  "column.updated": { icon: RefreshCw, color: "text-teal-500", bgColor: "bg-teal-500/10" },
  "column.deleted": { icon: Trash2, color: "text-red-500", bgColor: "bg-red-500/10" },
}

const defaultConfig: EventConfig = {
  icon: Activity,
  color: "text-muted-foreground",
  bgColor: "bg-muted",
}

function safeJsonParse(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "string" || value.trim().length === 0) return null
  try {
    return JSON.parse(value)
  } catch {
    return null
  }
}

function getStatusLabel(status: string, t: (key: string) => string): string {
  switch ((status || "").toLowerCase()) {
    case "todo": return t("tasks.todo")
    case "doing":
    case "in_progress": return t("tasks.inProgress")
    case "review": return t("tasks.review")
    case "done": return t("tasks.done")
    case "archived": return t("tasks.archived")
    case "cancelled": return t("tasks.cancelled")
    default: return status
  }
}

function buildCompactSummary(activity: UserActivityFeedItem, t: (key: string, values?: Record<string, string | number>) => string): string {
  const payload = safeJsonParse(activity.payloadJson)

  switch (activity.eventType) {
    case "user.joined_project":
      return t("activity.joinedProjectCompact")
    case "user.left_project":
      return t("activity.leftProjectCompact")
    case "project.news_published": {
      const title = payload?.title || payload?.Title
      return title ? `${t("activity.publishedNews")}: ${title}` : t("activity.publishedNews")
    }
    case "task.created": {
      const title = payload?.title || payload?.Title || activity.summary
      return `${t("activity.createdTask")}: «${title}»`
    }
    case "task.updated": {
      const title = payload?.title || payload?.Title || activity.summary
      return `${t("activity.updatedTask")}: «${title}»`
    }
    case "task.deleted": {
      const title = payload?.title || payload?.Title || activity.summary
      return `${t("activity.deletedTask")}: «${title}»`
    }
    case "task.restored": {
      const title = payload?.title || payload?.Title || activity.summary
      return `${t("activity.restoredTask")}: «${title}»`
    }
    case "task.status_changed": {
      const title = payload?.title || payload?.Title || activity.summary
      const fromVal = String(payload?.from || payload?.From || "")
      const toVal = String(payload?.to || payload?.To || "")
      const from = getStatusLabel(fromVal, t)
      const to = getStatusLabel(toVal, t)
      return `${title}: ${from} → ${to}`
    }
    case "task.attachment_added": {
      const title = payload?.taskTitle || payload?.title || activity.summary
      return `${t("activity.addedAttachment")}: ${title}`
    }
    case "column.created": {
      const name = payload?.name || payload?.Name || activity.summary
      return `${t("activity.createdColumn")}: ${name}`
    }
    case "column.updated": {
      const name = payload?.name || payload?.Name || activity.summary
      return `${t("activity.updatedColumn")}: ${name}`
    }
    case "column.deleted": {
      const name = payload?.name || payload?.Name || activity.summary
      return `${t("activity.deletedColumn")}: ${name}`
    }
    default:
      return activity.summary || activity.eventType
  }
}

export type ActivityItemCompactProps = {
  activity: UserActivityFeedItem
  showAvatar?: boolean
  showProject?: boolean
}

export function ActivityItemCompact({ activity, showAvatar = true, showProject = true }: ActivityItemCompactProps) {
  const t = useTranslations()
  const locale = useLocale()

  const actorName = activity.actorName ?? t("common.unknownUser")
  const avatarInitial = (actorName.trim().charAt(0) || "U").toUpperCase()
  const summary = buildCompactSummary(activity, t)

  const timeAgo = (() => {
    const raw = activity.createdAt
    if (typeof raw !== "string" || raw.trim().length === 0) return ""
    const date = new Date(raw)
    if (Number.isNaN(date.getTime())) return ""
    try {
      const dateLocale = getDateFnsLocale(locale)
      return formatDistanceToNow(date, { addSuffix: false, locale: dateLocale })
    } catch {
      return ""
    }
  })()

  const config = eventConfigMap[activity.eventType] || defaultConfig
  const Icon = config.icon

  return (
    <div className="group flex items-center gap-2 px-2 py-1.5 rounded-md hover:bg-muted/50 transition-colors cursor-pointer">
      {/* Icon */}
      <div className={cn("flex-shrink-0 w-6 h-6 rounded flex items-center justify-center", config.bgColor)}>
        <Icon className={cn("h-3.5 w-3.5", config.color)} />
      </div>

      {/* Avatar (optional) */}
      {showAvatar && (
        <Avatar className="h-5 w-5 flex-shrink-0">
          {activity.actorAvatarUrl ? (
            <AvatarImage src={activity.actorAvatarUrl} alt={actorName} />
          ) : (
            <AvatarFallback className="text-[10px] font-medium bg-muted">{avatarInitial}</AvatarFallback>
          )}
        </Avatar>
      )}

      {/* Content */}
      <div className="flex-1 min-w-0 flex items-center gap-2">
        <span className="text-sm font-medium text-foreground truncate max-w-[120px]">{actorName}</span>
        <span className="text-sm text-muted-foreground truncate flex-1">{summary}</span>
      </div>

      {/* Project badge (optional) */}
      {showProject && activity.projectTitle && (
        <span className="flex-shrink-0 text-xs text-muted-foreground bg-muted px-1.5 py-0.5 rounded truncate max-w-[100px]">
          {activity.projectTitle}
        </span>
      )}

      {/* Time */}
      <span className="flex-shrink-0 text-xs text-muted-foreground/60 w-16 text-right">{timeAgo}</span>
    </div>
  )
}
