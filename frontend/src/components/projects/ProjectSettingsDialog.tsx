"use client"

import { useCallback, useEffect, useState } from "react"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { Loader2 } from "lucide-react"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useToast } from "@/hooks/use-toast"
import { apiClient } from "@/lib/api/client"
import type { ProjectMemberPermission } from "@/components/projects/ProjectPermissionsPanel"
import { useTranslations } from "next-intl"

type ProjectVisibilityOption = "public" | "members" | "subscribers" | "private"

type ProjectSettingsProject = {
  visibility?: string | null
  defaultNewsVisibility?: string | null
  newsVisibility?: string | null
  defaultFilesVisibility?: string | null
  defaultMediaVisibility?: string | null
  filesVisibility?: string | null
}

/**
 * Props for the ProjectSettingsDialog component.
 */
export interface ProjectSettingsDialogProps {
  /** Whether the dialog is open */
  open: boolean
  /** Callback function to change dialog open state */
  onOpenChange: (open: boolean) => void
  /** ID of the project */
  projectId: string
  /** Project data */
  project: ProjectSettingsProject | null
  /** Role of the current user in the project */
  role?: "owner" | "leader" | "member" | null
  /** Whether the current user can manage settings */
  canManage?: boolean
  /** ID of the project owner */
  ownerId?: string
  /** Initial list of members */
  initialMembers?: ProjectMemberPermission[]
  /** Callback function triggered when settings are updated */
  onUpdated?: () => void
}

const visibilityOptions: ProjectVisibilityOption[] = ["public", "members", "subscribers", "private"]

const normalizeVisibility = (value?: string | null): ProjectVisibilityOption => {
  const normalized = (value || "").toLowerCase()
  if (visibilityOptions.includes(normalized as ProjectVisibilityOption)) {
    return normalized as ProjectVisibilityOption
  }
  return "public"
}

/**
 * A dialog for managing project settings.
 * Includes tabs for privacy/visibility and team permissions.
 *
 * @example
 * ```tsx
 * <ProjectSettingsDialog
 *   open={isOpen}
 *   onOpenChange={setIsOpen}
 *   projectId="123"
 *   project={projectData}
 *   canManage={true}
 * />
 * ```
 */
function ProjectVisibilitySelect({ label, value, onChange, disabled, placeholder, tVal }: {
  label: string; value: string; onChange: (v: string) => void
  disabled: boolean; placeholder: string; tVal: (k: string) => string
}) {
  return (
    <div className="space-y-2">
      <Label className="text-xs uppercase tracking-wide">{label}</Label>
      <Select value={value} onValueChange={onChange} disabled={disabled}>
        <SelectTrigger><SelectValue placeholder={placeholder} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="public">{tVal("public")}</SelectItem>
          <SelectItem value="members">{tVal("members")}</SelectItem>
          <SelectItem value="subscribers">{tVal("subscribers")}</SelectItem>
          <SelectItem value="private">{tVal("private")}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  )
}

