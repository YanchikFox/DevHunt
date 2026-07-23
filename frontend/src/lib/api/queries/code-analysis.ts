import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

/**
 * Severity breakdown from the code analyzer
 */
export interface SeverityCounts {
  critical?: number
  high?: number
  medium?: number
  low?: number
  info?: number
}

/**
 * A single code analysis issue
 */
export interface AnalysisIssue {
  rule_id: string
  rule_name?: string
  severity: string
  category: string
  message: string
  file: string
  file_path?: string
  line: number
  column?: number
  end_line?: number | null
  suggestion?: string
  cwe_id?: string | null
  snippet?: string | null
}

/**
 * Code analysis result from the DevHunt Analyzer
 */
export interface CodeAnalysisResult {
  id: string
  projectId: string
  integrationId?: string
  repository: string
  branch?: string
  commitSha?: string
  totalIssues: number
  totalFiles: number
  analysisTimeMs: number
  severityCounts?: SeverityCounts
  categoryCounts?: Record<string, number>
  issues?: AnalysisIssue[]
  status: string
  errorMessage?: string
  createdAt: string
}

/**
 * Hook to fetch the latest code analysis result for a project
 */
export function useLatestCodeAnalysis(projectId?: string) {
  return useQuery<CodeAnalysisResult>({
    queryKey: ["code-analysis", "latest", projectId],
    queryFn: async () => {
      const response = await apiClient.get<CodeAnalysisResult>(
        `/projects/${projectId}/code-analysis/latest`,
      )
      return response.data
    },
    enabled: !!projectId,
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: false,
  })
}

/**
 * Hook to fetch code analysis history for a project
 */
export function useCodeAnalysisHistory(projectId?: string, limit = 20) {
  return useQuery<CodeAnalysisResult[]>({
    queryKey: ["code-analysis", "history", projectId, limit],
    queryFn: async () => {
      const response = await apiClient.get<CodeAnalysisResult[]>(
        `/projects/${projectId}/code-analysis/history`,
        { params: { limit } },
      )
      return response.data
    },
    enabled: !!projectId,
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}

// ── Analysis Config (exclude patterns, dismissed issues) ──

export interface DismissedIssue {
  ruleId: string
  file?: string
  line?: number
  reason?: string
  dismissedBy?: string
  dismissedAt: string
}

export interface AnalysisConfig {
  excludePatterns: string[]
  dismissedIssues: DismissedIssue[]
}

export function useAnalysisConfig(projectId?: string) {
  return useQuery<AnalysisConfig>({
    queryKey: ["code-analysis", "config", projectId],
    queryFn: async () => {
      const response = await apiClient.get<AnalysisConfig>(
        `/projects/${projectId}/code-analysis/config`,
      )
      return response.data
    },
    enabled: !!projectId,
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}

function useCodeAnalysisAction(projectId: string | undefined, action: "dismiss" | "undismiss") {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { ruleId: string; file?: string; line?: number; reason?: string }) => {
      const response = await apiClient.post(`/projects/${projectId}/code-analysis/${action}`, req)
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["code-analysis", "config", projectId] })
    },
  })
}

export function useDismissIssue(projectId?: string) {
  return useCodeAnalysisAction(projectId, "dismiss")
}

export function useUndismissIssue(projectId?: string) {
  return useCodeAnalysisAction(projectId, "undismiss")
}

export function useUpdateExcludePatterns(projectId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (excludePatterns: string[]) => {
      const response = await apiClient.patch(
        `/projects/${projectId}/code-analysis/config/exclude-patterns`,
        { excludePatterns },
      )
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["code-analysis", "config", projectId] })
    },
  })
}
