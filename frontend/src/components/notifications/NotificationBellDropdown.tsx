"use client"

import { useCallback } from "react"
import { Bell, CheckCheck } from "lucide-react"
import { useLocale, useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  useNotifications,
  useMarkNotificationAsRead,
  useMarkAllNotificationsAsRead,
} from "@/lib/api/queries/notifications"
import { Link } from "@/i18n/routing"
import { cn } from "@/lib/utils"
import type { Notification } from "@/lib/api/schema"

export function NotificationBellDropdown() {
  const t = useTranslations("notificationBell")
  const locale = useLocale()
  const { data } = useNotifications(true, 1, 10)
  const markRead = useMarkNotificationAsRead()
  const markAll = useMarkAllNotificationsAsRead()

  const items = Array.isArray(data?.data) ? data.data : []
  const unreadCount = items.length

  const handleMarkAll = useCallback(() => markAll.mutate(), [markAll])
  const handleMarkOne = useCallback((id: string) => markRead.mutate(id), [markRead])

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          aria-label={t("title")}
          aria-haspopup="true"
          className="relative h-8 w-8 rounded-[8px] text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:ring-offset-2"
        >
          <Bell className="h-4 w-4" />
          {unreadCount > 0 && (
            <span className="absolute right-[6px] top-[6px] h-1.5 w-1.5 rounded-full bg-primary" />
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-[360px] max-w-[calc(100vw-2rem)] overflow-hidden rounded-[14px] border-border bg-bg-elevated p-0 shadow-lg" sideOffset={8}>
        <div className="flex items-center justify-between border-b border-border px-4 py-3">
          <span className="caption">[{t("title")}]</span>
          {unreadCount > 0 && (
            <Button variant="ghost" size="sm" className="h-7 gap-1 rounded-[8px] text-[11px]" onClick={handleMarkAll}>
              <CheckCheck className="h-3.5 w-3.5" />
              {t("markAllRead")}
            </Button>
          )}
        </div>
        <NotificationList items={items} onMarkRead={handleMarkOne} emptyLabel={t("noUnread")} locale={locale} />
        <div className="border-t border-border px-4 py-2.5">
          <Link href="/dashboard/notifications" className="text-[12px] text-primary hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:rounded-sm">
            {t("viewAll")}
          </Link>
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function NotificationList({
  items,
  onMarkRead,
  emptyLabel,
  locale,
}: {
  items: Notification[]
  onMarkRead: (id: string) => void
  emptyLabel: string
  locale: string
}) {
  if (items.length === 0) {
    return (
      <div className="px-4 py-10 text-center text-[13px] text-muted-foreground">
        {emptyLabel}
      </div>
    )
  }

  return (
    <div className="max-h-[min(320px,44vh)] overflow-y-auto p-2">
      {items.map((n) => (
        <NotificationRow key={n.id} item={n} onMarkRead={onMarkRead} locale={locale} />
      ))}
    </div>
  )
}

function NotificationRow({
  item,
  onMarkRead,
  locale,
}: {
  item: Notification
  onMarkRead: (id: string) => void
  locale: string
}) {
  const t = useTranslations("notificationBell")
  const handleClick = useCallback(() => onMarkRead(item.id), [item.id, onMarkRead])
  const createdAt = new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(item.createdAt))

  return (
    <div className={cn("flex gap-3 rounded-[10px] px-3 py-2.5 transition-colors hover:bg-bg-hover", !item.read && "bg-primary/5")}>
      <div className="mt-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-[8px] border border-border bg-bg-subtle text-primary">
        <Bell className="h-3.5 w-3.5" />
      </div>
      <div className="min-w-0 flex-1">
        <p className="truncate text-[13px] font-semibold text-foreground">{item.title}</p>
        {item.message && item.message !== item.title && (
          <p className="mt-0.5 truncate text-[12px] text-muted-foreground">{item.message}</p>
        )}
        <time dateTime={item.createdAt} className="mt-1 block font-mono text-[10px] text-muted-foreground/70">
          {createdAt}
        </time>
      </div>
      {!item.read && (
        <button
          type="button"
          onClick={handleClick}
          aria-label={t("markAsRead")}
          className="mt-1 -m-2 flex-shrink-0 rounded-full p-2 transition-colors hover:bg-bg-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50"
        >
          <span className="block h-2 w-2 rounded-full bg-primary" aria-hidden="true" />
        </button>
      )}
    </div>
  )
}
