import { z } from "zod"

export const AiTechStackOptionSchema = z.object({
  name: z.string(),
  description: z.string(),
  pros: z.array(z.string()),
  cons: z.array(z.string()),
})

export type AiTechStackOption = z.infer<typeof AiTechStackOptionSchema>

export const AiPlanTaskSchema = z.object({
  id: z.string(),
  title: z.string(),
  description: z.string(),
  dependsOn: z.array(z.string()).default([]),
  /** Area tags from the plan (frontend, backend, design, devops, qa, …) — used to suggest open roles. */
  tags: z.array(z.string()).default([]),
})

export type AiPlanTask = z.infer<typeof AiPlanTaskSchema>

export const AiPlanPhaseSchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string(),
  goals: z.array(z.string()).default([]),
  tasks: z.array(AiPlanTaskSchema).default([]),
})

export type AiPlanPhase = z.infer<typeof AiPlanPhaseSchema>

export const AiPlanDraftSchema = z.object({
  version: z.string(),
  idea: z.string(),
  techStack: z.string(),
  phases: z.array(AiPlanPhaseSchema),
  /** LLM that produced this plan (from Core API / ML service). */
  model: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
})

export type AiPlanDraft = z.infer<typeof AiPlanDraftSchema>

export const AiPlanResponseSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  status: z.string(),
  idea: z.string(),
  techStack: z.string(),
  planVersion: z.string(),
  createdAt: z.string(),
  appliedAt: z.string().nullable().optional(),
  draft: AiPlanDraftSchema,
})

export type AiPlanResponse = z.infer<typeof AiPlanResponseSchema>

export const AiPlanApplyResponseSchema = z.object({
  status: z.enum(["applied", "already_applied"]),
  taskCount: z.number(),
  linkCount: z.number(),
  appliedAt: z.string().nullable(),
})

export type AiPlanApplyResponse = z.infer<typeof AiPlanApplyResponseSchema>

// Diagram types

export const AiDiagramResponseSchema = z.object({
  code: z.string(),
  format: z.string().optional().default("mermaid"),
})

export type AiDiagramResponse = z.infer<typeof AiDiagramResponseSchema>
