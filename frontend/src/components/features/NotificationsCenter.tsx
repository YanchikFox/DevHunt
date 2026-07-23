"use client"

import { useState, useCallback, useMemo } from "react"
import { Button } from "@/components/ui/button"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import {
  useNotifications,
  useMarkNotificationAsRead,
  useMarkAllNotificationsAsRead,
} from "@/lib/api/queries/notifications"
import { Bell, Check } from "lucide-react"
import { Skeleton } from "@/components/ui/skeleton"
import type { Notification } from "@/lib/api/schema"
import { useTranslations, useLocale } from "next-intl"

import {
  NotificationItem,
  extractNotificationList,
  countUnread,
  TAB_KEYS,
  getTabLabel,
} from "./notificationHelpers"

function NotificationSkeleton() {
  return (
    <div className="flex flex-col">
      {[1, 2, 3, 4, 5].map((i) => (
        <div
          key={i}
          className="flex items-start gap-[14px] px-[18px] py-4 border-b border-border last:border-b-0"
        >
          <Skeleton className="h-[34px] w-[34px] rounded-[8px]" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-3.5 w-3/4" />
            <Skeleton className="h-3 w-28" />
          </div>
        </div>
      ))}
    </div>
  )
}

function NotificationEmptyState({ t }: { readonly t: (key: string) => string }) {
  return (
    <div className="flex flex-col items-center justify-center px-6 py-16 text-center">
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-[14px] border border-dashed border-border bg-bg-subtle">
        <Bell className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <h3 className="mb-1.5 text-[14px] font-semibold text-foreground">{t("noNotifications")}</h3>
      <p className="mx-auto max-w-xs text-[12px] leading-relaxed text-muted-foreground">
        {t("noNotificationsDesc")}
      </p>
    </div>
  )
}

export function NotificationsCenter() {
  const [activeTab, setActiveTab] = useState<string>("all")
  const { data: allNotifications, isLoading: allLoading } = useNotifications(false)
  const { data: unreadNotifications, isLoading: unreadLoading } = useNotifications(true)
  const markAsRead = useMarkNotificationAsRead()
  const markAllAsRead = useMarkAllNotificationsAsRead()
  const t = useTranslations("notifications")
  const tCommon = useTranslations("common")
  const locale = useLocale()

  const isLoading = activeTab === "all" ? allLoading : unreadLoading
  const rawData = activeTab === "all" ? allNotifications : unreadNotifications

  const notifications = useMemo(() => extractNotificationList(rawData), [rawData])
  const unreadCount = useMemo(() => countUnread(allNotifications), [allNotifications])

  const filteredNotifications = useMemo(() => {
    if (activeTab === "all") return notifications
    if (activeTab === "unread") return notifications.filter((n) => !n.read)
    return notifications.filter((n) => n.type === activeTab)
  }, [notifications, activeTab])

  const handleMarkAsRead = useCallback(
    async (id: string) => { await markAsRead.mutateAsync(id) },
    [markAsRead]
  )

  const handleMarkAllAsRead = useCallback(
    async () => { await markAllAsRead.mutateAsync() },
    [markAllAsRead]
  )

  return (
    <Tabs value={activeTab} onValueChange={setActiveTab} className="flex flex-col gap-[10px]">
      <div className="flex items-end justify-between gap-4 border-b border-border">
        <TabsList className="h-auto w-auto justify-start gap-1 rounded-none border-0 bg-transparent p-0">
          {TAB_KEYS.map(tab => (
            <TabsTrigger
              key={tab}
              value={tab}
              className="gap-2 rounded-none border-b-2 border-transparent px-[14px] py-2 text-[13px] font-medium capitalize text-muted-foreground shadow-none transition-colors data-[state=active]:border-primary data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none"
            >
              {t(getTabLabel(tab))}
              {(tab === "all" || tab === "unread") && unreadCount > 0 && (
                <span className="rounded bg-primary/10 px-1.5 py-0.5 font-mono text-[9px] text-primary">
                  {unreadCount}
                </span>
              )}
            </TabsTrigger>
          ))}
        </TabsList>
      </div>

      <div className="flex justify-end">
        <Button
          variant="outline"
          size="sm"
          onClick={handleMarkAllAsRead}
          disabled={unreadCount === 0 || markAllAsRead.isPending}
          className="h-8 gap-1.5 rounded-[8px] text-[12px]"
        >
          <Check className="h-3 w-3" />
          {markAllAsRead.isPending ? tCommon("processing") : t("markAsRead")}
        </Button>
      </div>

      <TabsContent value={activeTab} className="m-0">
        <div className="overflow-hidden rounded-[14px] border border-border bg-card">
          {isLoading ? (
            <NotificationSkeleton />
          ) : filteredNotifications.length > 0 ? (
            <div className="flex flex-col">
              {filteredNotifications.map((notif: Notification) => (
                <NotificationItem
                  key={notif.id}
                  notif={notif}
                  onMarkAsRead={handleMarkAsRead}
                  locale={locale}
                />
              ))}
            </div>
          ) : (
            <NotificationEmptyState t={t} />
          )}
        </div>
      </TabsContent>
    </Tabs>
  )
}
