import { useMemo } from "react"
import { useTranslations } from "next-intl"
import type { ProjectContext } from "@/lib/api/queries/project-context"

interface UseAiChatSuggestionsParams {
  hasProjectContext: boolean
  projectContext: ProjectContext
  agentEnabled: boolean
  locale: string
}

type AiTranslation = (key: string, values?: Record<string, string | number>) => string

/**
 * Hook to generate dynamic AI chat suggestions based on project context.
 * Extracted from AiChatWindow to reduce component complexity.
 */
export function useAiChatSuggestions({
  hasProjectContext,
  projectContext,
  agentEnabled,
}: UseAiChatSuggestionsParams): string[] {
  const t = useTranslations("ai")

  return useMemo(() => {
    if (!hasProjectContext || !projectContext.project) {
      return [
        t("suggestions.default1"),
        t("suggestions.default2"),
        t("suggestions.default3")
      ]
    }

    return agentEnabled
      ? getAgentSuggestions(projectContext, t)
      : getReadOnlySuggestions(projectContext, t)
  }, [hasProjectContext, projectContext, agentEnabled, t])
}

// Condition helpers to simplify logic
const hasActiveTasks = (ctx: ProjectContext): boolean =>
  (ctx.tasks.byStatus["todo"] || 0) + (ctx.tasks.byStatus["doing"] || 0) > 0

const hasEmptyTechStack = (ctx: ProjectContext): boolean =>
  !ctx.project?.techStack || ctx.project.techStack.length === 0

const hasTechStack = (ctx: ProjectContext): boolean =>
  Boolean(ctx.project?.techStack && ctx.project.techStack.length > 0)

const hasAnyTasks = (ctx: ProjectContext): boolean => ctx.tasks.total > 0

const hasSmallTeam = (ctx: ProjectContext): boolean => ctx.team.total < 3

/** Suggestions for agent mode (can modify tasks). */
function getAgentSuggestions(ctx: ProjectContext, t: AiTranslation): string[] {
  const result: string[] = [t("suggestions.createTask")]

  result.push(hasActiveTasks(ctx) ? t("suggestions.whichTask") : t("suggestions.createTasks"))

  if (hasEmptyTechStack(ctx)) {
    result.push(t("suggestions.updateStack"))
  }

  return result
}

/** Suggestions for read-only mode (viewing only). */
function getReadOnlySuggestions(ctx: ProjectContext, t: AiTranslation): string[] {
  const result: string[] = []

  // Tech stack suggestion
  if (hasTechStack(ctx)) {
    result.push(t("suggestions.explainStack", { stack: ctx.project!.techStack[0] }))
  } else {
    result.push(t("suggestions.suggestStack"))
  }

  // Tasks suggestion
  result.push(hasAnyTasks(ctx) ? t("suggestions.inProgress") : t("suggestions.showTasks"))

  // Team suggestion
  if (hasSmallTeam(ctx)) {
    result.push(t("suggestions.whoInTeam"))
  }

  return result
}
