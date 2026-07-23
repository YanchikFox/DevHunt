"use client"

import { useState, useMemo, useDeferredValue, useCallback } from "react"
import { useTranslations } from "next-intl"
import { useProjectsList, useCreateProject, useChangeProjectStatus } from "@/lib/api/queries/projects"
import type { Project, ProjectStatus } from "@/lib/api/schema"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogDescription } from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useForm, type UseFormReturn } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { useToast } from "@/hooks/use-toast"
import { Link } from "@/i18n/routing"
import { useAuth } from "@/lib/api/queries/auth"
import {
  Search,
  Plus,
  Filter,
  Users,
  Eye,
  Sparkles,
  MoreVertical,
  Play,
  CheckCircle,
  XCircle,
  RotateCcw,
  FolderKanban,
  Zap,
  type LucideIcon,
} from "lucide-react"
import { formatDistanceToNowStrict } from "date-fns"
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import { Skeleton } from "@/components/ui/skeleton"
import { SkillsChipsTypeahead } from "@/components/profile/SkillsChipsTypeahead"
import { cn } from "@/lib/utils"

const projectSchema = z.object({
  title: z.string().min(3, "Title must be at least 3 characters"),
  description: z.string().min(10, "Description must be at least 10 characters"),
  visibility: z.enum(["public", "private"]),
})

type ProjectForm = z.infer<typeof projectSchema>
type ProjectFormReturn = UseFormReturn<ProjectForm, unknown, ProjectForm>
type ProjectActionType = "publish" | "activate" | "complete" | "archive"
type ConfirmActionState = { type: ProjectActionType | null; projectId: string | null }
type TranslateFn = (key: string, values?: Record<string, string | number>) => string

type ProjectActionHandlers = Record<ProjectActionType, (event: React.MouseEvent) => void>

type ProjectMenuAction = {
  type: ProjectActionType
  labelKey: string
  icon: LucideIcon
  destructive?: boolean
}

const ALL_STATUS_OPTION = "all"
const LOADING_SKELETONS = [1, 2, 3, 4, 5, 6]

const ACTION_STATUS_MAP: Record<ProjectActionType, "active" | "recruiting" | "completed" | "archived"> = {
  publish: "recruiting",
  activate: "active",
  complete: "completed",
  archive: "archived",
}

const ACTION_SUCCESS_KEYS: Record<ProjectActionType, string> = {
  publish: "projects.publishedSuccess",
  activate: "projects.activatedSuccess",
  complete: "projects.completedSuccess",
  archive: "projects.archivedSuccess",
}

const CONFIRM_DIALOG_COPY: Record<
  ProjectActionType,
  { titleKey: string; descriptionKey: string; actionKey: string; destructive?: boolean }
> = {
  publish: {
    titleKey: "projects.publishConfirm",
    descriptionKey: "projects.publishDescription",
    actionKey: "projects.publish",
  },
  activate: {
    titleKey: "projects.activateConfirm",
    descriptionKey: "projects.activateDescription",
    actionKey: "projects.activate",
  },
  complete: {
    titleKey: "projects.completeConfirm",
    descriptionKey: "projects.completeDescription",
    actionKey: "projects.complete",
  },
  archive: {
    titleKey: "projects.archiveConfirm",
    descriptionKey: "projects.archiveDescription",
    actionKey: "projects.archive",
    destructive: true,
  },
}

const PROJECT_MENU_ACTIONS: Record<string, ProjectMenuAction[]> = {
  draft: [
    { type: "publish", labelKey: "projects.publish", icon: Play },
    { type: "archive", labelKey: "projects.archive", icon: XCircle, destructive: true },
  ],
  recruiting: [
    { type: "activate", labelKey: "projects.activate", icon: Play },
    { type: "archive", labelKey: "projects.archive", icon: XCircle, destructive: true },
  ],
  active: [
    { type: "complete", labelKey: "projects.complete", icon: CheckCircle },
    { type: "archive", labelKey: "projects.archive", icon: XCircle, destructive: true },
  ],
  archived: [{ type: "activate", labelKey: "projects.reactivate", icon: RotateCcw }],
  cancelled: [{ type: "activate", labelKey: "projects.reactivate", icon: RotateCcw }],
}

