import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

// ── Types ──────────────────────────────────────────────────

export interface TicketSummary {
  id: string
  category: string
  subject: string
  status: string
  priority: string
  userId?: string
  userEmail?: string
  assignedToUserId?: string
  assignedToName?: string
  createdAt: string
  updatedAt?: string
  resolvedAt?: string
  messageCount: number
}

export interface TicketListResponse {
  total: number
  page: number
  pageSize: number
  data: TicketSummary[]
}

export interface TicketMessage {
  id: string
  authorId: string
  authorName: string
  content: string
  isInternal: boolean
  createdAt: string
  editedAt?: string
}

export interface TicketDetail {
  id: string
  category: string
  subject: string
  description: string
  status: string
  priority: string
  assignedToUserId?: string
  relatedProjectId?: string
  relatedUserId?: string
  createdAt: string
  updatedAt?: string
  resolvedAt?: string
  messages: TicketMessage[]
}

export interface TicketHistoryEntry {
  id: string
  changeType: string
  oldValue?: string
  newValue?: string
  reason?: string
  changedByUserId: string
  changedByUserEmail: string
  createdAt: string
}

export interface TicketHistoryResponse {
  ticketId: string
  history: TicketHistoryEntry[]
}

export interface CreateTicketRequest {
  category: string
  subject: string
  description: string
  relatedProjectId?: string
  relatedUserId?: string
}

export interface AddMessageRequest {
  content: string
  isInternal?: boolean
}

export interface AdminTicketFilters {
  status?: string
  priority?: string
  category?: string
  assignedToUserId?: string
  search?: string
  page?: number
  pageSize?: number
}

export interface SupportStats {
  totalTickets: number
  openTickets: number
  inProgressTickets: number
  waitingUserTickets: number
  resolvedTickets: number
  closedTickets: number
  byPriority: Record<string, number>
  byCategory: Record<string, number>
  unassignedTickets: number
  avgResponseTime: number
}

// ── User queries ───────────────────────────────────────────

export const useMyTickets = (status?: string, category?: string, page = 1, pageSize = 20) => {
  return useQuery({
    queryKey: ["support", "my-tickets", status, category, page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      if (status) params.append("status", status)
      if (category) params.append("category", category)
      const { data } = await api.get<TicketListResponse>(`/support/tickets?${params.toString()}`)
      return data
    },
  })
}

export const useTicketDetails = (ticketId: string) => {
  return useQuery({
    queryKey: ["support", "ticket", ticketId],
    queryFn: async () => {
      const { data } = await api.get<TicketDetail>(`/support/tickets/${ticketId}`)
      return data
    },
    enabled: !!ticketId,
  })
}

export const useTicketHistory = (ticketId: string) => {
  return useQuery({
    queryKey: ["support", "ticket", ticketId, "history"],
    queryFn: async () => {
      const { data } = await api.get<TicketHistoryResponse>(`/support/tickets/${ticketId}/history`)
      return data
    },
    enabled: !!ticketId,
  })
}

export const useCreateTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: CreateTicketRequest) => {
      const { data } = await api.post<TicketSummary>("/support/tickets", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["support", "my-tickets"] })
    },
  })
}

export const useAddTicketMessage = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, ...body }: AddMessageRequest & { ticketId: string }) => {
      const { data } = await api.post<TicketMessage>(`/support/tickets/${ticketId}/messages`, body)
      return data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["support", "ticket", variables.ticketId] })
      queryClient.invalidateQueries({ queryKey: ["support", "my-tickets"] })
      queryClient.invalidateQueries({ queryKey: ["admin", "support"] })
    },
  })
}

export const useCloseTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (ticketId: string) => {
      await api.put(`/support/tickets/${ticketId}/close`)
    },
    onSuccess: (_, ticketId) => {
      queryClient.invalidateQueries({ queryKey: ["support", "ticket", ticketId] })
      queryClient.invalidateQueries({ queryKey: ["support", "my-tickets"] })
    },
  })
}

export const useReopenTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, reason }: { ticketId: string; reason?: string }) => {
      await api.post(`/support/tickets/${ticketId}/reopen`, reason ? { reason } : null)
    },
    onSuccess: (_, { ticketId }) => {
      queryClient.invalidateQueries({ queryKey: ["support", "ticket", ticketId] })
      queryClient.invalidateQueries({ queryKey: ["support", "my-tickets"] })
    },
  })
}

// ── Admin queries ──────────────────────────────────────────

export const useAdminTickets = (filters: AdminTicketFilters = {}) => {
  return useQuery({
    queryKey: ["admin", "support", "tickets", filters],
    queryFn: async () => {
      const params = new URLSearchParams()
      params.append("page", (filters.page ?? 1).toString())
      params.append("pageSize", (filters.pageSize ?? 20).toString())
      if (filters.status) params.append("status", filters.status)
      if (filters.priority) params.append("priority", filters.priority)
      if (filters.category) params.append("category", filters.category)
      if (filters.assignedToUserId) params.append("assignedToUserId", filters.assignedToUserId)
      if (filters.search) params.append("search", filters.search)
      const { data } = await api.get<TicketListResponse>(`/admin/support/tickets?${params.toString()}`)
      return data
    },
  })
}

export const useAdminSupportStats = () => {
  return useQuery({
    queryKey: ["admin", "support", "stats"],
    queryFn: async () => {
      const { data } = await api.get<SupportStats>("/admin/support/stats")
      return data
    },
  })
}

export const useAssignTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, adminUserId }: { ticketId: string; adminUserId: string }) => {
      await api.post("/admin/support/tickets/assign", { ticketId, adminUserId })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "support"] })
    },
  })
}

export const useResolveTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, resolution }: { ticketId: string; resolution: string }) => {
      await api.post("/admin/support/tickets/resolve", { ticketId, resolution })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "support"] })
      queryClient.invalidateQueries({ queryKey: ["support"] })
    },
  })
}

export const useReassignTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, newAdminUserId, reason }: { ticketId: string; newAdminUserId: string; reason?: string }) => {
      await api.post(`/admin/support/tickets/${ticketId}/reassign`, { newAdminUserId, reason })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "support"] })
    },
  })
}

export const useUpdateTicketPriority = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, priority, reason }: { ticketId: string; priority: string; reason?: string }) => {
      await api.put(`/admin/support/tickets/${ticketId}/priority`, { priority, reason })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "support"] })
    },
  })
}

export const useEscalateTicket = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ ticketId, reason }: { ticketId: string; reason?: string }) => {
      await api.post(`/admin/support/tickets/${ticketId}/escalate`, reason ? { reason } : null)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "support"] })
    },
  })
}
