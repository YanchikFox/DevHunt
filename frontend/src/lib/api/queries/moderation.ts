import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

// ── Types ──────────────────────────────────────────────────

export interface ModerationReport {
  id: string
  targetType: string
  targetId: string
  status: string
  reason: string
  actionTaken?: string
  createdAt: string
  processedAt?: string
  reporterId: string
  reporterName?: string
}

export const REPORT_TARGET_TYPES = {
  USER: "user",
  PROJECT: "project",
  MESSAGE: "message",
  NEWS_POST: "news_post",
  NEWS_COMMENT: "news_comment",
  SHOWCASE_COMMENT: "showcase_comment",
  COMMUNITY_POST: "community_post",
  TASK: "task",
  IMAGE: "image",
} as const

export interface SubmitReportRequest {
  targetType: string
  targetId: string
  reason: string
}

export interface ModerationDecisionRequest {
  reportId: string
  actionTaken?: string
  decision: "approve" | "decline" | "ban"
}

// ── Queries ────────────────────────────────────────────────

export const useModerationQueue = () => {
  return useQuery({
    queryKey: ["moderation", "queue"],
    queryFn: async () => {
      const { data } = await api.get<ModerationReport[]>("/moderation/queue")
      return data
    },
  })
}

// ── Mutations ──────────────────────────────────────────────

export const useSubmitReport = () => {
  return useMutation({
    mutationFn: async (req: SubmitReportRequest) => {
      const { data } = await api.post<ModerationReport>("/moderation/report", req)
      return data
    },
  })
}

export const useModerationDecision = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: ModerationDecisionRequest) => {
      const { data } = await api.post<ModerationReport>("/moderation/decision", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["moderation", "queue"] })
      queryClient.invalidateQueries({ queryKey: ["admin", "stats"] })
    },
  })
}
