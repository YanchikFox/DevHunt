"use client"

import { useCallback } from "react"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Download, FileText, Image as ImageIcon, File, Trash2 } from "lucide-react"
import type { ProjectDetailState } from "../_hooks/useProjectDetail"

interface ProjectTaskDialogProps {
  detail: ProjectDetailState
}

function TaskAttachmentsPanel({ detail }: ProjectTaskDialogProps) {
  const { projectId, t, tasks } = detail
  const handleOpenAttachmentPicker = useCallback(() => tasks.attachmentInputRef.current?.click(), [tasks.attachmentInputRef])

  return (
    <div className="space-y-3 rounded-lg border border-border/70 p-3">
      <div className="flex items-center justify-between">
        <div>
          <Label className="text-sm font-medium">{t("taskBoard.attachments")}</Label>
          <p className="text-xs text-muted-foreground">{t("taskBoard.uploadFilesToTask")}</p>
        </div>
        <div className="flex items-center gap-2">
          <input ref={tasks.attachmentInputRef} type="file" className="hidden" onChange={tasks.handleAttachmentSelected} />
          <Button type="button" size="sm" variant="outline" disabled={tasks.uploadAttachmentPending || !detail.canEditTasks} onClick={handleOpenAttachmentPicker}>
            {tasks.uploadAttachmentPending ? t("common.uploading") : t("common.upload")}
          </Button>
        </div>
      </div>
      {tasks.taskAttachmentsLoading ? (
        <p className="text-xs text-muted-foreground">{t("loading.attachments")}</p>
      ) : tasks.attachments.length === 0 ? (
        <p className="text-xs text-muted-foreground">{t("emptyStates.noAttachmentsYet")}</p>
      ) : (
        <div className="space-y-2 max-h-40 overflow-y-auto">
          {tasks.attachments.map((att) => {
            const isImage = att.contentType?.startsWith("image/")
            const isPdf = att.contentType === "application/pdf"
            const downloadUrl = `/api/proxy-core/projects/${projectId}/tasks/${tasks.activeTaskIdForAttachments}/attachments/${att.id}`
            return (
              <div key={att.id} className="flex items-center gap-3 rounded-md border px-3 py-2 text-sm group hover:bg-muted/50 transition-colors">
                <div className="flex-shrink-0 w-8 h-8 rounded bg-muted flex items-center justify-center">
                  {isImage ? <ImageIcon className="h-4 w-4 text-primary" /> : isPdf ? <FileText className="h-4 w-4 text-red-500" /> : <File className="h-4 w-4 text-muted-foreground" />}
                </div>
                <div className="flex-1 min-w-0">
                  <a href={downloadUrl} target="_blank" rel="noopener noreferrer" className="font-medium hover:underline truncate block">{att.fileName}</a>
                  <span className="text-xs text-muted-foreground">
                    {att.attachedByUserName ? t("taskBoard.attachedBy", { name: att.attachedByUserName }) : t("taskBoard.attachment")} ·{" "}
                    {new Date(att.attachedAt).toLocaleDateString()}{att.fileSize && ` · ${(att.fileSize / 1024).toFixed(0)} KB`}
                  </span>
                </div>
                <div className="flex items-center gap-1 flex-shrink-0">
                  <Button type="button" size="sm" variant="ghost" className="h-8 w-8 p-0" asChild>
                    <a href={downloadUrl} download={att.fileName}><Download className="h-4 w-4" /></a>
                  </Button>
                  {detail.canEditTasks && (
                    <Button type="button" size="sm" variant="ghost" className="h-8 w-8 p-0 text-destructive opacity-0 group-hover:opacity-100 transition-opacity"
                      onClick={() => tasks.handleDeleteAttachmentClick(att.id)} disabled={tasks.deleteAttachmentPending}>
                      <Trash2 className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}

export function ProjectTaskDialog({ detail }: ProjectTaskDialogProps) {
  const { t, dialogs, tasks, team } = detail

  const handlePriorityChange = useCallback((value: string) => {
    tasks.form.setValue("priority", value as "low" | "medium" | "high" | "urgent", {
      shouldValidate: true,
      shouldDirty: true,
    })
  }, [tasks.form])

  const handleAssigneeChange = useCallback((value: string) => {
    const finalValue = value === "unassigned" ? undefined : value
    tasks.form.setValue("assigneeId", finalValue, { shouldValidate: true, shouldDirty: true })
  }, [tasks.form])

  return (
    <Dialog open={dialogs.isTaskDialogOpen} onOpenChange={tasks.handleTaskDialogOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{tasks.editingTask ? t("tasks.editTask") : t("tasks.create")}</DialogTitle>
          <DialogDescription>
            {tasks.targetColumnName
              ? t("tasks.column", { name: tasks.targetColumnName })
              : t("tasks.fillInDetails")}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={tasks.form.handleSubmit(tasks.onSubmitTask)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="task-title">{t("tasks.taskTitle")}</Label>
            <Input
              id="task-title"
              placeholder={t("tasks.taskTitle")}
              {...tasks.form.register("title", { required: true })}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="task-description">{t("projects.description")}</Label>
            <Textarea
              id="task-description"
              rows={3}
              placeholder={t("tasks.whatNeedsToBeDone")}
              {...tasks.form.register("description")}
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>{t("tasks.priority")}</Label>
              <Select value={tasks.priorityValue || "medium"} onValueChange={handlePriorityChange}>
                <SelectTrigger>
                  <SelectValue placeholder={t("tasks.selectPriority")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="low">{t("tasks.low")}</SelectItem>
                  <SelectItem value="medium">{t("tasks.medium")}</SelectItem>
                  <SelectItem value="high">{t("tasks.high")}</SelectItem>
                  <SelectItem value="urgent">{t("tasks.urgent")}</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="space-y-2">
              <Label htmlFor="task-due-date">{t("tasks.dueDate")}</Label>
              <Input id="task-due-date" type="date" {...tasks.form.register("dueDate")} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>{t("tasks.assignee")}</Label>
            <Select value={tasks.assigneeValue ?? "unassigned"} onValueChange={handleAssigneeChange}>
              <SelectTrigger>
                <SelectValue placeholder={t("tasks.unassigned")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="unassigned">{t("tasks.unassigned")}</SelectItem>
                {team.projectTeam.map((member) => {
                  const id = member.userId || member.UserId || member.id || member.Id
                  const name = member.fullName || member.FullName || member.email || member.Email || t("teams.member")
                  if (!id) return null
                  return (
                    <SelectItem key={id} value={id}>
                      {name}
                    </SelectItem>
                  )
                })}
              </SelectContent>
            </Select>
          </div>

          {tasks.editingTask && <TaskAttachmentsPanel detail={detail} />}

          <DialogFooter className="gap-2">
            <Button type="button" variant="outline" onClick={tasks.handleTaskDialogCancel}>
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={!tasks.isValid || tasks.isTaskSubmitting}>
              {tasks.isTaskSubmitting
                ? t("common.loading")
                : tasks.editingTask
                  ? t("common.save")
                  : t("common.create")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
