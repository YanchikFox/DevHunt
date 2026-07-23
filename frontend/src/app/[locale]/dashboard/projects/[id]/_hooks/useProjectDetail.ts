"use client"

import { useTranslations } from "next-intl"
import { useProjectDetailIntegrations } from "./useProjectDetailIntegrations"
import { useProjectLifecycleActions } from "./useProjectLifecycleActions"
import { useProjectCoreData } from "./useProjectCoreData"
import { useToast } from "@/hooks/use-toast"
import { useCurrentUser } from "@/hooks/use-current-user"
import { useProjectDialogs } from "@/hooks/use-project-dialogs"
import { useProjectStatusActions } from "@/hooks/use-project-status-actions"
import type { ProjectWithExtras } from "./types"
import { useProjectDetailParams } from "./useProjectDetailParams"
import { useProjectDetailActivity } from "./useProjectDetailActivity"
import { useProjectDetailMedia } from "./useProjectDetailMedia"
import { useProjectDetailTasks } from "./useProjectDetailTasks"
import { useProjectDetailTeam } from "./useProjectDetailTeam"

interface ProjectDetailParams {
  id: string
}

const getStatusColor = (status: string) => {
  switch (status) {
    case "completed": case "done": return "bg-green-100 text-green-700 dark:bg-green-900/20 dark:text-green-400"
    case "in_progress": case "doing": return "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400"
    case "recruiting": case "review": return "bg-orange-100 text-orange-700 dark:bg-orange-900/20 dark:text-orange-400"
    default: return "bg-gray-100 text-gray-700 dark:bg-gray-900/20 dark:text-gray-400"
  }
}

export function useProjectDetail(params: Promise<ProjectDetailParams> | ProjectDetailParams) {
  const t = useTranslations()
  const { toast } = useToast()
  const { user: currentUser } = useCurrentUser()

  const { finalProjectId, hasProjectId, mounted } = useProjectDetailParams(params)

  const {
    isUuidParam, resolvedProjectId, hasResolvedId, slugLookupLoading,
    project, projectLoading, projectError, refetchProject,
    tasks, tasksLoading, tasksError,
    permissions, teamMembers, teamLoading, teamError,
    incomingInvitations, sentInvitations, respondToInvitation, cancelInvitation, sendInvitation,
  } = useProjectCoreData(finalProjectId, hasProjectId)

  const dialogs = useProjectDialogs()
  const statusActions = useProjectStatusActions({ projectId: project?.id || null, t, refetchProject })

  const typedProject = project as ProjectWithExtras | undefined
  const canEdit = permissions?.canEdit ?? false
  const canManageTeam = permissions?.canManageTeam ?? false
  const canViewTasks = permissions?.canViewTasks ?? false
  const canEditTasks = canEdit || (permissions?.canManageTasks ?? false)
  const canManageSettings = Boolean(permissions?.role === "owner" || permissions?.role === "leader" || permissions?.canManageTeam)

  const teamState = useProjectDetailTeam({
    project: typedProject, teamMembers, incomingInvitations, sentInvitations,
    respondToInvitation, cancelInvitation, sendInvitation,
    projectId: resolvedProjectId, currentUserId: currentUser?.id,
    refetchProject, t, toast,
    setIsInviteDialogOpen: dialogs.setIsInviteDialogOpen,
    setIsRequestDialogOpen: dialogs.setIsRequestDialogOpen,
  })

  const tasksState = useProjectDetailTasks({
    projectId: resolvedProjectId, tasks: Array.isArray(tasks) ? tasks : [],
    canEditTasks, t, refetchProject,
    isTaskDialogOpen: dialogs.isTaskDialogOpen, setIsTaskDialogOpen: dialogs.setIsTaskDialogOpen, toast,
  })

  const activityState = useProjectDetailActivity(resolvedProjectId, hasResolvedId, t)

  const mediaState = useProjectDetailMedia({
    projectId: resolvedProjectId, project: typedProject, t, toast,
    setIsAddNewsDialogOpen: dialogs.setIsAddNewsDialogOpen,
    setIsUploadMediaDialogOpen: dialogs.setIsUploadMediaDialogOpen,
    deletingGalleryImageId: dialogs.deletingGalleryImageId,
    setDeletingGalleryImageId: dialogs.setDeletingGalleryImageId,
    deletingNewsId: dialogs.deletingNewsId, setDeletingNewsId: dialogs.setDeletingNewsId,
  })

  const integrationState = useProjectDetailIntegrations(resolvedProjectId, hasResolvedId, toast, t)
  const lifecycleActions = useProjectLifecycleActions(
    project ? { id: project.id, title: project.title ?? "" } : undefined,
    resolvedProjectId, refetchProject, toast, t,
    dialogs.setIsEditDialogOpen, dialogs.deleteConfirmText,
  )

  return {
    t, mounted,
    projectId: resolvedProjectId,
    hasProjectId: hasProjectId && (isUuidParam || hasResolvedId || slugLookupLoading),
    project, projectLoading, projectError,
    tasksLoading, tasksError, teamLoading, teamError,
    permissions, canEdit, canManageTeam, canViewTasks, canEditTasks, canManageSettings,
    currentUserId: currentUser?.id, dialogs, statusActions,
    tasks: tasksState, team: teamState, media: mediaState, activity: activityState,
    ...integrationState, ...lifecycleActions, getStatusColor,
  }
}

export type ProjectDetailState = ReturnType<typeof useProjectDetail>
