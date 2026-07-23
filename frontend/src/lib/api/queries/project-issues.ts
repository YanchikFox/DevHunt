import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

// ── Types ──────────────────────────────────────────────────

export const ISSUE_TYPES = [
  "technical",
  "legal",
  "conflict",
  "abuse",
  "violation",
  "security",
  "other",
] as const

export type IssueType = (typeof ISSUE_TYPES)[number]

export const ISSUE_STATUSES = [
  "open",
  "investigating",
  "resolved",
  "dismissed",
  "escalated",
] as const

export type IssueStatus = (typeof ISSUE_STATUSES)[number]

export interface ProjectIssue {
  id: string
  projectId: string
  title: string
  description: string
  type: string
  status: string
  priority: string
  createdAt: string
  updatedAt?: string
  reporterId: string
  reporterName?: string
  relatedUserId?: string
  relatedUserName?: string
  assignedToAdminId?: string
  resolution?: string
}

export interface CreateProjectIssueRequest {
  type: string
  title: string
  description: string
  relatedUserId?: string
}

// ── Queries ────────────────────────────────────────────────

export const useProjectIssues = (projectId: string, enabled = true) => {
  return useQuery({
    queryKey: ["project-issues", projectId],
    queryFn: async () => {
      const { data } = await api.get<ProjectIssue[]>(
        `/projects/${projectId}/issues`
      )
      return data
    },
    enabled: enabled && Boolean(projectId),
  })
}

// ── Mutations ──────────────────────────────────────────────

export const useCreateProjectIssue = (projectId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: CreateProjectIssueRequest) => {
      const { data } = await api.post<ProjectIssue>(
        `/projects/${projectId}/issues`,
        req
      )
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["project-issues", projectId],
      })
    },
  })
}

export const useCancelProjectIssue = (projectId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ issueId, reason }: { issueId: string; reason?: string }) => {
      await api.post(`/projects/${projectId}/issues/${issueId}/cancel`, {
        reason,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["project-issues", projectId],
      })
    },
  })
}
