"use client"

import { useState, useCallback } from "react"
import {
  useAdminContentProjects,
  useAdminDeleteProject,
  type AdminContentProject,
} from "@/lib/api/queries/admin-content"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Trash2, Pencil, Star, Eye } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { EditProjectDialog } from "./EditProjectDialog"
import { CenteredLoader } from "@/components/ui/loading"
import { TableEmptyState } from "@/components/ui/empty-state"
import { DataTablePagination } from "@/components/ui/data-table-pagination"

const statusColors: Record<string, string> = {
  active: "bg-green-50 text-green-700 border-green-200",
  archived: "bg-gray-50 text-gray-700 border-gray-200",
  draft: "bg-yellow-50 text-yellow-700 border-yellow-200",
  completed: "bg-blue-50 text-blue-700 border-blue-200",
}

const visibilityColors: Record<string, string> = {
  public: "bg-emerald-50 text-emerald-700 border-emerald-200",
  private: "bg-red-50 text-red-700 border-red-200",
  unlisted: "bg-orange-50 text-orange-700 border-orange-200",
}

interface ProjectFiltersProps {
  searchQuery: string
  statusFilter: string
  visibilityFilter: string
  featuredFilter: string
  onSearchChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  onStatusChange: (v: string) => void
  onVisibilityChange: (v: string) => void
  onFeaturedChange: (v: string) => void
  t: (key: string) => string
}

function ProjectFilters({
  searchQuery, statusFilter, visibilityFilter, featuredFilter,
  onSearchChange, onStatusChange, onVisibilityChange, onFeaturedChange, t,
}: ProjectFiltersProps) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Input placeholder={t("searchProjects")} value={searchQuery} onChange={onSearchChange} className="w-64" />
      <Select value={statusFilter} onValueChange={onStatusChange}>
        <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allStatuses")}</SelectItem>
          <SelectItem value="active">{t("active")}</SelectItem>
          <SelectItem value="draft">{t("draft")}</SelectItem>
          <SelectItem value="archived">{t("archived")}</SelectItem>
          <SelectItem value="completed">{t("completed")}</SelectItem>
        </SelectContent>
      </Select>
      <Select value={visibilityFilter} onValueChange={onVisibilityChange}>
        <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allVisibility")}</SelectItem>
          <SelectItem value="public">{t("public")}</SelectItem>
          <SelectItem value="private">{t("private")}</SelectItem>
          <SelectItem value="unlisted">{t("unlisted")}</SelectItem>
        </SelectContent>
      </Select>
      <Select value={featuredFilter} onValueChange={onFeaturedChange}>
        <SelectTrigger className="w-36"><SelectValue /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allProjects")}</SelectItem>
          <SelectItem value="true">{t("featuredOnly")}</SelectItem>
          <SelectItem value="false">{t("notFeatured")}</SelectItem>
        </SelectContent>
      </Select>
    </div>
  )
}

