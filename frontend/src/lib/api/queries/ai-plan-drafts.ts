import { useMutation } from "@tanstack/react-query"
import { z } from "zod"
import { apiClient } from "../client"
import {
  AiPlanDraftSchema,
  AiTechStackOptionSchema,
  type AiPlanDraft,
  type AiTechStackOption,
} from "../ai-types"
import type { TechStackGoals } from "./ai-plans"

/**
 * Pre-project ("ephemeral") AI planning — the user hasn't picked/created a
 * project yet, so these calls hit `/api/ai/plans/draft` which does not write
 * to the database. Once they're happy, a separate "Create project from plan"
 * flow creates a real project and applies the plan through the existing
 * project-scoped endpoints.
 */

export const AiPlanDraftResponseSchema = z.object({
  idea: z.string(),
  techStack: z.string(),
  planVersion: z.string(),
  draft: AiPlanDraftSchema,
})

export type AiPlanDraftResponse = z.infer<typeof AiPlanDraftResponseSchema>

export const AiTechStackResponseSchema = z.object({
  version: z.string(),
  options: z.array(AiTechStackOptionSchema),
  promptVersion: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
})

export type AiTechStackResponse = z.infer<typeof AiTechStackResponseSchema>

export interface GenerateAiPlanDraftInput {
  readonly idea: string
  readonly techStack?: string
  readonly customTags?: string[]
  readonly customRoles?: string[]
  readonly goals?: TechStackGoals
}

export interface RefineAiPlanDraftInput {
  readonly idea: string
  readonly techStack: string
  readonly currentPlan: AiPlanDraft
  readonly instructions: string
  readonly customTags?: string[]
  readonly customRoles?: string[]
  readonly goals?: TechStackGoals
}

export interface SuggestDraftTechStackInput {
  readonly idea: string
  readonly goals?: TechStackGoals
}

export function useGenerateAiPlanDraft() {
  return useMutation({
    mutationFn: async (input: GenerateAiPlanDraftInput): Promise<AiPlanDraftResponse> => {
      const response = await apiClient.post<AiPlanDraftResponse>("/ai/plans/draft", {
        idea: input.idea,
        techStack: input.techStack,
        customTags: input.customTags,
        customRoles: input.customRoles,
        goals: input.goals,
      })
      return AiPlanDraftResponseSchema.parse(response.data)
    },
  })
}

export function useRefineAiPlanDraft() {
  return useMutation({
    mutationFn: async (input: RefineAiPlanDraftInput): Promise<AiPlanDraftResponse> => {
      const response = await apiClient.post<AiPlanDraftResponse>("/ai/plans/draft/refine", {
        idea: input.idea,
        techStack: input.techStack,
        currentPlan: input.currentPlan,
        instructions: input.instructions,
        customTags: input.customTags,
        customRoles: input.customRoles,
        goals: input.goals,
      })
      return AiPlanDraftResponseSchema.parse(response.data)
    },
  })
}

export function useSuggestDraftTechStack() {
  return useMutation({
    mutationFn: async (input: SuggestDraftTechStackInput): Promise<AiTechStackOption[]> => {
      const response = await apiClient.post<AiTechStackResponse>("/ai/plans/draft/tech-stack", {
        idea: input.idea,
        goals: input.goals,
      })
      return AiTechStackResponseSchema.parse(response.data).options
    },
  })
}
