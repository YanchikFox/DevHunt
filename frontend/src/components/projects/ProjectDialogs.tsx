"use client"

import { useTranslations } from "next-intl"
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
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { EditProjectForm } from "@/components/projects/EditProjectForm"
import type { Project } from "@/lib/api/schema"

type ConfirmActionType = "publish" | "activate" | "complete" | "archive" | "unarchive" | "unpublish" | null

interface Task {
  Id?: string
  id?: string
  Title?: string
  title?: string
}

interface PendingStatusChange {
  taskId: string
  taskTitle: string
  oldStatus: string
  newStatus: string
}

// EditableProject to be compatible with EditProjectForm
type EditableProject = Omit<Project, "maxTeamSize"> & { maxTeamSize?: number | null }
type ProjectType = EditableProject

interface ProjectDialogsProps {
  // Confirm Action Dialog
  confirmAction: { type: ConfirmActionType; projectId: string | null }
  onCloseConfirmAction: (open: boolean) => void
  onConfirmStatusChange: () => void
  isChangingStatus: boolean

  deletingTask: Task | null
  onCloseDeleteTask: (open: boolean) => void
  onCancelDeleteTask: () => void
  onDeleteTask: () => void

  // Edit Project Dialog
  isEditDialogOpen: boolean
  setIsEditDialogOpen: (open: boolean) => void
  project: ProjectType | null
  onSaveProject: (data: Partial<EditableProject>) => Promise<void>
  onCloseEditDialog: () => void

  isDeleteDialogOpen: boolean
  onCloseDeleteProject: (open: boolean) => void
  deleteConfirmText: string
  onDeleteConfirmTextChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  onCancelDeleteProject: () => void
  onDeleteProject: () => void
  isDeleting: boolean

  // Task Status Change Dialog
  pendingStatusChange: PendingStatusChange | null
  onCloseStatusChange: (open: boolean) => void
  onConfirmTaskStatusChange: () => void
  isUpdatingTask: boolean

  // Gallery Delete Dialog
  deletingGalleryImageId: string | null
  onCloseDeleteGalleryImage: (open: boolean) => void
  onDeleteGalleryImage: () => void
  isDeletingGalleryImage: boolean

  // News Delete Dialog
  deletingNewsId: string | null
  onCloseDeleteNews: (open: boolean) => void
  onDeleteNews: () => void
  isDeletingNews: boolean
}