const STATUS_LABEL_KEYS: Record<string, string> = {
  in_progress: "projects.inProgress",
  cancelled: "projects.cancelled",
  archived: "projects.archived",
}

function getProjectStatusLabelKey(status: string) {
  return STATUS_LABEL_KEYS[status] ?? `projects.${status}`
}

function getEmptyStateMessageKey(searchQuery: string, statusFilter: string) {
  if (searchQuery || statusFilter) return "projects.emptyStateWithFilters"
  return "projects.emptyStateDefault"
}

function formatProjectUpdatedRelative(value: string) {
  try {
    return formatDistanceToNowStrict(new Date(value), { addSuffix: false })
      // "2 hours" → "2h" feel (compact like mockup). Fall back to full string if regex fails.
      .replace(/^(\d+) (second|minute|hour|day|week|month|year)s?$/,
        (_m, n: string, unit: string) => `${n}${unit[0]}`)
      + " ago"
  } catch {
    return ""
  }
}

export default function ProjectsPage() {
  const {
    t,
    user,
    searchQuery,
    statusFilter,
    statusSelectValue,
    viewMode,
    projects,
    isLoading,
    isOpen,
    technologies,
    setTechnologies,
    requiredRoles,
    setRequiredRoles,
    form,
    isCreating,
    confirmAction,
    dialogCopy,
    handleSearchQueryChange,
    handleStatusChange,
    handleViewModeMine,
    handleViewModeAll,
    handleDialogOpenChange,
    handleCloseDialog,
    handleOpenDialog,
    handleCreateProject,
    handleConfirmAction,
    handleCloseConfirmDialog,
    handleOpenConfirmDialog,
  } = useProjectsPage()

  return (
    <div className="mx-auto max-w-[1280px] space-y-6 fade-in">
      <ProjectsHeader t={t} />

      {/* Filters and actions */}
      <div className="rounded-[14px] border border-border bg-card p-4 space-y-4">
        <ProjectsFilters
          t={t}
          searchQuery={searchQuery}
          statusSelectValue={statusSelectValue}
          viewMode={viewMode}
          onSearchChange={handleSearchQueryChange}
          onStatusChange={handleStatusChange}
          onViewModeMine={handleViewModeMine}
          onViewModeAll={handleViewModeAll}
        />

        <div className="w-full h-px bg-border" />

        <ProjectsQuickActions
          t={t}
          isOpen={isOpen}
          onOpenChange={handleDialogOpenChange}
          onCancel={handleCloseDialog}
          form={form}
          technologies={technologies}
          setTechnologies={setTechnologies}
          requiredRoles={requiredRoles}
          setRequiredRoles={setRequiredRoles}
          onSubmit={handleCreateProject}
          isSubmitting={isCreating}
        />
      </div>

      <ProjectsContent
        isLoading={isLoading}
        projects={projects}
        user={user}
        t={t}
        searchQuery={searchQuery}
        statusFilter={statusFilter}
        onCreateProject={handleOpenDialog}
        onAction={handleOpenConfirmDialog}
      />

      <AlertDialog open={confirmAction.type !== null} onOpenChange={handleCloseConfirmDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{dialogCopy ? t(dialogCopy.titleKey) : ""}</AlertDialogTitle>
            <AlertDialogDescription>
              {dialogCopy ? t(dialogCopy.descriptionKey) : ""}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmAction}
              className={
                dialogCopy?.destructive
                  ? "bg-destructive text-destructive-foreground hover:bg-destructive/90"
                  : ""
              }
            >
              {dialogCopy ? t(dialogCopy.actionKey) : ""}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}

function useProjectFilters() {
  const [searchQuery, setSearchQuery] = useState("")
  const [statusFilter, setStatusFilter] = useState<string>("")
  const [viewMode, setViewMode] = useState<"all" | "mine">("mine")

  const deferredSearch = useDeferredValue(searchQuery)
  const statusSelectValue = statusFilter || ALL_STATUS_OPTION

  const handleSearchQueryChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
  }, [])

  const handleStatusChange = useCallback((value: string) => {
    setStatusFilter(value === ALL_STATUS_OPTION ? "" : value)
  }, [])

  const handleViewModeMine = useCallback(() => setViewMode("mine"), [])
  const handleViewModeAll = useCallback(() => setViewMode("all"), [])

  return {
    searchQuery,
    deferredSearch,
    statusFilter,
    statusSelectValue,
    viewMode,
    handleSearchQueryChange,
    handleStatusChange,
    handleViewModeMine,
    handleViewModeAll,
  }
}

