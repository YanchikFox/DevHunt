"use client"

import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent } from "react"
import { useCreateTask, useMoveTask, useReorderTasks, useTaskAttachments, useUpdateTask, useUploadTaskAttachment, useDeleteTaskAttachment } from "@/lib/api/queries/tasks"
import { useProjectTaskHandlers } from "@/hooks/use-project-task-handlers"
import { useProjectTaskBoard } from "@/hooks/use-project-task-board"
import type { TaskAttachment } from "@/lib/api/schema"
import type { TaskColumn } from "@/lib/api/task-board-types"
import type { Task, ToastFn, TranslateFn } from "./types"

interface UseProjectDetailTasksOptions {
  projectId: string
  tasks: Task[]
  canEditTasks: boolean
  t: TranslateFn
  refetchProject: () => Promise<unknown>
  isTaskDialogOpen: boolean
  setIsTaskDialogOpen: (open: boolean) => void
  toast: ToastFn
}

export function useProjectDetailTasks({
  projectId,
  tasks,
  canEditTasks,
  t,
  refetchProject,
  isTaskDialogOpen,
  setIsTaskDialogOpen,
  toast,
}: UseProjectDetailTasksOptions) {
  const [targetColumnId, setTargetColumnId] = useState<string | null>(null)
  const [isTaskDetailPanelOpen, setIsTaskDetailPanelOpen] = useState(false)
  const [selectedTaskForPanel, setSelectedTaskForPanel] = useState<Task | null>(null)
  const [isTaskPanelCreateMode, setIsTaskPanelCreateMode] = useState(false)

  const taskHandlers = useProjectTaskHandlers({
    projectId,
    tasks: Array.isArray(tasks) ? tasks : [],
    canEditTasks,
    t,
    refetchProject,
    setIsTaskDialogOpen,
    targetColumnId,
  })

  const {
    projectTasks,
    tasksByStatus,
    form,
    isValid,
    editingTask,
    setEditingTask,
    deletingTask,
    activeTaskId,
    pendingStatusChange,
    onSubmitTask,
    confirmStatusChange,
    handleDeleteTask,
    handleEditTaskClick,
    handleDeleteTaskClick,
    handleCreateTaskClick,
    handleCloseTaskDialog,
    handleCloseDeleteTaskDialog,
    handleCancelDeleteTask,
    handleCloseStatusChangeDialog,
    isSubmitting: isTaskSubmitting,
  } = taskHandlers

  const priorityValue = form.watch("priority")
  const assigneeValue = form.watch("assigneeId")

  useEffect(() => {
    if (!isTaskDialogOpen) {
      setTargetColumnId(null)
    }
  }, [isTaskDialogOpen])

  const attachmentInputRef = useRef<HTMLInputElement | null>(null)
  const activeTaskIdForAttachments = editingTask ? editingTask.Id || editingTask.id || "" : ""
  const { data: taskAttachments, isLoading: taskAttachmentsLoading } = useTaskAttachments(
    projectId,
    editingTask ? activeTaskIdForAttachments : null
  )
  const uploadAttachment = useUploadTaskAttachment()
  const deleteAttachment = useDeleteTaskAttachment()
  const attachments: TaskAttachment[] = taskAttachments ?? []

  const taskBoardHook = useProjectTaskBoard({
    projectId,
    tasks: projectTasks,
    canEdit: canEditTasks,
  })

  const {
    columns,
    tasksByColumn,
    columnsLoading,
    isAddColumnDialogOpen,
    setIsAddColumnDialogOpen,
    editingColumn,
    setEditingColumn,
    handleCreateColumn,
    handleUpdateColumn,
    handleDeleteColumn,
    handleReorderColumns,
    isCreatingColumn,
    isUpdatingColumn,
    isDeletingColumn,
  } = taskBoardHook

  const targetColumnName = useMemo(() => {
    if (!targetColumnId) return null
    const found = columns.find((c) => c.id === targetColumnId)
    return found?.name || null
  }, [columns, targetColumnId])

  const [deletingColumn, setDeletingColumn] = useState<TaskColumn | null>(null)
  const otherColumnsForDelete = columns.filter((c) => c.id !== deletingColumn?.id)

  const moveTaskMutation = useMoveTask()
  const reorderTasksMutation = useReorderTasks()
  const updateTaskMutation = useUpdateTask()
  const createTask = useCreateTask()

  const selectedTaskId = selectedTaskForPanel ? selectedTaskForPanel.Id || selectedTaskForPanel.id || "" : ""
  const { data: panelTaskAttachments, isLoading: panelAttachmentsLoading } = useTaskAttachments(
    projectId,
    selectedTaskId || null
  )
  const panelAttachments: TaskAttachment[] = panelTaskAttachments ?? []

  const handleOpenTaskPanel = useCallback((task: Task) => {
    setSelectedTaskForPanel(task)
    setIsTaskPanelCreateMode(false)
    setIsTaskDetailPanelOpen(true)
  }, [])

  const handleOpenTaskPanelForCreate = useCallback((columnId: string) => {
    setTargetColumnId(columnId)
    setSelectedTaskForPanel(null)
    setIsTaskPanelCreateMode(true)
    setIsTaskDetailPanelOpen(true)
  }, [])

  const handleCloseTaskPanel = useCallback(() => {
    setIsTaskDetailPanelOpen(false)
    setSelectedTaskForPanel(null)
    setIsTaskPanelCreateMode(false)
    setTargetColumnId(null)
  }, [])

  const handleSaveTaskFromPanel = useCallback(async (
    taskId: string,
    updates: Partial<{
      title: string
      description: string
      priority: string
      dueDate: string
      assigneeId: string
    }>
  ) => {
    await updateTaskMutation.mutateAsync({
      taskId,
      projectId,
      updates: {
        title: updates.title,
        description: updates.description,
        priority: updates.priority as "low" | "medium" | "high" | "urgent" | undefined,
        dueDate: updates.dueDate,
        assigneeId: updates.assigneeId,
      },
    })

    if (selectedTaskForPanel) {
      setSelectedTaskForPanel((prev) => {
        if (!prev) return null
        return {
          ...prev,
          Title: updates.title ?? prev.Title,
          title: updates.title ?? prev.title,
          Description: updates.description ?? prev.Description,
          description: updates.description ?? prev.description,
          Priority: updates.priority ?? prev.Priority,
          priority: updates.priority ?? prev.priority,
          Deadline: updates.dueDate ?? prev.Deadline,
          deadline: updates.dueDate ?? prev.deadline,
          dueDate: updates.dueDate ?? prev.dueDate,
          assigneeId: updates.assigneeId ?? prev.assigneeId,
        }
      })
    }
    await refetchProject()
  }, [updateTaskMutation, projectId, selectedTaskForPanel, refetchProject])

  const handleUploadAttachmentFromPanel = useCallback(async (file: File) => {
    if (!selectedTaskId) return
    await uploadAttachment.mutateAsync({
      projectId,
      taskId: selectedTaskId,
      file,
    })
  }, [uploadAttachment, projectId, selectedTaskId])

  const handleDeleteAttachmentFromPanel = useCallback(async (attachmentId: string) => {
    if (!selectedTaskId) return
    await deleteAttachment.mutateAsync({
      projectId,
      taskId: selectedTaskId,
      attachmentId,
    })
  }, [deleteAttachment, projectId, selectedTaskId])

  const handleCreateTaskInColumn = useCallback((columnId: string) => {
    handleOpenTaskPanelForCreate(columnId)
  }, [handleOpenTaskPanelForCreate])

  const handleCreateTaskFromPanel = useCallback(async (data: {
    title: string
    description?: string
    priority: string
    dueDate?: string
    assigneeId?: string
    columnId?: string
  }) => {
    await createTask.mutateAsync({
      projectId,
      title: data.title,
      description: data.description,
      priority: data.priority as "low" | "medium" | "high" | "urgent",
      dueDate: data.dueDate,
      assigneeId: data.assigneeId,
      columnId: data.columnId,
    })
    await refetchProject()
    toast({
      title: t("common.success"),
      description: t("tasks.taskCreatedSuccess"),
    })
  }, [createTask, projectId, refetchProject, toast, t])

  const handleMoveTask = useCallback(async (taskId: string, newColumnId: string, positionInColumn?: number) => {
    await moveTaskMutation.mutateAsync({
      projectId,
      taskId,
      columnId: newColumnId,
      positionInColumn,
    })
  }, [moveTaskMutation, projectId])

  const handleReorderTasks = useCallback(async (columnId: string, orderedTaskIds: string[]) => {
    if (!orderedTaskIds.length) return
    try {
      await reorderTasksMutation.mutateAsync({
        projectId,
        updates: orderedTaskIds.map((taskId, index) => ({
          taskId,
          columnId,
          position: index,
        })),
      })
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("tasks.taskMoveFailed"),
        variant: "destructive",
      })
    }
  }, [reorderTasksMutation, projectId, toast, t])

  const handleAttachmentSelected = useCallback(async (event: ChangeEvent<HTMLInputElement>) => {
    if (!editingTask) return
    const file = event.target.files?.[0]
    if (!file) return

    try {
      await uploadAttachment.mutateAsync({
        projectId,
        taskId: editingTask.Id || editingTask.id || "",
        file,
      })
      toast({ title: t("projects.attachmentAdded"), description: file.name })
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("projects.attachmentAddFailed"),
        variant: "destructive",
      })
    } finally {
      event.target.value = ""
    }
  }, [editingTask, uploadAttachment, projectId, toast, t])

  const handleDeleteAttachmentClick = useCallback(async (attachmentId: string) => {
    if (!editingTask) return
    try {
      await deleteAttachment.mutateAsync({
        projectId,
        taskId: editingTask.Id || editingTask.id || "",
        attachmentId,
      })
      toast({ title: t("projects.attachmentRemoved") })
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("projects.attachmentRemoveFailed"),
        variant: "destructive",
      })
    }
  }, [deleteAttachment, editingTask, projectId, toast, t])

  const handleTaskDialogOpenChange = useCallback((open: boolean) => {
    if (!open) {
      setTargetColumnId(null)
      handleCloseTaskDialog()
    } else {
      setIsTaskDialogOpen(true)
    }
  }, [handleCloseTaskDialog, setIsTaskDialogOpen, setTargetColumnId])

  const handleTaskDialogCancel = useCallback(() => {
    setTargetColumnId(null)
    handleCloseTaskDialog()
  }, [handleCloseTaskDialog, setTargetColumnId])

  const handleOpenAddColumnDialog = useCallback(() => {
    setIsAddColumnDialogOpen(true)
  }, [setIsAddColumnDialogOpen])

  const handleEditColumn = useCallback((column: TaskColumn) => {
    setEditingColumn(column)
  }, [setEditingColumn])

  const handleRequestDeleteColumn = useCallback((column: TaskColumn) => {
    setDeletingColumn(column)
  }, [setDeletingColumn])

  const handleColumnDialogOpenChange = useCallback((open: boolean) => {
    if (!open) {
      setIsAddColumnDialogOpen(false)
      setEditingColumn(null)
    }
  }, [setIsAddColumnDialogOpen, setEditingColumn])

  const handleColumnSubmit = useCallback(async (data: { name: string; color?: string }) => {
    if (editingColumn) {
      await handleUpdateColumn(editingColumn.id, data)
    } else {
      await handleCreateColumn(data)
    }
  }, [editingColumn, handleCreateColumn, handleUpdateColumn])

  const handleDeleteColumnDialogOpenChange = useCallback((open: boolean) => {
    if (!open) setDeletingColumn(null)
  }, [])

  const handleConfirmDeleteColumn = useCallback(async (moveTasksTo?: string) => {
    if (deletingColumn) {
      await handleDeleteColumn(deletingColumn, moveTasksTo)
      setDeletingColumn(null)
    }
  }, [deletingColumn, handleDeleteColumn])

  return { projectTasks, tasksByStatus, form, isValid, editingTask, setEditingTask, deletingTask, activeTaskId, pendingStatusChange, onSubmitTask, confirmStatusChange, handleDeleteTask, handleEditTaskClick, handleDeleteTaskClick, handleCreateTaskClick, handleCloseTaskDialog, handleCloseDeleteTaskDialog, handleCancelDeleteTask, handleCloseStatusChangeDialog, isTaskSubmitting, priorityValue, assigneeValue, attachmentInputRef, activeTaskIdForAttachments, taskAttachmentsLoading, attachments, uploadAttachmentPending: uploadAttachment.isPending, deleteAttachmentPending: deleteAttachment.isPending, handleAttachmentSelected, handleDeleteAttachmentClick, columns, tasksByColumn, columnsLoading, isAddColumnDialogOpen, setIsAddColumnDialogOpen, editingColumn, setEditingColumn, handleCreateColumn, handleUpdateColumn, handleDeleteColumn, handleReorderColumns, isCreatingColumn, isUpdatingColumn, isDeletingColumn, targetColumnId, targetColumnName, deletingColumn, otherColumnsForDelete, isTaskDetailPanelOpen, selectedTaskForPanel, isTaskPanelCreateMode, panelAttachments, panelAttachmentsLoading, handleOpenTaskPanel, handleCloseTaskPanel, handleSaveTaskFromPanel, handleCreateTaskFromPanel, handleUploadAttachmentFromPanel, handleDeleteAttachmentFromPanel, handleCreateTaskInColumn, handleMoveTask, handleReorderTasks, handleTaskDialogOpenChange, handleTaskDialogCancel, handleOpenAddColumnDialog, handleEditColumn, handleRequestDeleteColumn, handleColumnDialogOpenChange, handleColumnSubmit, handleDeleteColumnDialogOpenChange, handleConfirmDeleteColumn }
}
