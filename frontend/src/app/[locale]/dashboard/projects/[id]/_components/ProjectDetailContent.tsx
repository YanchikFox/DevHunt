"use client"

import type { ProjectDetailState } from "../_hooks/useProjectDetail"
import { ProjectTaskDialog } from "./ProjectTaskDialog"
import { ProjectDetailDialogs } from "./ProjectDetailDialogs"
import { ProjectDashboard } from "./ProjectDashboard"

interface ProjectDetailContentProps {
  detail: ProjectDetailState
}

export function ProjectDetailContent({ detail }: ProjectDetailContentProps) {
  const { project } = detail

  if (!project) return null

  return (
    <div className="h-full animate-in fade-in slide-in-from-bottom-4 duration-700">
      <ProjectDashboard detail={detail} />
      <ProjectTaskDialog detail={detail} />
      <ProjectDetailDialogs detail={detail} />
    </div>
  )
}