function useCreateProjectDialog(userId: string | undefined) {
  const t = useTranslations()
  const { toast } = useToast()
  const createProject = useCreateProject()

  const [isOpen, setIsOpen] = useState(false)
  const [technologies, setTechnologies] = useState<string[]>([])
  const [requiredRoles, setRequiredRoles] = useState<string[]>([])

  const form = useForm<ProjectForm>({
    resolver: zodResolver(projectSchema),
    defaultValues: { title: "", description: "", visibility: "public" },
  })

  const resetForm = useCallback(() => {
    form.reset()
    setTechnologies([])
    setRequiredRoles([])
  }, [form])

  const handleDialogOpenChange = useCallback(
    (open: boolean) => {
      setIsOpen(open)
      if (!open) resetForm()
    },
    [resetForm]
  )

  const handleCloseDialog = useCallback(() => {
    setIsOpen(false)
    resetForm()
  }, [resetForm])

  const handleOpenDialog = useCallback(() => setIsOpen(true), [])

  const handleCreateProject = useCallback(
    async (data: ProjectForm) => {
      try {
        await createProject.mutateAsync({
          title: data.title,
          description: data.description,
          technologies,
          requiredRoles,
          ownerId: userId || "",
          status: "draft",
          visibility: data.visibility,
        })
        toast({ title: t("common.success"), description: "Project created successfully" })
        setIsOpen(false)
        resetForm()
      } catch (error) {
        toast({
          title: t("common.error"),
          description: error instanceof Error ? error.message : "Failed to create project",
          variant: "destructive",
        })
      }
    },
    [createProject, requiredRoles, technologies, toast, t, userId, resetForm]
  )

  return {
    isOpen,
    technologies,
    setTechnologies,
    requiredRoles,
    setRequiredRoles,
    form,
    isCreating: createProject.isPending,
    handleDialogOpenChange,
    handleCloseDialog,
    handleOpenDialog,
    handleCreateProject,
  }
}

function useProjectStatusActions() {
  const t = useTranslations()
  const { toast } = useToast()
  const changeStatus = useChangeProjectStatus()

  const [confirmAction, setConfirmAction] = useState<ConfirmActionState>({ type: null, projectId: null })
  const dialogCopy = confirmAction.type ? CONFIRM_DIALOG_COPY[confirmAction.type] : null

  const handleCloseConfirmDialog = useCallback((open: boolean) => {
    if (!open) setConfirmAction({ type: null, projectId: null })
  }, [])

  const handleOpenConfirmDialog = useCallback((type: ProjectActionType, projectId: string) => {
    setConfirmAction({ type, projectId })
  }, [])

  const handleConfirmAction = useCallback(async () => {
    if (!confirmAction.projectId || !confirmAction.type) return
    try {
      await changeStatus.mutateAsync({
        id: confirmAction.projectId,
        status: ACTION_STATUS_MAP[confirmAction.type],
      })
      toast({
        title: t("common.success"),
        description: t(ACTION_SUCCESS_KEYS[confirmAction.type]),
      })
      setConfirmAction({ type: null, projectId: null })
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("projects.actionFailed"),
        variant: "destructive",
      })
    }
  }, [confirmAction, changeStatus, toast, t])

  return {
    confirmAction,
    dialogCopy,
    handleCloseConfirmDialog,
    handleOpenConfirmDialog,
    handleConfirmAction,
  }
}