export function ProjectSettingsDialog({
  open,
  onOpenChange,
  projectId,
  project,
  role: _role,
  canManage = false,
  initialMembers: _initialMembers,
  onUpdated,
}: ProjectSettingsDialogProps) {
  const { toast } = useToast()
  const t = useTranslations("projects.settings")
  const tVal = useTranslations("projects.visibility")
  const tCommon = useTranslations("common")

  const [projectVisibility, setProjectVisibility] = useState<ProjectVisibilityOption>("public")
  const [defaultNewsVisibility, setDefaultNewsVisibility] = useState<ProjectVisibilityOption>("public")
  const [defaultFilesVisibility, setDefaultFilesVisibility] = useState<ProjectVisibilityOption>("members")
  const queryClient = useQueryClient()
  const updateProjectVisibility = useMutation({
    mutationFn: async ({
      projectId,
      visibility,
    }: {
      projectId: string
      visibility: ProjectVisibilityOption
    }) => {
      const response = await apiClient.patch(`/projects/${projectId}/visibility`, {
        Visibility: visibility,
      })
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
    },
  })

  const updateProjectSettings = useMutation({
    mutationFn: async ({
      projectId,
      settings,
    }: {
      projectId: string
      settings: Record<string, unknown>
    }) => {
      const response = await apiClient.patch(`/projects/${projectId}/settings`, settings)
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
    },
  })

  useEffect(() => {
    const visibility = normalizeVisibility(project?.visibility ?? undefined)
    const newsVisibility = normalizeVisibility(
      project?.defaultNewsVisibility ?? project?.newsVisibility ?? project?.visibility
    )
    const filesVisibility = normalizeVisibility(
      project?.defaultFilesVisibility ??
      project?.defaultMediaVisibility ??
      project?.filesVisibility ??
      project?.visibility
    )

    setProjectVisibility(visibility)
    setDefaultNewsVisibility(newsVisibility)
    setDefaultFilesVisibility(filesVisibility)
  }, [project, open])

  const handleVisibilityChange = useCallback(
    async (value: ProjectVisibilityOption) => {
      setProjectVisibility(value)
      if (!canManage || !projectId) return
      try {
        await updateProjectVisibility.mutateAsync({ projectId, visibility: value })
        onUpdated?.()
        toast({
          title: t("visibilityUpdated"),
          description: t("visibilityUpdateDesc"),
        })
      } catch (error) {
        toast({
          title: tCommon("error"),
          description: error instanceof Error ? error.message : tCommon("updateFailed"),
          variant: "destructive",
        })
      }
    },
    [canManage, projectId, updateProjectVisibility, onUpdated, toast, t, tCommon]
  )

  const handleDefaultNewsVisibilityChange = useCallback(
    (value: ProjectVisibilityOption) => setDefaultNewsVisibility(value),
    []
  )

  const handleDefaultFilesVisibilityChange = useCallback(
    (value: ProjectVisibilityOption) => setDefaultFilesVisibility(value),
    []
  )

  const handleSaveDefaults = useCallback(async () => {
    if (!projectId || !canManage) return
    try {
      await updateProjectSettings.mutateAsync({
        projectId,
        settings: {
          Visibility: projectVisibility,
          DefaultNewsVisibility: defaultNewsVisibility,
          DefaultFilesVisibility: defaultFilesVisibility,
        },
      })
      onUpdated?.()
      toast({
        title: t("settingsSaved"),
        description: t("settingsSavedDesc"),
      })
    } catch (error) {
      toast({
        title: tCommon("error"),
        description: error instanceof Error ? error.message : t("updateFailed"),
        variant: "destructive",
      })
    }
  }, [
    projectId,
    canManage,
    updateProjectSettings,
    projectVisibility,
    defaultNewsVisibility,
    defaultFilesVisibility,
    onUpdated,
    toast,
    t,
    tCommon
  ])

  const isSavingSettings = updateProjectSettings.isPending || updateProjectVisibility.isPending

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-5xl">
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
          <DialogDescription>
            {t("description")}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-6">
          <Card className="border-2">
            <CardHeader>
              <CardTitle className="text-sm font-semibold uppercase">{t("privacyTitle")}</CardTitle>
              <CardDescription>{t("privacyDescription")}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <ProjectVisibilitySelect
                label={t("projectVisibility")}
                value={projectVisibility}
                onChange={handleVisibilityChange}
                disabled={!canManage || updateProjectVisibility.isPending || updateProjectSettings.isPending}
                placeholder={t("selectVisibility")}
                tVal={tVal}
              />

              <div className="grid gap-4 sm:grid-cols-2">
                <ProjectVisibilitySelect
                  label={t("defaultNewsVisibility")}
                  value={defaultNewsVisibility}
                  onChange={handleDefaultNewsVisibilityChange}
                  disabled={!canManage || updateProjectVisibility.isPending || updateProjectSettings.isPending}
                  placeholder={t("newsVisibilityPlaceholder")}
                  tVal={tVal}
                />
                <ProjectVisibilitySelect
                  label={t("defaultFilesVisibility")}
                  value={defaultFilesVisibility}
                  onChange={handleDefaultFilesVisibilityChange}
                  disabled={!canManage || updateProjectVisibility.isPending || updateProjectSettings.isPending}
                  placeholder={t("filesVisibilityPlaceholder")}
                  tVal={tVal}
                />
              </div>

              <div className="flex justify-end">
                <Button onClick={handleSaveDefaults} disabled={!canManage || isSavingSettings}>
                  {isSavingSettings && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                  {t("save")}
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </DialogContent>
    </Dialog>
  )
}
