"use client"

import { useState, useCallback } from "react"
import { useAdminProjectAction } from "@/lib/api/queries/admin"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, EyeOff, Archive, Star, StarOff } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "@/lib/api/client"

interface ProjectListResponse {
  total: number
  page: number
  pageSize: number
  data: ProjectSummary[]
}

interface ProjectSummary {
  id: string
  title: string
  ownerName: string
  status: string
  visibility: string
  featured: boolean
  createdAt: string
}

type ProjectActionType = "hide" | "archive" | "feature" | "unfeature"

function useAdminProjects(page = 1, pageSize = 20) {
  return useQuery({
    queryKey: ["admin", "projects", page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      const { data } = await api.get<ProjectListResponse>(`/projects?${params.toString()}`)
      return data
    },
  })
}

export function ProjectManagement() {
  const t = useTranslations("admin")
  const { toast } = useToast()
  const [page, setPage] = useState(1)
  const [actionReason, setActionReason] = useState("")
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
  const [actionType, setActionType] = useState<ProjectActionType | null>(null)

  const { data, isLoading } = useAdminProjects(page)
  const projectAction = useAdminProjectAction()
  const queryClient = useQueryClient()

  const handleAction = useCallback(async (projectId: string, action: ProjectActionType, reason: string) => {
    try {
      await projectAction.mutateAsync({ projectId, action, reason })
      toast({ title: t("projectActionSuccess") })
      queryClient.invalidateQueries({ queryKey: ["admin", "projects"] })
      setSelectedProjectId(null)
      setActionReason("")
      setActionType(null)
    } catch {
      toast({ title: t("projectActionFailed"), variant: "destructive" })
    }
  }, [projectAction, toast, t, queryClient])

  const openActionDialog = useCallback((projectId: string, action: ProjectActionType) => {
    setSelectedProjectId(projectId)
    setActionType(action)
    setActionReason("")
  }, [])

  const handleConfirmAction = useCallback(() => {
    if (selectedProjectId && actionType && actionReason.trim()) {
      handleAction(selectedProjectId, actionType, actionReason)
    }
  }, [selectedProjectId, actionType, actionReason, handleAction])

  const handleDialogClose = useCallback((open: boolean) => {
    if (!open) {
      setSelectedProjectId(null)
      setActionType(null)
      setActionReason("")
    }
  }, [])

  const handleReasonChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setActionReason(e.target.value)
  }, [])

  const handlePrevPage = useCallback(() => setPage((p) => p - 1), [])
  const handleNextPage = useCallback(() => setPage((p) => p + 1), [])

  const getDialogTitle = () => {
    switch (actionType) {
      case "hide": return t("hideProject")
      case "archive": return t("archiveProject")
      case "feature": return t("projectManagement.featureProject")
      case "unfeature": return t("projectManagement.unfeatureProject")
      default: return ""
    }
  }

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin" />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("projectTitle")}</TableHead>
              <TableHead>{t("owner")}</TableHead>
              <TableHead>{t("status")}</TableHead>
              <TableHead>{t("visibility")}</TableHead>
              <TableHead>{t("featured")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data?.data?.map((project) => (
              <ProjectRow
                key={project.id}
                project={project}
                onOpenDialog={openActionDialog}
              />
            ))}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      <div className="flex justify-center gap-2">
        <Button variant="outline" disabled={page === 1} onClick={handlePrevPage}>
          {t("previous")}
        </Button>
        <Button variant="outline" disabled={!data || (data.data?.length ?? 0) < 20} onClick={handleNextPage}>
          {t("next")}
        </Button>
      </div>

      {/* Action Dialog for all project actions */}
      <Dialog open={!!selectedProjectId && !!actionType} onOpenChange={handleDialogClose}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{getDialogTitle()}</DialogTitle>
            <DialogDescription className="sr-only">{t("reasonPlaceholder")}</DialogDescription>
          </DialogHeader>
          <div className="py-4 space-y-2">
            <p className="text-sm text-muted-foreground">
              <span className="text-destructive">*</span> {t("reasonPlaceholder")}
            </p>
            <Textarea
              placeholder={t("reasonPlaceholder")}
              value={actionReason}
              onChange={handleReasonChange}
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button
              variant={actionType === "archive" ? "destructive" : "default"}
              onClick={handleConfirmAction}
              disabled={!actionReason.trim() || projectAction.isPending}
            >
              {projectAction.isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
              {t("confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

interface ProjectRowProps {
  project: ProjectSummary
  onOpenDialog: (projectId: string, action: ProjectActionType) => void
}

function ProjectRow({ project, onOpenDialog }: ProjectRowProps) {
  const t = useTranslations("admin")

  const handleFeatureToggle = useCallback(() => {
    onOpenDialog(project.id, project.featured ? "unfeature" : "feature")
  }, [project.id, project.featured, onOpenDialog])

  const handleHide = useCallback(() => {
    onOpenDialog(project.id, "hide")
  }, [project.id, onOpenDialog])

  const handleArchive = useCallback(() => {
    onOpenDialog(project.id, "archive")
  }, [project.id, onOpenDialog])

  return (
    <TableRow>
      <TableCell className="font-medium">{project.title}</TableCell>
      <TableCell>{project.ownerName}</TableCell>
      <TableCell>
        <Badge variant="outline">{project.status}</Badge>
      </TableCell>
      <TableCell>
        <Badge variant={project.visibility === "public" ? "outline" : "secondary"}>
          {project.visibility}
        </Badge>
      </TableCell>
      <TableCell>
        {project.featured && (
          <Badge className="bg-yellow-100 text-yellow-700 border-yellow-200">
            <Star className="h-3 w-3 mr-1" /> {t("featured")}
          </Badge>
        )}
      </TableCell>
      <TableCell>
        <div className="flex gap-2">
          <Button size="sm" variant="ghost" onClick={handleFeatureToggle}>
            {project.featured ? <StarOff className="h-4 w-4" /> : <Star className="h-4 w-4" />}
          </Button>
          <Button size="sm" variant="ghost" onClick={handleHide}>
            <EyeOff className="h-4 w-4" />
          </Button>
          <Button size="sm" variant="ghost" className="text-destructive" onClick={handleArchive}>
            <Archive className="h-4 w-4" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  )
}
