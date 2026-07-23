"use client"

import { useState, useCallback } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { useToast } from "@/hooks/use-toast"
import { apiClient } from "@/lib/api/client"

export type ProjectStatusActionType =
  | "publish"
  | "activate"
  | "complete"
  | "archive"
  | "unarchive"
  | "unpublish"

export interface ConfirmAction {
  type: ProjectStatusActionType | null
  projectId: string | null
}

interface UseProjectStatusActionsOptions {
  projectId: string | null
  t: (key: string, values?: Record<string, string | number>) => string
  refetchProject: () => Promise<unknown>
}

export function useProjectStatusActions({
  projectId,
  t,
  refetchProject,
}: UseProjectStatusActionsOptions) {
  const queryClient = useQueryClient()
  const { toast } = useToast()

  const [confirmAction, setConfirmAction] = useState<ConfirmAction>({
    type: null,
    projectId: null,
  })
  const [isChangingStatus, setIsChangingStatus] = useState(false)

  // Action creators
  const handlePublishProject = useCallback(() => {
    setConfirmAction({ type: "publish", projectId })
  }, [projectId])

  const handleActivateProject = useCallback(() => {
    setConfirmAction({ type: "activate", projectId })
  }, [projectId])

  const handleCompleteProject = useCallback(() => {
    setConfirmAction({ type: "complete", projectId })
  }, [projectId])

  const handleArchiveProject = useCallback(() => {
    setConfirmAction({ type: "archive", projectId })
  }, [projectId])

  const handleUnarchiveProject = useCallback(() => {
    setConfirmAction({ type: "unarchive", projectId })
  }, [projectId])

  const handleUnpublishProject = useCallback(() => {
    setConfirmAction({ type: "unpublish", projectId })
  }, [projectId])

  // Close dialog
  const handleCloseConfirmDialog = useCallback((open: boolean) => {
    if (!open) setConfirmAction({ type: null, projectId: null })
  }, [])

  // Confirm status change
  const handleConfirmStatusChange = useCallback(async () => {
    if (!confirmAction.type || !confirmAction.projectId) return

    try {
      setIsChangingStatus(true)
      // L-01/L-02: Call dedicated lifecycle endpoints instead of generic PATCH /status
      // Each endpoint validates business rules, publishes events, logs activity, triggers achievements
      const actionEndpoints: Record<ProjectStatusActionType, string> = {
        publish: `/projects/${confirmAction.projectId}/publish`,
        activate: `/projects/${confirmAction.projectId}/activate`,
        complete: `/projects/${confirmAction.projectId}/complete`,
        archive: `/projects/${confirmAction.projectId}/archive`,
        unarchive: `/projects/${confirmAction.projectId}/unarchive`,
        unpublish: `/projects/${confirmAction.projectId}/publish`, // draft → recruiting uses publish endpoint
      }

      await apiClient.post(actionEndpoints[confirmAction.type])

      await queryClient.invalidateQueries({ queryKey: ["projects", confirmAction.projectId] })
      await refetchProject()

      const actionTranslations: Record<ProjectStatusActionType, string> = {
        publish: t("projects.published"),
        activate: t("projects.activated"),
        complete: t("projects.completed"),
        archive: t("projects.archived"),
        unarchive: t("projects.unarchived"),
        unpublish: t("projects.unpublished"),
      }
      toast({
        title: t("common.success"),
        description: t("projects.projectStatusChanged", { action: actionTranslations[confirmAction.type] }),
      })
      setConfirmAction({ type: null, projectId: null })
    } catch (error) {
      console.error("Error changing project status:", error)
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("projects.failedToChangeStatus"),
        variant: "destructive",
      })
    } finally {
      setIsChangingStatus(false)
    }
  }, [confirmAction, queryClient, refetchProject, t, toast])

  const getConfirmDialogTexts = useCallback(() => {
    const titles: Record<ProjectStatusActionType, string> = {
      publish: t("projects.publishConfirm"),
      activate: t("projects.activateConfirm"),
      complete: t("projects.completeConfirm"),
      archive: t("projects.archiveConfirm"),
      unarchive: t("projects.unarchiveConfirm"),
      unpublish: t("projects.unpublishConfirm"),
    }

    const descriptions: Record<ProjectStatusActionType, string> = {
      publish: t("projects.publishConfirmDesc"),
      activate: t("projects.activateConfirmDesc"),
      complete: t("projects.completeConfirmDesc"),
      archive: t("projects.archiveConfirmDesc"),
      unarchive: t("projects.unarchiveConfirmDesc"),
      unpublish: t("projects.unpublishConfirmDesc"),
    }

    if (!confirmAction.type) {
      return { title: "", description: "" }
    }

    return {
      title: titles[confirmAction.type],
      description: descriptions[confirmAction.type],
    }
  }, [confirmAction.type, t])

  return {
    // State
    confirmAction,
    setConfirmAction,
    isChangingStatus,

    // Actions
    handlePublishProject,
    handleActivateProject,
    handleCompleteProject,
    handleArchiveProject,
    handleUnarchiveProject,
    handleUnpublishProject,
    handleCloseConfirmDialog,
    handleConfirmStatusChange,

    // Utils
    getConfirmDialogTexts,
  }
}