function useProjectsPage() {
  const t = useTranslations()
  const { data: user } = useAuth()

  const filters = useProjectFilters()
  const dialog = useCreateProjectDialog(user?.id)
  const statusActions = useProjectStatusActions()

  const { data: projects, isLoading } = useProjectsList({
    status: filters.statusFilter || undefined,
    search: filters.deferredSearch || undefined,
    myProjects: filters.viewMode === "mine",
    excludeDrafts: filters.viewMode === "all",
  })

  return {
    t,
    user,
    searchQuery: filters.searchQuery,
    statusFilter: filters.statusFilter,
    statusSelectValue: filters.statusSelectValue,
    viewMode: filters.viewMode,
    projects,
    isLoading,
    isOpen: dialog.isOpen,
    technologies: dialog.technologies,
    setTechnologies: dialog.setTechnologies,
    requiredRoles: dialog.requiredRoles,
    setRequiredRoles: dialog.setRequiredRoles,
    form: dialog.form,
    isCreating: dialog.isCreating,
    confirmAction: statusActions.confirmAction,
    dialogCopy: statusActions.dialogCopy,
    handleSearchQueryChange: filters.handleSearchQueryChange,
    handleStatusChange: filters.handleStatusChange,
    handleViewModeMine: filters.handleViewModeMine,
    handleViewModeAll: filters.handleViewModeAll,
    handleDialogOpenChange: dialog.handleDialogOpenChange,
    handleCloseDialog: dialog.handleCloseDialog,
    handleOpenDialog: dialog.handleOpenDialog,
    handleCreateProject: dialog.handleCreateProject,
    handleConfirmAction: statusActions.handleConfirmAction,
    handleCloseConfirmDialog: statusActions.handleCloseConfirmDialog,
    handleOpenConfirmDialog: statusActions.handleOpenConfirmDialog,
  }
}

function ProjectsContent({
  isLoading,
  projects,
  user,
  t,
  searchQuery,
  statusFilter,
  onCreateProject,
  onAction,
}: Readonly<{
  isLoading: boolean
  projects?: Project[]
  user?: { id: string } | null
  t: TranslateFn
  searchQuery: string
  statusFilter: string
  onCreateProject: () => void
  onAction: (type: ProjectActionType, projectId: string) => void
}>) {
  if (isLoading) {
    return (
      <div className="grid gap-3.5 grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {LOADING_SKELETONS.map((value) => (
          <div key={value} className="rounded-[14px] border border-border bg-card p-4 space-y-3">
            <div className="flex items-center gap-3">
              <Skeleton className="h-9 w-9 rounded-[8px]" />
              <div className="flex-1 space-y-1.5">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-full" />
              </div>
            </div>
            <div className="flex gap-1">
              <Skeleton className="h-5 w-14 rounded-full" />
              <Skeleton className="h-5 w-12 rounded-full" />
            </div>
          </div>
        ))}
      </div>
    )
  }

  if (projects && projects.length > 0) {
    return (
      <div className="grid gap-3.5 grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {projects.map((project) => (
          <ProjectCard
            key={project.id}
            project={project}
            user={user}
            t={t}
            onAction={onAction}
          />
        ))}
      </div>
    )
  }

  const message = t(getEmptyStateMessageKey(searchQuery, statusFilter))
  const showCreate = !searchQuery && !statusFilter

  return (
    <div className="rounded-[14px] border border-dashed border-border bg-card p-12 text-center">
      <FolderKanban className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
      <h3 className="text-[15px] font-semibold text-foreground mb-1">No projects found</h3>
      <p className="text-[13px] text-muted-foreground mb-6 max-w-md mx-auto">{message}</p>
      {showCreate && (
        <Button onClick={onCreateProject} size="sm" className="h-8 text-[12px] rounded-[8px] gap-1.5">
          <Plus className="h-3.5 w-3.5" />
          Create Your First Project
        </Button>
      )}
    </div>
  )
}

