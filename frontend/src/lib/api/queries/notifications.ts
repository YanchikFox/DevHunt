import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockNotificationsApi } from "../adapters/mock"
import { apiClient } from "../client"
import { mapNotification } from "../adapters/backend-mappers"

export function useNotifications(unreadOnly?: boolean, page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ["notifications", unreadOnly, page, pageSize],
    queryFn: async () => {
      if (USE_MOCKS) {
        const data = await mockNotificationsApi.list("")
        return { data, pagination: null }
      }
      // Backend endpoint: GET /api/notifications (userId from JWT token)
      // Returns paginated response with Data and Pagination
      // Note: axios interceptor converts all keys to camelCase before we receive the response,
      // so backend's PascalCase (Data, Pagination, IsRead) arrives as camelCase (data, pagination, isRead).
      type BackendNotificationDto = {
        id: string
        type: string
        title: string
        content: string | null
        relatedEntityType: string | null
        relatedEntityId: string | null
        priority: string | null
        createdAt: string
        isRead: boolean
        readAt: string | null
      }
      type BackendResponse = {
        data: BackendNotificationDto[]
        pagination: {
          page: number
          pageSize: number
          total: number
          totalPages: number
          hasNext: boolean
          hasPrevious: boolean
        }
      }
      const response = await apiClient.get<BackendResponse>("/notifications", {
        params: {
          ...(unreadOnly ? { unreadOnly: true } : {}),
          page,
          pageSize,
        },
      })

      const notifications = (response.data.data ?? []).map((dto) =>
        mapNotification(dto as Record<string, unknown>, dto.id)
      )
      return {
        data: notifications,
        pagination: response.data.pagination,
      }
    },
    refetchInterval: false, // P0-1: Removed 30s polling — notifications arrive via SignalR
    staleTime: 5 * 60 * 1000, // 5 minutes — SignalR invalidates cache on new notifications
  })
}

export function useMarkNotificationAsRead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (id: string) => {
      if (USE_MOCKS) {
        await mockNotificationsApi.markAsRead(id)
        return
      }
      // Backend endpoint: POST /api/notifications/mark-read/{id}
      await apiClient.post(`/notifications/mark-read/${id}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] })
    },
  })
}

export function useMarkAllNotificationsAsRead() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      if (USE_MOCKS) {
        return
      }
      await apiClient.post("/notifications/mark-all-read")
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] })
    },
  })
}

export function useUnreadCount() {
  return useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: async () => {
      if (USE_MOCKS) return { count: 0 }
      const response = await apiClient.get<{ count: number }>("/notifications/unread-count")
      return response.data
    },
    staleTime: 60 * 1000,
    refetchInterval: false,
  })
}

export function useDeleteNotification() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/notifications/${id}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] })
    },
  })
}

export function useDeleteAllReadNotifications() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      await apiClient.delete("/notifications")
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] })
    },
  })
}
