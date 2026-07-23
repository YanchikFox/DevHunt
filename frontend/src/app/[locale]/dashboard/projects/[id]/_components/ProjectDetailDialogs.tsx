"use client"

import { useCallback, useMemo } from "react"
import { useTranslations } from "next-intl"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Input } from "@/components/ui/input"
import { ProjectSettingsDialog } from "@/components/projects/ProjectSettingsDialog"
import { AddNewsDialog } from "@/components/projects/AddNewsDialog"
import { UploadMediaDialog } from "@/components/projects/UploadMediaDialog"
import { GalleryLightbox } from "@/components/projects/GalleryLightbox"
import { ProjectDialogs } from "@/components/projects/ProjectDialogs"
import { ColumnDialog } from "@/components/projects/ColumnDialog"
import { DeleteColumnDialog } from "@/components/projects/DeleteColumnDialog"
import { TaskDetailPanel } from "@/components/projects/TaskDetailPanel"
import type { ProjectDetailState } from "../_hooks/useProjectDetail"

function JoinRequestDialog({ team, dialogs, t }: {
  team: ProjectDetailState["team"]
  dialogs: Pick<ProjectDetailState["dialogs"], "isRequestDialogOpen" | "setIsRequestDialogOpen">
  t: (key: string) => string
}) {
  const handleRequestMessageChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => team.setRequestMessage(e.target.value),
    [team]
  )
  const handleClose = useCallback(() => dialogs.setIsRequestDialogOpen(false), [dialogs])

  return (
    <Dialog open={dialogs.isRequestDialogOpen} onOpenChange={dialogs.setIsRequestDialogOpen}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("teams.requestToJoin")}</DialogTitle>
          <DialogDescription>{t("teams.sendRequestToJoin")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="apply-role">{t("teams.applyingFor")}</Label>
            <Input id="apply-role" value={team.requestRole} onChange={(e) => team.setRequestRole(e.target.value)} placeholder={t("teams.roleDeveloper")} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="apply-message">{t("teams.messageOptional")}</Label>
            <Input id="apply-message" value={team.requestMessage} onChange={handleRequestMessageChange} placeholder={t("teams.tellTeamWhyYouWantToJoin")} />
          </div>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="outline" onClick={handleClose}>{t("common.cancel")}</Button>
            <Button onClick={team.onSubmitJoinRequest} disabled={team.isRequesting}>
              {team.isRequesting ? t("teams.sending") : t("teams.sendRequest")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}

interface ProjectDetailDialogsProps {
  detail: ProjectDetailState
}

export function ProjectDetailDialogs({ detail }: ProjectDetailDialogsProps) {
  const t = useTranslations()
  const { projectId, project, permissions, dialogs, tasks, team, media, statusActions, canManageSettings, canEditTasks } = detail

  const handleCloseDeleteGalleryImageDialog = useCallback((open: boolean) => {
    if (!open) dialogs.setDeletingGalleryImageId(null)
  }, [dialogs])

  const handleCloseDeleteNewsDialog = useCallback((open: boolean) => {
    if (!open) dialogs.setDeletingNewsId(null)
  }, [dialogs])

  const deleteColumnDialogColumn = useMemo(() => {
    if (!tasks.deletingColumn) return null
    return {
      id: tasks.deletingColumn.id,
      name: tasks.deletingColumn.name,
      taskCount: tasks.tasksByColumn[tasks.deletingColumn.id]?.length || 0,
    }
  }, [tasks.deletingColumn, tasks.tasksByColumn])

  const deleteColumnDialogOtherColumns = useMemo(
    () => tasks.otherColumnsForDelete.map((col) => ({
      id: col.id,
      name: col.name,
      taskCount: tasks.tasksByColumn[col.id]?.length || 0,
    })),
    [tasks.otherColumnsForDelete, tasks.tasksByColumn]
  )

  const canDeleteGallery = Boolean(permissions?.canManageGallery || permissions?.canManageFiles)

  if (!project) return null

  return (
    <>
      <ProjectSettingsDialog
        open={dialogs.isSettingsDialogOpen}
        onOpenChange={dialogs.setIsSettingsDialogOpen}
        projectId={projectId}
        project={project}
        role={permissions?.role ?? null}
        canManage={canManageSettings}
        ownerId={project.ownerId}
        initialMembers={team.projectMembersForPermissions}
        onUpdated={detail.handleSettingsUpdated}
      />

      <AddNewsDialog
        open={dialogs.isAddNewsDialogOpen}
        onOpenChange={dialogs.setIsAddNewsDialogOpen}
        projectId={projectId}
      />

      <UploadMediaDialog
        open={dialogs.isUploadMediaDialogOpen}
        onOpenChange={dialogs.setIsUploadMediaDialogOpen}
        projectId={projectId}
      />

      <GalleryLightbox
        selectedImage={media.selectedGalleryImage}
        media={media.projectMedia}
        canDelete={canDeleteGallery}
        onClose={media.handleCloseGalleryLightbox}
        onNavigate={media.handleNavigateGalleryLightbox}
        onDelete={media.handleRequestDeleteGalleryImage}
      />

      <ProjectDialogs
        confirmAction={statusActions.confirmAction}
        onCloseConfirmAction={statusActions.handleCloseConfirmDialog}
        onConfirmStatusChange={statusActions.handleConfirmStatusChange}
        isChangingStatus={statusActions.isChangingStatus}
        deletingTask={tasks.deletingTask}
        onCloseDeleteTask={tasks.handleCloseDeleteTaskDialog}
        onCancelDeleteTask={tasks.handleCancelDeleteTask}
        onDeleteTask={tasks.handleDeleteTask}
        isEditDialogOpen={dialogs.isEditDialogOpen}
        setIsEditDialogOpen={dialogs.setIsEditDialogOpen}
        project={project}
        onSaveProject={detail.handleSaveProject}
        onCloseEditDialog={dialogs.handleCloseEditDialog}
        isDeleteDialogOpen={dialogs.isDeleteDialogOpen}
        onCloseDeleteProject={dialogs.handleCloseDeleteProjectDialog}
        deleteConfirmText={dialogs.deleteConfirmText}
        onDeleteConfirmTextChange={dialogs.handleDeleteConfirmTextChange}
        onCancelDeleteProject={dialogs.handleCancelDeleteProject}
        onDeleteProject={detail.handleDeleteProject}
        isDeleting={detail.deleteProjectPending}
        pendingStatusChange={tasks.pendingStatusChange}
        onCloseStatusChange={tasks.handleCloseStatusChangeDialog}
        onConfirmTaskStatusChange={tasks.confirmStatusChange}
        isUpdatingTask={tasks.isTaskSubmitting}
        deletingGalleryImageId={dialogs.deletingGalleryImageId}
        onCloseDeleteGalleryImage={handleCloseDeleteGalleryImageDialog}
        onDeleteGalleryImage={media.handleDeleteGalleryImage}
        isDeletingGalleryImage={media.deleteFilePending}
        deletingNewsId={dialogs.deletingNewsId}
        onCloseDeleteNews={handleCloseDeleteNewsDialog}
        onDeleteNews={media.handleDeleteNewsItem}
        isDeletingNews={media.deleteNewsPending}
      />

      <ColumnDialog
        open={tasks.isAddColumnDialogOpen || tasks.editingColumn !== null}
        onOpenChange={tasks.handleColumnDialogOpenChange}
        column={tasks.editingColumn}
        onSubmit={tasks.handleColumnSubmit}
        isSubmitting={tasks.isCreatingColumn || tasks.isUpdatingColumn}
      />

      <DeleteColumnDialog
        open={tasks.deletingColumn !== null}
        onOpenChange={tasks.handleDeleteColumnDialogOpenChange}
        column={deleteColumnDialogColumn}
        otherColumns={deleteColumnDialogOtherColumns}
        onConfirm={tasks.handleConfirmDeleteColumn}
        isDeleting={tasks.isDeletingColumn}
      />

      <JoinRequestDialog team={team} dialogs={dialogs} t={t} />

      <TaskDetailPanel
        task={tasks.selectedTaskForPanel}
        isOpen={tasks.isTaskDetailPanelOpen}
        onClose={tasks.handleCloseTaskPanel}
        onSave={tasks.handleSaveTaskFromPanel}
        onCreate={tasks.handleCreateTaskFromPanel}
        onDelete={tasks.handleDeleteTaskClick}
        projectId={projectId}
        teamMembers={team.projectTeam}
        attachments={tasks.panelAttachments}
        attachmentsLoading={tasks.panelAttachmentsLoading}
        canEdit={canEditTasks}
        onUploadAttachment={tasks.handleUploadAttachmentFromPanel}
        isUploading={tasks.uploadAttachmentPending}
        onDeleteAttachment={tasks.handleDeleteAttachmentFromPanel}
        isCreateMode={tasks.isTaskPanelCreateMode}
        targetColumnId={tasks.targetColumnId}
        targetColumnName={tasks.targetColumnName}
      />
    </>
  )
}