function ProjectsHeader({ t }: Readonly<{ t: TranslateFn }>) {
  return (
    <div>
      <span className="caption mb-1.5 block">[Manage]</span>
      <h1 className="font-serif text-[32px] font-normal tracking-[-0.5px] leading-none text-foreground">
        {t("navigation.projects")}
      </h1>
      <p className="text-[14px] text-muted-foreground mt-2">
        Create and manage your development projects
      </p>
    </div>
  )
}

function ProjectsFilters({
  t,
  searchQuery,
  statusSelectValue,
  viewMode,
  onSearchChange,
  onStatusChange,
  onViewModeMine,
  onViewModeAll,
}: Readonly<{
  t: TranslateFn
  searchQuery: string
  statusSelectValue: string
  viewMode: "all" | "mine"
  onSearchChange: (event: React.ChangeEvent<HTMLInputElement>) => void
  onStatusChange: (value: string) => void
  onViewModeMine: () => void
  onViewModeAll: () => void
}>) {
  return (
    <div className="flex flex-col md:flex-row items-end md:items-center gap-2.5">
      <div className="relative flex-1 w-full md:w-auto min-w-[240px]">
        <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
        <Input
          id="project-search"
          type="search"
          placeholder={t("projects.searchPlaceholder")}
          value={searchQuery}
          onChange={onSearchChange}
          className="pl-8 h-9 text-[13px] rounded-[10px]"
        />
      </div>

      <div className="flex items-center gap-2 w-full md:w-auto">
        <Select value={statusSelectValue} onValueChange={onStatusChange}>
          <SelectTrigger className="w-full md:w-[150px] h-9 text-[12px] rounded-[10px]">
            <div className="flex items-center gap-1.5 text-muted-foreground">
              <Filter className="h-3 w-3" />
              <SelectValue />
            </div>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_STATUS_OPTION}>All Status</SelectItem>
            <SelectItem value="draft">{t("projects.draft")}</SelectItem>
            <SelectItem value="active">{t("projects.active")}</SelectItem>
            <SelectItem value="recruiting">{t("projects.recruiting")}</SelectItem>
            <SelectItem value="in_progress">{t("projects.inProgress")}</SelectItem>
            <SelectItem value="completed">{t("projects.completed")}</SelectItem>
          </SelectContent>
        </Select>

        <div className="flex gap-0.5 bg-secondary p-[3px] rounded-[10px]">
          <button
            type="button"
            onClick={onViewModeMine}
            className={cn(
              "flex items-center gap-1.5 px-2.5 py-[5px] rounded-[6px] text-[12px] font-medium transition-all duration-150",
              viewMode === "mine" ? "bg-card text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            )}
          >
            <Users className="h-3 w-3" />
            <span className="hidden sm:inline">{t("projects.myProjects")}</span>
          </button>
          <button
            type="button"
            onClick={onViewModeAll}
            className={cn(
              "flex items-center gap-1.5 px-2.5 py-[5px] rounded-[6px] text-[12px] font-medium transition-all duration-150",
              viewMode === "all" ? "bg-card text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground"
            )}
          >
            <Eye className="h-3 w-3" />
            <span className="hidden sm:inline">{t("projects.allProjects")}</span>
          </button>
        </div>
      </div>
    </div>
  )
}

function ProjectsQuickActions({
  t,
  isOpen,
  onOpenChange,
  onCancel,
  form,
  technologies,
  setTechnologies,
  requiredRoles,
  setRequiredRoles,
  onSubmit,
  isSubmitting,
}: Readonly<{
  t: TranslateFn
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  onCancel: () => void
  form: ProjectFormReturn
  technologies: string[]
  setTechnologies: (skills: string[]) => void
  requiredRoles: string[]
  setRequiredRoles: (skills: string[]) => void
  onSubmit: (data: ProjectForm) => Promise<void>
  isSubmitting: boolean
}>) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-4">
      <div className="flex items-center gap-2 text-[12px] text-muted-foreground">
        <Sparkles className="h-3.5 w-3.5 text-primary" />
        <span>{t("dashboard.quickActionsDescription")}</span>
      </div>

      <CreateProjectDialog
        t={t}
        isOpen={isOpen}
        onOpenChange={onOpenChange}
        onCancel={onCancel}
        form={form}
        technologies={technologies}
        setTechnologies={setTechnologies}
        requiredRoles={requiredRoles}
        setRequiredRoles={setRequiredRoles}
        onSubmit={onSubmit}
        isSubmitting={isSubmitting}
      />
    </div>
  )
}

