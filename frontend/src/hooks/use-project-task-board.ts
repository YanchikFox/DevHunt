"use client"

import { useTranslations } from "next-intl"
import { useMemo, useCallback, useState, useEffect } from "react"
import { DragStartEvent, DragEndEvent } from "@dnd-kit/core"
import { useToast } from "@/hooks/use-toast"
import { useColumns, useCreateColumn, useUpdateColumn, useDeleteColumn, useReorderColumns } from "@/lib/api/queries/columns"
import { useMoveTask } from "@/lib/api/queries/tasks"
import type { TaskColumn } from "@/lib/api/schema"

// Generic task type that works with both schema Task and local Task interfaces
interface TaskLike {
  id?: string
  Id?: string
  columnId?: string
  positionInColumn?: number
}

export interface TasksByColumn {
  [columnId: string]: TaskLike[]
}

interface UseProjectTaskBoardOptions {
  projectId: string
  tasks: TaskLike[]
  canEdit: boolean
}

export function useProjectTaskBoard({
  projectId,
  tasks,
  canEdit,
}: UseProjectTaskBoardOptions) {
  const t = useTranslations()
  const { toast } = useToast()

  const showError = useCallback((error: unknown, fallbackKey: string) => {
    toast({
      title: t("common.error"),
      description: error instanceof Error ? error.message : t(fallbackKey),
      variant: "destructive",
    })
  }, [toast, t])

  // Queries
  const { data: columns = [], isLoading: columnsLoading } = useColumns(projectId)

  // Mutations
  const createColumnMutation = useCreateColumn()
  const updateColumnMutation = useUpdateColumn()
  const deleteColumnMutation = useDeleteColumn()
  const reorderColumnsMutation = useReorderColumns()
  const moveTaskMutation = useMoveTask()
  // Local state
  const [activeTaskId, setActiveTaskId] = useState<string | null>(null)
  const [isAddColumnDialogOpen, setIsAddColumnDialogOpen] = useState(false)
  const [editingColumn, setEditingColumn] = useState<TaskColumn | null>(null)
  const [columnOrder, setColumnOrder] = useState<string[]>([])

  // Maintain a stable ordered columns list (position or last known order)
  const orderedColumns = useMemo(() => {
    if (!columns || columns.length === 0) return []

    // If we already have an order list that matches current columns, use it
    if (columnOrder.length) {
      const byId = new Map(columns.map((c) => [c.id, c]))
      return columnOrder
        .map((id) => byId.get(id))
        .filter((c): c is TaskColumn => c != null)
    }

    // Fallback: sort by position
    return [...columns].sort((a, b) => (a.position || 0) - (b.position || 0))
  }, [columns, columnOrder])

  // Sync order state when server data changes
  useEffect(() => {
    if (!columns || columns.length === 0) return
    const sorted = [...columns]
      .sort((a, b) => (a.position || 0) - (b.position || 0))
      .map((c) => c.id)
    setColumnOrder(sorted)
  }, [columns])

  // Group tasks by columnId
  const tasksByColumn = useMemo((): TasksByColumn => {
    const grouped: TasksByColumn = {}

    orderedColumns.forEach(col => {
      grouped[col.id] = []
    })

    // Also add an "uncategorized" bucket for tasks without columns
    grouped["uncategorized"] = []

    // Group tasks
    tasks.forEach(task => {
      const columnId = task.columnId || "uncategorized"
      if (!grouped[columnId]) {
        grouped[columnId] = []
      }
      grouped[columnId].push(task)
    })

    // Sort tasks by positionInColumn within each column
    Object.keys(grouped).forEach(columnId => {
      grouped[columnId].sort((a, b) => (a.positionInColumn || 0) - (b.positionInColumn || 0))
    })

    return grouped
  }, [tasks, orderedColumns])

  // Drag handlers
  const handleDragStart = useCallback((event: DragStartEvent) => {
    setActiveTaskId(event.active.id as string)
  }, [])

  const handleDragEnd = useCallback(async (event: DragEndEvent) => {
    const { active, over } = event
    setActiveTaskId(null)

    if (!over || !canEdit) return

    const taskId = active.id as string
    const targetColumnId = over.id as string

    // Find the task
    const task = tasks.find(t => (t.id || t.Id) === taskId)
    if (!task) return

    // If same column, don't do anything for now (would need position calculation)
    if (task.columnId === targetColumnId) return

    try {
      await moveTaskMutation.mutateAsync({
        projectId,
        taskId,
        columnId: targetColumnId,
      })

      // TB-15: invalidation handled by useMoveTask onSuccess callback — no need to duplicate here

      toast({
        title: t("common.success"),
        description: t("tasks.taskMovedSuccess"),
      })
    } catch (error) {
      console.error("Error moving task:", error)
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("tasks.taskMoveFailed"),
        variant: "destructive",
      })
    }
  }, [canEdit, tasks, moveTaskMutation, projectId, toast, t])

  // Column management
  const handleCreateColumn = useCallback(async (data: { name: string; color?: string }) => {
    try {
      await createColumnMutation.mutateAsync({
        projectId,
        name: data.name,
        color: data.color,
      })
      setIsAddColumnDialogOpen(false)
      toast({
        title: t("common.success"),
        description: t("tasks.columnCreatedSuccess"),
      })
    } catch (error) {
      showError(error, "tasks.columnCreateFailed")
    }
  }, [createColumnMutation, projectId, toast, t, showError])

  const handleUpdateColumn = useCallback(async (columnId: string, data: { name?: string; color?: string }) => {
    try {
      await updateColumnMutation.mutateAsync({
        projectId,
        columnId,
        updates: data,
      })
      setEditingColumn(null)
      toast({
        title: t("common.success"),
        description: t("tasks.columnUpdatedSuccess"),
      })
    } catch (error) {
      showError(error, "tasks.columnUpdateFailed")
    }
  }, [updateColumnMutation, projectId, toast, t, showError])

  const handleDeleteColumn = useCallback(async (column: TaskColumn, moveTasksTo?: string) => {
    try {
      await deleteColumnMutation.mutateAsync({
        projectId,
        columnId: column.id,
        moveTasksTo,
      })
      toast({
        title: t("common.success"),
        description: t("tasks.columnDeletedSuccess"),
      })
    } catch (error) {
      showError(error, "tasks.columnDeleteFailed")
    }
  }, [deleteColumnMutation, projectId, toast, t, showError])

  const handleReorderColumns = useCallback(async (columnIds: string[]) => {
    try {
      setColumnOrder(columnIds)
      await reorderColumnsMutation.mutateAsync({
        projectId,
        columnIds,
      })
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("tasks.columnReorderFailed"),
        variant: "destructive",
      })
    }
  }, [reorderColumnsMutation, projectId, toast, t])

  return {
    // Data
    columns: orderedColumns,
    tasksByColumn,
    columnsLoading,

    // Drag state
    activeTaskId,
    handleDragStart,
    handleDragEnd,

    // Column management
    isAddColumnDialogOpen,
    setIsAddColumnDialogOpen,
    editingColumn,
    setEditingColumn,
    handleCreateColumn,
    handleUpdateColumn,
    handleDeleteColumn,
    handleReorderColumns,

    // Mutation states
    isCreatingColumn: createColumnMutation.isPending,
    isUpdatingColumn: updateColumnMutation.isPending,
    isDeletingColumn: deleteColumnMutation.isPending,
  }
}
