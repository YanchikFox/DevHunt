import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

// Types

export interface AdminChatStats {
  total: number
  groups: number
  directs: number
  orphaned: number
  totalMessages: number
  activeConversations: number
}

export interface AdminConversation {
  id: string
  type: string
  title: string | null
  participantCount: number
  messageCount: number
  lastMessageAt: string | null
  createdAt: string
  isOrphaned: boolean
}

export interface AdminConversationMessage {
  id: string
  senderId: string
  senderName: string
  senderAvatarUrl: string | null
  content: string
  createdAt: string
  isEdited: boolean
  isDeleted: boolean
  isAiGenerated: boolean
}

interface PaginatedResponse<T> {
  data: T[]
  pagination: {
    page: number
    pageSize: number
    total: number
    totalPages: number
  }
}

interface ConversationFilters {
  type?: string
  search?: string
  orphaned?: boolean
}

// Queries

export const useAdminChatStats = () => {
  return useQuery({
    queryKey: ["admin", "chat", "stats"],
    queryFn: async () => {
      const { data } = await api.get<AdminChatStats>("/admin/chat/stats")
      return data
    },
  })
}

export const useAdminConversations = (page = 1, pageSize = 20, filters?: ConversationFilters) => {
  return useQuery({
    queryKey: ["admin", "chat", "conversations", page, pageSize, filters],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      })
      if (filters?.type && filters.type !== "all") params.append("type", filters.type)
      if (filters?.search) params.append("search", filters.search)
      if (filters?.orphaned) params.append("orphaned", "true")

      const { data } = await api.get<PaginatedResponse<AdminConversation>>(
        `/admin/chat/conversations?${params.toString()}`
      )
      return data
    },
  })
}

export const useAdminConversationMessages = (conversationId: string | null, page = 1, pageSize = 50) => {
  return useQuery({
    queryKey: ["admin", "chat", "messages", conversationId, page],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      })
      const { data } = await api.get<PaginatedResponse<AdminConversationMessage>>(
        `/admin/chat/conversations/${conversationId}/messages?${params.toString()}`
      )
      return data
    },
    enabled: Boolean(conversationId),
  })
}

// Mutations

export const useDeleteConversation = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (conversationId: string) => {
      await api.delete(`/admin/chat/conversations/${conversationId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "chat"] })
    },
  })
}

export const useDeleteChatMessage = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ conversationId, messageId }: { conversationId: string; messageId: string }) => {
      await api.delete(`/admin/chat/conversations/${conversationId}/messages/${messageId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "chat"] })
    },
  })
}

export const useCleanupOrphanedChats = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<{ deletedCount: number; deletedMessages: number }>(
        "/admin/chat/cleanup-orphaned"
      )
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "chat"] })
    },
  })
}