function ProjectTitleField({
  t,
  register,
  error,
}: Readonly<{
  t: TranslateFn
  register: ReturnType<ProjectFormReturn["register"]>
  error?: string
}>) {
  return (
    <div className="space-y-2">
      <Label htmlFor="title">{t("projects.projectTitle")}</Label>
      <Input id="title" {...register} placeholder="My Awesome Project" aria-invalid={error ? "true" : "false"} />
      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

function ProjectDescriptionField({
  t,
  register,
  error,
}: Readonly<{
  t: TranslateFn
  register: ReturnType<ProjectFormReturn["register"]>
  error?: string
}>) {
  return (
    <div className="space-y-2">
      <Label htmlFor="description">{t("projects.description")}</Label>
      <textarea
        id="description"
        {...register}
        rows={4}
        className="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        placeholder="Describe your project..."
        aria-invalid={error ? "true" : "false"}
      />
      {error && (
        <p className="text-sm text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

function ProjectSkillsFields({
  t,
  technologies,
  setTechnologies,
  requiredRoles,
  setRequiredRoles,
}: Readonly<{
  t: TranslateFn
  technologies: string[]
  setTechnologies: (skills: string[]) => void
  requiredRoles: string[]
  setRequiredRoles: (skills: string[]) => void
}>) {
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <div className="space-y-2">
        <Label htmlFor="technologies">{t("projects.technologies")}</Label>
        <SkillsChipsTypeahead
          inputId="technologies"
          label={t("projects.technologies")}
          placeholder="React, TypeScript, Node.js"
          skills={technologies}
          setSkills={setTechnologies}
          showLabel={false}
          showHelpText={false}
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="requiredRoles">{t("projects.requiredRoles")}</Label>
        <SkillsChipsTypeahead
          inputId="requiredRoles"
          label={t("projects.requiredRoles")}
          placeholder="Frontend Developer, Backend Developer"
          skills={requiredRoles}
          setSkills={setRequiredRoles}
          maxSkills={20}
          enableSuggestions={false}
          showLabel={false}
          showHelpText={false}
        />
      </div>
    </div>
  )
}

function ProjectVisibilityField({
  t,
  visibility,
  onVisibilityChange,
}: Readonly<{
  t: TranslateFn
  visibility: string
  onVisibilityChange: (value: string) => void
}>) {
  return (
    <>
      <Label htmlFor="visibility">{t("projects.visibilityLabel")}</Label>
      <Select value={visibility} onValueChange={onVisibilityChange}>
        <SelectTrigger id="visibility">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="public">{t("projects.public")}</SelectItem>
          <SelectItem value="private">{t("projects.private")}</SelectItem>
        </SelectContent>
      </Select>
    </>
  )
}

function CreateProjectDialog({
  t,
  isOpen,
  onOpenChange,
  onCancel,
  form,
  technologies,
  setTechnologies,
  requiredRoles,
  setRequiredRoles,
  onSubmit,
  isSubmitting,
}: Readonly<{
  t: TranslateFn
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  onCancel: () => void
  form: ProjectFormReturn
  technologies: string[]
  setTechnologies: (skills: string[]) => void
  requiredRoles: string[]
  setRequiredRoles: (skills: string[]) => void
  onSubmit: (data: ProjectForm) => Promise<void>
  isSubmitting: boolean
}>) {
  const {
    register,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
  } = form
  const visibility = watch("visibility")

  const handleVisibilityChange = useCallback(
    (value: string) => setValue("visibility", value as "public" | "private"),
    [setValue]
  )

  const handleFormSubmit = useMemo(() => handleSubmit(onSubmit), [handleSubmit, onSubmit])

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>
        <Button size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
          <Plus className="h-3.5 w-3.5" />
          {t("projects.create")}
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("projects.create")}</DialogTitle>
          <DialogDescription>Create a new project and start building with your team</DialogDescription>
        </DialogHeader>
        <form onSubmit={handleFormSubmit} className="space-y-4">
          <ProjectTitleField t={t} register={register("title")} error={errors.title?.message} />
          <ProjectDescriptionField
            t={t}
            register={register("description")}
            error={errors.description?.message}
          />
          <ProjectSkillsFields
            t={t}
            technologies={technologies}
            setTechnologies={setTechnologies}
            requiredRoles={requiredRoles}
            setRequiredRoles={setRequiredRoles}
          />
          <ProjectVisibilityField
            t={t}
            visibility={visibility}
            onVisibilityChange={handleVisibilityChange}
          />
          <div className="flex justify-end gap-2 pt-4">
            <Button type="button" variant="outline" onClick={onCancel}>
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t("common.loading") : t("common.create")}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

interface ProjectCardProps {
  project: Project
  user?: { id: string } | null
  t: TranslateFn
  onAction: (type: ProjectActionType, projectId: string) => void
}

function ProjectCard({ project, user, t, onAction }: ProjectCardProps) {
  const handleMenuClick = useCallback((e: React.MouseEvent) => e.preventDefault(), [])

  const handlePublish = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault()
      onAction("publish", project.id)
    },
    [onAction, project.id]
  )

  const handleActivate = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault()
      onAction("activate", project.id)
    },
    [onAction, project.id]
  )

  const handleComplete = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault()
      onAction("complete", project.id)
    },
    [onAction, project.id]
  )

  const handleArchive = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault()
      onAction("archive", project.id)
    },
    [onAction, project.id]
  )

  const actionHandlers = useMemo<ProjectActionHandlers>(
    () => ({
      publish: handlePublish,
      activate: handleActivate,
      complete: handleComplete,
      archive: handleArchive,
    }),
    [handleActivate, handleArchive, handleComplete, handlePublish]
  )

  const menuActions = useMemo(() => PROJECT_MENU_ACTIONS[project.status] ?? [], [project.status])

  const hue = (project.title?.charCodeAt(0) ?? 65) * 7 % 360
  const boosts = project.boostsCount ?? 0
  const openRoles = project.openRolesCount ?? 0
  const members = project.teamSize || 1
  const tagline = project.description
  const href = `/dashboard/projects/${project.slug || project.id}`
  const updatedLabel = formatProjectUpdatedRelative(project.updatedAt || project.createdAt)

  return (
    <Link
      href={href}
      className="block h-full group"
    >
      <div className="flex h-full flex-col rounded-[14px] border border-border bg-card p-4 gap-3 hover:border-primary/30 hover:-translate-y-0.5 hover:shadow-md transition-all duration-150">
        <div className="flex items-start gap-3">
          <div
            className="h-9 w-9 rounded-[8px] flex items-center justify-center text-white font-mono font-semibold text-[14px] shrink-0"
            style={{ background: `oklch(0.72 0.12 ${hue})` }}
          >
            {project.title?.[0] ?? "?"}
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0 flex-1">
                <p className="text-[14px] font-semibold text-foreground truncate group-hover:text-primary transition-colors">
                  {project.title}
                </p>
                {project.slug && (
                  <p className="font-mono text-[10px] text-muted-foreground truncate">/{project.slug}</p>
                )}
              </div>
              <ProjectStatusBadge status={project.status} t={t} />
              {project.ownerId === user?.id && menuActions.length > 0 && (
                <ProjectCardActionsMenu
                  t={t}
                  actions={menuActions}
                  actionHandlers={actionHandlers}
                  onTriggerClick={handleMenuClick}
                />
              )}
            </div>
          </div>
        </div>

        {tagline && (
          <p className="text-[12px] text-muted-foreground leading-[1.4] line-clamp-2 min-h-[34px]">
            {tagline}
          </p>
        )}

        {project.technologies.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {project.technologies.slice(0, 3).map((tech) => (
              <span key={tech} className="chip text-[10px]">{tech}</span>
            ))}
            {project.technologies.length > 3 && (
              <span className="chip text-[10px]">+{project.technologies.length - 3}</span>
            )}
          </div>
        )}

        <div className="mt-auto pt-2.5 border-t border-border flex items-center justify-between gap-3 text-[11px] text-muted-foreground">
          <span className="flex items-center gap-1" aria-label={`${members} ${members === 1 ? "member" : "members"}`}>
            <Users className="h-3 w-3" />{members}
          </span>
          <span className="flex items-center gap-1" aria-label={`${boosts} boosts`}>
            <Zap className={cn("h-3 w-3", project.boostedByMe && "fill-current text-primary")} />
            {boosts}
          </span>
          {openRoles > 0 && (
            <span className="text-warning font-medium">{openRoles} open</span>
          )}
          {updatedLabel && (
            <span className="font-mono ml-auto">{updatedLabel}</span>
          )}
        </div>
      </div>
    </Link>
  )
}

