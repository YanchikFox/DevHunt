import { useMutation, useQuery } from "@tanstack/react-query"
import { apiClient } from "../client"
import type {
  AiDiagramResponse,
  AiPlanApplyResponse,
  AiPlanDraft,
  AiPlanResponse,
  AiTechStackOption,
} from "../ai-types"

export interface AiTechStackResponse {
  version: string
  options: AiTechStackOption[]
}

export interface TechStackGoals {
  performance?: boolean
  cost?: boolean
  developerSpeed?: boolean
  scalability?: boolean
  security?: boolean
  maintainability?: boolean
}

export function useGenerateTechStack(projectId?: string) {
  return useMutation({
    mutationFn: async (data: { idea: string; goals?: TechStackGoals }): Promise<AiTechStackOption[]> => {
      if (!projectId) throw new Error("ProjectId is required")
      const response = await apiClient.post<AiTechStackResponse>(`/projects/${projectId}/ai/plans/tech-stack`, {
        idea: data.idea,
        goals: data.goals,
      })
      return response.data.options
    },
  })
}

export function useGenerateAiPlan() {
  return useMutation({
    mutationFn: async (data: {
      projectId: string
      idea: string
      techStack: string
      customTags?: string[]
      customRoles?: string[]
      goals?: TechStackGoals
    }): Promise<AiPlanResponse> => {
      const response = await apiClient.post<AiPlanResponse>(
        `/projects/${data.projectId}/ai/plans`,
        {
          idea: data.idea,
          techStack: data.techStack,
          customTags: data.customTags,
          customRoles: data.customRoles,
          goals: data.goals,
        }
      )
      return response.data
    },
  })
}

export function useAiPlan(projectId?: string, planId?: string | null) {
  return useQuery({
    queryKey: ["ai-plan", projectId, planId],
    queryFn: async () => {
      const response = await apiClient.get<AiPlanResponse>(
        `/projects/${projectId}/ai/plans/${planId}`
      )
      return response.data
    },
    enabled: Boolean(projectId && planId),
  })
}

export function useApplyAiPlan() {
  return useMutation({
    mutationFn: async (data: { projectId: string; planId: string }): Promise<AiPlanApplyResponse> => {
      const response = await apiClient.post<AiPlanApplyResponse>(
        `/projects/${data.projectId}/ai/plans/${data.planId}/apply`
      )
      return response.data
    },
  })
}

export function useRefineAiPlan() {
  return useMutation({
    mutationFn: async (data: {
      projectId: string
      idea: string
      techStack: string
      currentPlan: AiPlanDraft // P1-06: Proper type instead of Record<string, unknown>
      instructions: string
      customTags?: string[]
      customRoles?: string[]
      goals?: TechStackGoals
    }): Promise<AiPlanResponse> => {
      const response = await apiClient.post<AiPlanResponse>(
        `/projects/${data.projectId}/ai/plans/refine`,
        {
          idea: data.idea,
          techStack: data.techStack,
          currentPlan: data.currentPlan,
          instructions: data.instructions,
          customTags: data.customTags,
          customRoles: data.customRoles,
          goals: data.goals,
        }
      )
      return response.data
    },
  })
}

export function useRefineTechStack(projectId?: string) {
  return useMutation({
    mutationFn: async (data: {
      idea: string
      currentOptions: AiTechStackOption[] // P1-06: Proper type instead of Record<string, unknown>[]
      instructions: string
      goals?: TechStackGoals
    }): Promise<AiTechStackOption[]> => {
      if (!projectId) throw new Error("ProjectId is required")
      const response = await apiClient.post<AiTechStackResponse>(
        `/projects/${projectId}/ai/plans/tech-stack/refine`,
        {
          idea: data.idea,
          currentOptions: data.currentOptions,
          instructions: data.instructions,
          goals: data.goals,
        }
      )
      return response.data.options
    },
  })
}

export function useGenerateDiagram(projectId?: string) {
  return useMutation({
    mutationFn: async (data: {
      techStack: string
      idea?: string
      format?: string
      diagramType?: string
      projectContext?: string
    }): Promise<AiDiagramResponse> => {
      if (!projectId) throw new Error("ProjectId is required")
      const response = await apiClient.post<AiDiagramResponse>(
        `/projects/${projectId}/ai/plans/diagram`,
        {
          techStack: data.techStack,
          idea: data.idea,
          format: data.format ?? "mermaid",
          diagramType: data.diagramType ?? "architecture",
          projectContext: data.projectContext,
        }
      )
      return response.data
    },
  })
}
