import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

export interface AdminExtendedStats {
  totalUsers: number
  activeUsers: number
  totalProjects: number
  activeProjects: number
  completedProjects: number
  showcaseProjects: number
  totalTeams: number
  pendingModeration: number
  openSupportTickets: number
  inProgressTickets: number
  resolvedTickets: number
  openFeedback: number
  plannedFeedback: number
  completedFeedback: number
  openIssues: number
  investigatingIssues: number
  resolvedIssues: number
}

export const useAdminExtendedStats = () => {
  return useQuery({
    queryKey: ["admin", "stats", "extended"],
    queryFn: async () => {
      const { data } = await api.get<AdminExtendedStats>("/admin/stats/extended")
      return data
    },
  })
}

export interface AdminIssueSummary {
  id: string
  type: string
  title: string
  status: string
  priority: string
  projectId: string
  projectTitle: string
  reporterId: string
  reporterEmail: string
  assignedToAdminId?: string
  relatedUserId?: string
  createdAt: string
  resolvedAt?: string
  adminResolution?: string
}

export interface AdminIssueListResponse {
  total: number
  page: number
  pageSize: number
  data: AdminIssueSummary[]
}

export interface AdminIssueDetail {
  id: string
  type: string
  title: string
  description: string
  status: string
  priority: string
  project: { projectId: string; title: string }
  reporter: { reporterId: string; email: string; fullName: string }
  relatedUser?: { relatedUserId: string; email: string; fullName: string }
  assignedToAdmin?: { assignedToAdminId: string; email: string; fullName: string }
  adminResolution?: string
  resolvedByAdmin?: { resolvedByAdminId: string; email: string; fullName: string }
  createdAt: string
  updatedAt?: string
  resolvedAt?: string
  escalatedAt?: string
}

export interface ResolveIssueRequest {
  issueId: string
  resolution: string
  actionTaken: "warn" | "suspend" | "remove" | "dismiss"
}

export const useAdminIssues = (status?: string, type?: string, page = 1, pageSize = 20) => {
  return useQuery({
    queryKey: ["admin", "issues", status, type, page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      if (status) params.append("status", status)
      if (type) params.append("type", type)
      const { data } = await api.get<AdminIssueListResponse>(`/admin/issues?${params.toString()}`)
      return data
    },
  })
}

export const useAdminIssueDetails = (issueId: string) => {
  return useQuery({
    queryKey: ["admin", "issues", issueId],
    queryFn: async () => {
      const { data } = await api.get<AdminIssueDetail>(`/admin/issues/${issueId}`)
      return data
    },
    enabled: !!issueId,
  })
}

export const useAssignIssue = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ issueId, adminUserId }: { issueId: string; adminUserId: string }) => {
      await api.post(`/admin/issues/${issueId}/assign`, adminUserId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "issues"] })
    },
  })
}

export const useResolveIssue = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: ResolveIssueRequest) => {
      await api.post(`/admin/issues/${req.issueId}/resolve`, req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "issues"] })
      queryClient.invalidateQueries({ queryKey: ["admin", "stats"] })
    },
  })
}

export const useEscalateIssue = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (issueId: string) => {
      await api.put(`/admin/issues/${issueId}/escalate`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "issues"] })
    },
  })
}