function ProjectStatusBadge({ status, t }: Readonly<{ status: ProjectStatus; t: TranslateFn }>) {
  const STATUS_STYLES: Record<string, { bg: string; text: string }> = {
    idea:       { bg: "bg-info/15",       text: "text-info" },
    recruiting: { bg: "bg-warning/15",    text: "text-warning" },
    active:     { bg: "bg-success/15",    text: "text-success" },
    beta:       { bg: "bg-primary/10",    text: "text-primary" },
    draft:      { bg: "bg-muted",         text: "text-muted-foreground" },
    completed:  { bg: "bg-muted",         text: "text-muted-foreground" },
    archived:   { bg: "bg-muted",         text: "text-muted-foreground" },
    in_progress:{ bg: "bg-success/15",    text: "text-success" },
    cancelled:  { bg: "bg-muted",         text: "text-muted-foreground" },
  }
  const s = STATUS_STYLES[status] ?? { bg: "bg-muted", text: "text-muted-foreground" }
  return (
    <span className={cn("chip text-[10px] capitalize border-transparent", s.bg, s.text)}>
      {t(getProjectStatusLabelKey(status))}
    </span>
  )
}

function ProjectCardActionsMenu({
  t,
  actions,
  actionHandlers,
  onTriggerClick,
}: Readonly<{
  t: TranslateFn
  actions: ProjectMenuAction[]
  actionHandlers: ProjectActionHandlers
  onTriggerClick: (event: React.MouseEvent) => void
}>) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild onClick={onTriggerClick}>
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 text-muted-foreground hover:text-foreground"
          aria-label={t("projects.actionsMenu")}
        >
          <MoreVertical className="h-4 w-4" aria-hidden="true" />
          <span className="sr-only">{t("projects.actionsMenu")}</span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {actions.map((action) => (
          <DropdownMenuItem
            key={action.type}
            onClick={actionHandlers[action.type]}
            className={action.destructive ? "text-destructive" : undefined}
          >
            <action.icon className="mr-2 h-4 w-4" />
            {t(action.labelKey)}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