function DeleteConfirmDialog({ open, onOpenChange, title, description, onConfirm, isPending, cancelLabel, deleteLabel, loadingLabel, contentClassName }: {
  open: boolean; onOpenChange: (open: boolean) => void
  title: string; description: string; onConfirm: () => void; isPending: boolean
  cancelLabel: string; deleteLabel: string; loadingLabel: string; contentClassName?: string
}) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className={contentClassName}>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction className="bg-red-600 hover:bg-red-700" onClick={onConfirm} disabled={isPending}>
            {isPending ? loadingLabel : deleteLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

export function ProjectDialogs({
  // Confirm Action
  confirmAction,
  onCloseConfirmAction,
  onConfirmStatusChange,
  isChangingStatus,

  deletingTask,
  onCloseDeleteTask,
  onCancelDeleteTask,
  onDeleteTask,

  // Edit Project
  isEditDialogOpen,
  setIsEditDialogOpen,
  project,
  onSaveProject,
  onCloseEditDialog,

  isDeleteDialogOpen,
  onCloseDeleteProject,
  deleteConfirmText,
  onDeleteConfirmTextChange,
  onCancelDeleteProject,
  onDeleteProject,
  isDeleting,

  // Task Status Change
  pendingStatusChange,
  onCloseStatusChange,
  onConfirmTaskStatusChange,
  isUpdatingTask,

  // Gallery Delete
  deletingGalleryImageId,
  onCloseDeleteGalleryImage,
  onDeleteGalleryImage,
  isDeletingGalleryImage,

  // News Delete
  deletingNewsId,
  onCloseDeleteNews,
  onDeleteNews,
  isDeletingNews,
}: ProjectDialogsProps) {
  const t = useTranslations()

  return (
    <>
      {/* Confirm Action Dialog (Project Status) */}
      <AlertDialog open={confirmAction.type !== null} onOpenChange={onCloseConfirmAction}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {confirmAction.type === "publish" && t("projects.publishConfirm")}
              {confirmAction.type === "activate" && t("projects.activateConfirm")}
              {confirmAction.type === "complete" && t("projects.completeConfirm")}
              {confirmAction.type === "archive" && t("projects.archiveConfirm")}
              {confirmAction.type === "unarchive" && t("projects.unarchiveConfirm")}
              {confirmAction.type === "unpublish" && t("projects.unpublishConfirm")}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {confirmAction.type === "publish" && t("projects.publishConfirmDesc")}
              {confirmAction.type === "activate" && t("projects.activateConfirmDesc")}
              {confirmAction.type === "complete" && t("projects.completeConfirmDesc")}
              {confirmAction.type === "archive" && t("projects.archiveConfirmDesc")}
              {confirmAction.type === "unarchive" && t("projects.unarchiveConfirmDesc")}
              {confirmAction.type === "unpublish" && t("projects.unpublishConfirmDesc")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={onConfirmStatusChange}
              disabled={isChangingStatus}
              className={
                confirmAction.type === "archive"
                  ? "bg-destructive text-destructive-foreground hover:bg-destructive/90"
                  : ""
              }
            >
              {isChangingStatus
                ? t("common.loading")
                : confirmAction.type === "publish"
                  ? t("projects.publish")
                  : confirmAction.type === "activate"
                    ? t("projects.activate")
                    : confirmAction.type === "complete"
                      ? t("projects.complete")
                      : confirmAction.type === "archive"
                        ? t("projects.archive")
                        : confirmAction.type === "unarchive"
                          ? t("projects.unarchive")
                          : confirmAction.type === "unpublish"
                            ? t("projects.unpublish")
                            : t("common.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete Task Dialog */}
      <AlertDialog open={Boolean(deletingTask)} onOpenChange={onCloseDeleteTask}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="text-destructive">{t("taskBoard.deleteTask")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("taskBoard.deleteTaskConfirm", { title: deletingTask?.Title || deletingTask?.title || "" })}
              <br />
              {t("taskBoard.deleteTaskRestore")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={onCancelDeleteTask}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              onClick={onDeleteTask}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {t("common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Edit Project Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-2xl flex flex-col p-0 gap-0 overflow-hidden">
          <DialogHeader className="px-6 pt-6 pb-4 border-b border-border shrink-0">
            <DialogTitle>{t("projects.editProject")}</DialogTitle>
            <DialogDescription>{t("projects.editProjectDescription")}</DialogDescription>
          </DialogHeader>
          <div className="overflow-y-auto px-6 py-4">
            {project && (
              <EditProjectForm
                project={project}
                onSave={onSaveProject}
                onCancel={onCloseEditDialog}
              />
            )}
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Project Dialog */}
      <AlertDialog open={isDeleteDialogOpen} onOpenChange={onCloseDeleteProject}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle className="text-destructive">{t("projects.deleteConfirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription className="space-y-3">
              <p>
                {t("projects.deleteConfirmIrreversible")}{" "}
                <span className="font-semibold text-destructive">{t("projects.irreversible")}</span>.
                {t("projects.deleteConfirmWillBeDeleted")}{" "}
                <span className="font-semibold">{t("projects.deletedForever")}</span>.
              </p>
              <p className="text-sm">
                {t("projects.enterProjectNameToConfirm")}{" "}
                <span className="font-mono font-semibold bg-muted px-1 py-0.5 rounded">
                  {project?.title}
                </span>{" "}
                {t("projects.toConfirm")}
              </p>
              <Input
                value={deleteConfirmText}
                onChange={onDeleteConfirmTextChange}
                placeholder={t("projects.enterProjectName")}
                className="mt-2"
              />
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={onCancelDeleteProject}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              disabled={deleteConfirmText !== project?.title || isDeleting}
              onClick={onDeleteProject}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {isDeleting ? t("common.loading") : t("projects.deleteProject")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Task Status Change Confirmation Dialog */}
      <AlertDialog open={pendingStatusChange !== null} onOpenChange={onCloseStatusChange}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("tasks.confirmStatusChange")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t.rich("tasks.confirmStatusChangeDesc", {
                strong: (chunks) => <strong>{chunks}</strong>,
                taskTitle: pendingStatusChange?.taskTitle ?? "",
                oldStatus: pendingStatusChange?.oldStatus ?? "",
                newStatus: pendingStatusChange?.newStatus ?? "",
              })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction onClick={onConfirmTaskStatusChange}>
              {isUpdatingTask ? t("common.loading") : t("common.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <DeleteConfirmDialog
        open={Boolean(deletingGalleryImageId)} onOpenChange={onCloseDeleteGalleryImage}
        title={t("projects.deleteImageConfirm")} description={t("projects.deleteImageConfirmDesc")}
        onConfirm={onDeleteGalleryImage} isPending={isDeletingGalleryImage}
        cancelLabel={t("common.cancel")} deleteLabel={t("common.delete")} loadingLabel={t("common.loading")}
        contentClassName="z-[200]"
      />

      <DeleteConfirmDialog
        open={Boolean(deletingNewsId)} onOpenChange={onCloseDeleteNews}
        title={t("news.deleteNewsConfirm")} description={t("news.deleteNewsConfirmDesc")}
        onConfirm={onDeleteNews} isPending={isDeletingNews}
        cancelLabel={t("common.cancel")} deleteLabel={t("common.delete")} loadingLabel={t("common.loading")}
      />
    </>
  )
}
