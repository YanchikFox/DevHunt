"use client"

import { useEffect, useState, useCallback } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { createNotificationClient } from "@/lib/realtime/client"
import { useAuth } from "@/lib/api/queries/auth"
import { useNotifications, useMarkNotificationAsRead } from "@/lib/api/queries/notifications"
import { fetchRealtimeAccessToken } from "@/lib/auth/realtime-token"
import type { Notification } from "@/lib/api/schema"

/**
 * Hook to manage real-time notifications.
 * Connects to the notification service via SignalR (or mock client) and merges
 * real-time notifications with the initial list fetched from the API.
 *
 * @returns An object containing the list of notifications, unread count, and a function to mark as read.
 */
export function useRealtimeNotifications() {
  const { data: user } = useAuth()
  const { data: notifications } = useNotifications()
  const markAsRead = useMarkNotificationAsRead()
  const queryClient = useQueryClient() // P0-1: For cache invalidation on SignalR events
  const [newNotifications, setNewNotifications] = useState<Notification[]>([])
  const [isClient, setIsClient] = useState(false)

  // Ensure we only access browser APIs on the client
  useEffect(() => {
    setIsClient(true)
  }, [])

  useEffect(() => {
    // Only run on client side and when user is available
    if (!isClient || !user?.id) return undefined

    const notificationClient = createNotificationClient()

    notificationClient.onMessage((notification) => {
      setNewNotifications((prev) => [notification, ...prev])
      // P0-1: Invalidate TanStack Query cache — replaces polling with event-driven refresh
      queryClient.invalidateQueries({ queryKey: ["notifications"] })
    })

    notificationClient.onError((error) => {
      // P1-9: Guard console.debug — only log in development
      if (process.env.NODE_ENV === "development") {
        console.error("[SignalR] Notification client error:", error?.message)
      }
    })

    void (async () => {
      const token = await fetchRealtimeAccessToken()
      if (!token) return

      await notificationClient.connect(user.id, token).catch((err: unknown) => {
        // P1-9: console.error in catch is allowed per F-06 rules
        console.error("[SignalR] Initial connection failed:", (err as Error)?.message)
      })
    })()

    return () => {
      notificationClient.disconnect()
    }
  }, [user?.id, isClient, queryClient])

  // P1-10: Wrap in useCallback to prevent re-creation on every render
  const markNotificationAsRead = useCallback(
    (id: string) => {
      markAsRead.mutate(id)
      setNewNotifications((prev) => prev.filter((n) => n.id !== id))
    },
    [markAsRead]
  )

  // Only combine notifications on client to prevent hydration mismatch
  const notificationsArray: Notification[] =
    notifications && "data" in notifications && Array.isArray(notifications.data)
      ? notifications.data
      : Array.isArray(notifications)
        ? notifications
        : []
  const allNotifications: Notification[] = isClient
    ? [...(newNotifications || []), ...notificationsArray]
    : notificationsArray

  return {
    notifications: allNotifications,
    unreadCount: Array.isArray(allNotifications)
      ? allNotifications.filter((n: Notification) => !n.read).length
      : 0,
    markAsRead: markNotificationAsRead,
  }
}