export function ProjectsContent() {
  const t = useTranslations("admin")
  const { toast } = useToast()

  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] = useState("all")
  const [visibilityFilter, setVisibilityFilter] = useState("all")
  const [featuredFilter, setFeaturedFilter] = useState("all")
  const [searchQuery, setSearchQuery] = useState("")

  const [editProject, setEditProject] = useState<AdminContentProject | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<AdminContentProject | null>(null)

  const { data, isLoading } = useAdminContentProjects(page, 20, {
    status: statusFilter !== "all" ? statusFilter : undefined,
    visibility: visibilityFilter !== "all" ? visibilityFilter : undefined,
    featured: featuredFilter !== "all" ? featuredFilter === "true" : undefined,
    search: searchQuery || undefined,
  })

  const deleteProject = useAdminDeleteProject()

  const handleDelete = useCallback(async () => {
    if (!deleteTarget) return
    try {
      await deleteProject.mutateAsync(deleteTarget.id)
      toast({ title: t("projectDeleted") })
      setDeleteTarget(null)
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [deleteTarget, deleteProject, toast, t])

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
    setPage(1)
  }, [])
  const handleStatusChange = useCallback((value: string) => { setStatusFilter(value); setPage(1) }, [])
  const handleVisibilityChange = useCallback((value: string) => { setVisibilityFilter(value); setPage(1) }, [])
  const handleFeaturedChange = useCallback((value: string) => { setFeaturedFilter(value); setPage(1) }, [])
  const handlePrevPage = useCallback(() => setPage(p => p - 1), [])
  const handleNextPage = useCallback(() => setPage(p => p + 1), [])
  const handleCloseEditDialog = useCallback((open: boolean) => { if (!open) setEditProject(null) }, [])
  const handleCloseDeleteDialog = useCallback((open: boolean) => { if (!open) setDeleteTarget(null) }, [])
  const handleCancelDelete = useCallback(() => setDeleteTarget(null), [])

  const projects = data?.data ?? []
  const pagination = data?.pagination

  return (
    <div className="space-y-4">
      <ProjectFilters
        searchQuery={searchQuery}
        statusFilter={statusFilter}
        visibilityFilter={visibilityFilter}
        featuredFilter={featuredFilter}
        onSearchChange={handleSearchChange}
        onStatusChange={handleStatusChange}
        onVisibilityChange={handleVisibilityChange}
        onFeaturedChange={handleFeaturedChange}
        t={t}
      />

      {/* Table */}
      {isLoading ? (
        <CenteredLoader />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("title")}</TableHead>
              <TableHead>{t("owner")}</TableHead>
              <TableHead>{t("status")}</TableHead>
              <TableHead>{t("visibility")}</TableHead>
              <TableHead>{t("team")}</TableHead>
              <TableHead>{t("tasks")}</TableHead>
              <TableHead>{t("created")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {projects.map((project) => (
              <ProjectRow
                key={project.id}
                project={project}
                onEdit={setEditProject}
                onDelete={setDeleteTarget}
                t={t}
              />
            ))}
            {projects.length === 0 && (
              <TableEmptyState colSpan={8} description={t("noProjects")} />
            )}
          </TableBody>
        </Table>
      )}

      {/* Pagination */}
      <DataTablePagination 
        page={page} 
        pagination={pagination} 
        onPrevPage={handlePrevPage} 
        onNextPage={handleNextPage} 
        t={t} 
      />

      {/* Edit Dialog */}
      {editProject && (
        <EditProjectDialog
          projectId={editProject.id}
          open={editProject !== null}
          onOpenChange={handleCloseEditDialog}
        />
      )}

      {/* Delete Confirmation */}
      <Dialog open={deleteTarget !== null} onOpenChange={handleCloseDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("deleteProject")}</DialogTitle>
            <DialogDescription>
              {t("deleteProjectConfirm", { title: deleteTarget?.title ?? "" })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelDelete}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteProject.isPending}>
              {deleteProject.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("delete")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

interface ProjectRowProps {
  project: AdminContentProject
  onEdit: (p: AdminContentProject) => void
  onDelete: (p: AdminContentProject) => void
  t: (key: string) => string
}

function ProjectRow({ project, onEdit, onDelete, t: _t }: ProjectRowProps) {
  const handleEdit = useCallback(() => onEdit(project), [onEdit, project])
  const handleDelete = useCallback(() => onDelete(project), [onDelete, project])

  return (
    <TableRow>
      <TableCell className="font-medium max-w-[200px] truncate">
        <div className="flex items-center gap-1.5">
          {project.featured && <Star className="h-3.5 w-3.5 text-yellow-500 fill-yellow-500 shrink-0" />}
          {project.showcasePublished && <Eye className="h-3.5 w-3.5 text-blue-500 shrink-0" />}
          <span className="truncate">{project.title}</span>
        </div>
      </TableCell>
      <TableCell className="text-sm">{project.ownerName || "—"}</TableCell>
      <TableCell>
        <Badge variant="outline" className={statusColors[project.status] || ""}>
          {project.status}
        </Badge>
      </TableCell>
      <TableCell>
        <Badge variant="outline" className={visibilityColors[project.visibility] || ""}>
          {project.visibility}
        </Badge>
      </TableCell>
      <TableCell>{project.teamCount}</TableCell>
      <TableCell>{project.taskCount}</TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {new Date(project.createdAt).toLocaleDateString()}
      </TableCell>
      <TableCell>
        <div className="flex gap-1">
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleEdit}>
            <Pencil className="h-4 w-4" />
          </Button>
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleDelete}>
            <Trash2 className="h-4 w-4 text-destructive" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  )
}
