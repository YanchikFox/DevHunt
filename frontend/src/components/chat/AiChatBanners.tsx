"use client"

import { memo } from "react"
import { AlertTriangle, Info, Wrench, Lock } from "lucide-react"
import type { ProjectContext } from "@/lib/api/queries/project-context"

interface BannerProps {
  t: (key: string) => string | undefined
}

interface ProjectContextBannerProps extends BannerProps {
  projectContext: ProjectContext
}

interface AgentModeBannerProps {
  t: (key: string) => string
  agentEnabled: boolean
}

/**
 * AI Disclaimer Banner - warns that content is AI-generated.
 */
export const AiDisclaimerBanner = memo(function AiDisclaimerBanner({ t }: BannerProps) {
  return (
    <div className="px-3 py-2 bg-amber-50 dark:bg-amber-950/30 border-b border-amber-200 dark:border-amber-800 flex items-center gap-2">
      <AlertTriangle className="h-4 w-4 text-amber-600 dark:text-amber-400 shrink-0" />
      <p className="text-xs text-amber-700 dark:text-amber-300">
        {t("ai.disclaimer")}
      </p>
    </div>
  )
})

/**
 * Project Context Banner - shows that project context is loaded.
 */
export const ProjectContextBanner = memo(function ProjectContextBanner({ t, projectContext }: ProjectContextBannerProps) {
  return (
    <div className="flex items-center gap-2 border-b border-primary/15 bg-primary/5 px-3 py-2">
      <Info className="h-4 w-4 shrink-0 text-primary" />
      <p className="text-xs text-primary">
        {t("ai.contextLoaded")}
        <span className="ml-2 opacity-75">
          ({projectContext.team.total} {t("ai.membersShort")}, {projectContext.tasks.total} {t("ai.tasksShort")})
        </span>
      </p>
    </div>
  )
})

/**
 * Agent Mode Banner - shows agent or guest mode status.
 */
export const AgentModeBanner = memo(function AgentModeBanner({ t, agentEnabled }: AgentModeBannerProps) {
  if (agentEnabled) {
    return (
      <div className="px-3 py-2 bg-emerald-50 dark:bg-emerald-950/30 border-b border-emerald-200 dark:border-emerald-800 flex items-center gap-2">
        <Wrench className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
        <p className="text-xs text-emerald-700 dark:text-emerald-300">
          {t("ai.agentMode")}
        </p>
      </div>
    )
  }

  return (
    <div className="px-3 py-2 bg-muted/30 border-b border-border flex items-center gap-2">
      <Lock className="h-3 w-3 text-muted-foreground shrink-0" />
      <p className="text-xs text-muted-foreground">
        {t("ai.guestMode")}
      </p>
    </div>
  )
})
