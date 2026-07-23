"use client"

import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import {
  type TaskDetailPanelProps,
  useTaskDetailState,
  TaskTitle,
  TaskTags,
  TaskDescription,
  TaskPriority,
  TaskDueDate,
  TaskAssignee,
  TaskAttachments,
  TaskDependencies,
  TaskDetailHeader,
} from "./task-detail"

// Re-export types for backward compatibility
export type { Task, TeamMember, Attachment, TaskDetailPanelProps } from "./task-detail"

/**
 * TaskDetailPanel - A slide-out panel for viewing and editing task details.
 * 
 * This component has been refactored to reduce complexity by extracting:
 * - State management into useTaskDetailState hook
 * - UI sections into separate components (TaskTitle, TaskTags, TaskDescription, etc.)
 * - Type definitions into types.ts
 */
export function TaskDetailPanel({
  task,
  isOpen,
  onClose,
  onSave,
  onCreate,
  onDelete: _onDelete,
  projectId,
  teamMembers,
  attachments,
  attachmentsLoading,
  canEdit,
  onUploadAttachment,
  isUploading,
  onDeleteAttachment,
  isCreateMode = false,
  targetColumnId,
  targetColumnName,
}: Readonly<TaskDetailPanelProps>) {
  const t = useTranslations()

  const { state, handlers, refs, taskQueries, computed } = useTaskDetailState({
    task,
    isCreateMode,
    isOpen,
    canEdit,
    projectId,
    targetColumnId,
    onSave,
    onCreate,
    onClose,
    onUploadAttachment,
  })

  if (!isOpen) return null

  return (
    <>
      {/* Backdrop — matches the Dialog overlay so sibling modals feel uniform. */}
      <button
        type="button"
        aria-label="Close panel"
        className="fixed inset-0 z-40 bg-black/55 backdrop-blur-sm transition-opacity cursor-default border-0"
        onClick={onClose}
      />

      {/* Panel — elevated surface token (same as project cards / overview
          tiles) so the panel reads as a lifted layer above the board, not a
          flat rectangle. Left edge gets a soft inner highlight in dark mode. */}
      <div
        className={cn(
          "fixed right-0 top-0 z-50 h-full w-full max-w-lg bg-bg-elevated border-l border-border",
          "shadow-[-24px_0_60px_-12px_rgb(0_0_0/0.45)]",
          "before:pointer-events-none before:absolute before:inset-y-0 before:left-0 before:w-px before:bg-gradient-to-b before:from-transparent before:via-white/10 before:to-transparent",
          "transform transition-transform duration-300 ease-out",
          isOpen ? "translate-x-0" : "translate-x-full"
        )}
      >
        {/* Header */}
        <TaskDetailHeader
          isCreateMode={isCreateMode}
          targetColumnName={targetColumnName}
          status={computed.status}
          priority={computed.priority}
          canEdit={canEdit}
          task={task}
          onClose={onClose}
          t={t}
        />

        {/* Content */}
        <div className={cn(
          "flex-1 overflow-y-auto p-4 space-y-6",
          isCreateMode ? "h-[calc(100%-120px)]" : "h-[calc(100%-57px)]"
        )}>
          {/* Title */}
          <div>
            <TaskTitle
              isCreateMode={isCreateMode}
              editingTitle={state.editingTitle}
              canEdit={canEdit}
              localTitle={state.localTitle}
              isSaving={state.isSaving}
              titleInputRef={refs.titleInputRef}
              setLocalTitle={state.setLocalTitle}
              setEditingTitle={state.setEditingTitle}
              handleSaveTitle={handlers.handleSaveTitle}
              handleTitleKeyDown={handlers.handleTitleKeyDown}
              t={t}
            />

            {/* Tags */}
            <TaskTags
              localTags={state.localTags}
              editingTags={state.editingTags}
              canEdit={canEdit}
              isSaving={state.isSaving}
              newTagInput={state.newTagInput}
              setNewTagInput={state.setNewTagInput}
              setEditingTags={state.setEditingTags}
              handleRemoveTag={handlers.handleRemoveTag}
              handleSaveTags={handlers.handleSaveTags}
              handleTagInputKeyDown={handlers.handleTagInputKeyDown}
              t={t}
            />
          </div>

          {/* Priority & Due Date Row */}
          <div className="grid grid-cols-2 gap-4">
            <TaskPriority
              priority={computed.priority}
              canEdit={canEdit}
              isSaving={state.isSaving}
              handlePriorityChange={handlers.handlePriorityChange}
              t={t}
            />

            <TaskDueDate
              dueDate={computed.dueDate}
              isOverdue={computed.isOverdue}
              canEdit={canEdit}
              isSaving={state.isSaving}
              handleDueDateChange={handlers.handleDueDateChange}
              t={t}
            />
          </div>

          {/* Assignee */}
          <TaskAssignee
            task={task}
            teamMembers={teamMembers}
            canEdit={canEdit}
            isSaving={state.isSaving}
            handleAssigneeChange={handlers.handleAssigneeChange}
            t={t}
          />

          {/* Description */}
          <TaskDescription
            task={task}
            localDescription={state.localDescription}
            editingDescription={state.editingDescription}
            canEdit={canEdit}
            isSaving={state.isSaving}
            setLocalDescription={state.setLocalDescription}
            setEditingDescription={state.setEditingDescription}
            handleSaveDescription={handlers.handleSaveDescription}
            t={t}
          />

          {/* Attachments - hidden in create mode */}
          {!isCreateMode && (
            <TaskAttachments
              projectId={projectId}
              taskId={taskQueries.taskId}
              attachments={attachments}
              attachmentsLoading={attachmentsLoading}
              canEdit={canEdit}
              isUploading={isUploading}
              fileInputRef={refs.fileInputRef}
              handleFileSelect={handlers.handleFileSelect}
              onDeleteAttachment={onDeleteAttachment}
              t={t}
            />
          )}

          {/* Dependencies Section */}
          {!isCreateMode && (
            <TaskDependencies
              projectId={projectId}
              taskId={taskQueries.taskId}
              canEdit={canEdit}
              addingDependency={state.addingDependency}
              setAddingDependency={state.setAddingDependency}
              selectedDependency={state.selectedDependency}
              setSelectedDependency={state.setSelectedDependency}
              allTasks={taskQueries.allTasks}
              links={taskQueries.links}
              createLink={taskQueries.createLink}
              deleteLink={taskQueries.deleteLink}
              t={t}
            />
          )}
        </div>

        {/* Footer - Create button for create mode */}
        {isCreateMode && (
          <div className="absolute bottom-0 left-0 right-0 border-t border-border bg-background px-4 py-3 flex items-center justify-end gap-2">
            <Button
              variant="outline"
              onClick={onClose}
              disabled={state.isSaving}
            >
              {t("common.cancel")}
            </Button>
            <Button
              onClick={handlers.handleCreateTask}
              disabled={!state.localTitle.trim() || state.isSaving}
            >
              {state.isSaving ? t("common.creating") : t("tasks.createTask")}
            </Button>
          </div>
        )}
      </div>
    </>
  )
}
