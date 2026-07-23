import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

export interface ArtifactDto {
  id: string
  type: string
  title: string | null
  content: string
  version: number
  generatedAt: string
}

export interface ArtifactsResponse {
  artifacts: ArtifactDto[]
}

export interface GeneratePassportResponse {
  artifacts: ArtifactDto[]
  provider: string | null
  model: string | null
}

/**
 * Fetch all passport artifacts for a project.
 */
export function useProjectArtifacts(projectId?: string) {
  return useQuery({
    queryKey: ["project-artifacts", projectId],
    queryFn: async (): Promise<ArtifactDto[]> => {
      const response = await apiClient.get<ArtifactsResponse>(
        `/projects/${projectId}/artifacts`
      )
      return response.data.artifacts
    },
    enabled: Boolean(projectId),
  })
}

/**
 * Generate (or regenerate) the full project passport via AI.
 */
export function useGeneratePassport(projectId?: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (): Promise<GeneratePassportResponse> => {
      if (!projectId) throw new Error("ProjectId is required")
      const response = await apiClient.post<GeneratePassportResponse>(
        `/projects/${projectId}/artifacts/generate`
      )
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["project-artifacts", projectId] })
    },
  })
}

/**
 * Artifact section type constants and display order.
 */
export const ARTIFACT_TYPES = [
  { type: "overview", icon: "FileText" },
  { type: "tech_stack", icon: "Cpu" },
  { type: "architecture", icon: "GitBranch" },
  { type: "architecture_diagram", icon: "Network" },
  { type: "roadmap", icon: "Map" },
  { type: "decisions", icon: "Scale" },
] as const

export type ArtifactType = (typeof ARTIFACT_TYPES)[number]["type"]

/**
 * Save or update a single artifact (e.g. architecture diagram).
 */
export function useSaveArtifact(projectId?: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      type,
      content,
      title,
    }: {
      type: string
      content: string
      title?: string
    }): Promise<ArtifactDto> => {
      if (!projectId) throw new Error("ProjectId is required")
      const response = await apiClient.put<ArtifactDto>(
        `/projects/${projectId}/artifacts/${type}`,
        { content, title }
      )
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["project-artifacts", projectId] })
    },
  })
}
