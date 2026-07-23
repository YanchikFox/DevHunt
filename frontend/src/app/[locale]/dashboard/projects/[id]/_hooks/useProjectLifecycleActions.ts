"use client"

import { useCallback } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { useTranslations } from "next-intl"
import { useRouter } from "@/i18n/routing"
import { useUpdateProject, useDeleteProject } from "@/lib/api/queries/projects"
import { useToast } from "@/hooks/use-toast"
import type { Project as SchemaProject } from "@/lib/api/schema"
import type { Project } from "./types"

export function useProjectLifecycleActions(
  project: { id: string; title: string } | null | undefined,
  resolvedProjectId: string,
  refetchProject: () => void,
  toast: ReturnType<typeof useToast>["toast"],
  t: ReturnType<typeof useTranslations>,
  setIsEditDialogOpen: (open: boolean) => void,
  deleteConfirmText: string,
) {
  const queryClient = useQueryClient()
  const router = useRouter()
  const updateProject = useUpdateProject()
  const deleteProject = useDeleteProject()

  const handleSettingsUpdated = useCallback(() => {
    refetchProject()
    queryClient.invalidateQueries({ queryKey: ["projects", resolvedProjectId] })
  }, [resolvedProjectId, queryClient, refetchProject])

  const handleSaveProject = useCallback(async (data: Partial<Project>) => {
    if (!project) return
    try {
      const { status, visibility, ...rest } = data
      const payload: Partial<SchemaProject> = {
        ...rest,
        ...(status ? { status: status as SchemaProject["status"] } : {}),
        ...(visibility ? { visibility: visibility as SchemaProject["visibility"] } : {}),
      }
      await updateProject.mutateAsync({ id: project.id, data: payload })
      toast({ title: t("common.success"), description: t("projects.projectUpdated") })
      setIsEditDialogOpen(false)
    } catch (error) {
      toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("projects.projectUpdateFailed"), variant: "destructive" })
    }
  }, [project, updateProject, toast, t, setIsEditDialogOpen])

  const handleDeleteProject = useCallback(async () => {
    if (!project || deleteConfirmText !== project.title) return
    try {
      await deleteProject.mutateAsync(project.id)
      toast({ title: t("projects.projectDeleted"), description: t("projects.projectDeleteSuccess") })
      router.push("/dashboard/projects")
    } catch (error) {
      toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("projects.projectDeleteFailed"), variant: "destructive" })
    }
  }, [project, deleteConfirmText, deleteProject, toast, router, t])

  return {
    updateProjectPending: updateProject.isPending,
    deleteProjectPending: deleteProject.isPending,
    handleSettingsUpdated,
    handleSaveProject,
    handleDeleteProject,
  }
}
