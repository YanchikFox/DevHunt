"use client"

import { useState, useCallback, useEffect, useMemo } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { DragStartEvent, DragEndEvent } from "@dnd-kit/core"
import { useToast } from "@/hooks/use-toast"
import { useCreateTask, useUpdateTask, useDeleteTask } from "@/lib/api/queries/tasks"

// Schema
const createTaskSchema = (t: (key: string, values?: Record<string, string | number>) => string) => z.object({
  title: z.string().min(3, t("validation.minLength", { count: 3 })),
  description: z.string().optional(),
  priority: z.enum(["low", "medium", "high", "urgent"]),
  dueDate: z.string().optional(),
  assigneeId: z.string().optional(),
})

export type TaskForm = z.infer<ReturnType<typeof createTaskSchema>>
export type TaskPriority = "low" | "medium" | "high" | "urgent"

export interface Task {
  Id?: string
  id?: string
  Title?: string
  title?: string
  Description?: string
  description?: string
  Priority?: string
  priority?: string
  Deadline?: string
  deadline?: string
  dueDate?: string
  Status?: string
  status?: string
  assigneeId?: string
  assigneeName?: string
  assigneeAvatarUrl?: string
  columnId?: string
  positionInColumn?: number
}

export interface TasksByStatus {
  todo: Task[]
  doing: Task[]
  review: Task[]
  done: Task[]
  archived: Task[]
  cancelled: Task[]
}

export interface PendingStatusChange {
  taskId: string
  taskTitle: string
  oldStatus: string
  newStatus: string
}

interface UseProjectTaskHandlersOptions {
  projectId: string
  tasks: Task[]
  canEditTasks: boolean
  t: (key: string, values?: Record<string, string | number>) => string
  refetchProject: () => Promise<unknown>
  setIsTaskDialogOpen: (open: boolean) => void
  targetColumnId?: string | null
}

export function useProjectTaskHandlers({
  projectId,
  tasks,
  canEditTasks,
  t,
  refetchProject,
  setIsTaskDialogOpen,
  targetColumnId,
}: UseProjectTaskHandlersOptions) {
  const queryClient = useQueryClient()
  const { toast } = useToast()

  // Mutations
  const createTask = useCreateTask()
  const updateTask = useUpdateTask()
  const deleteTaskMutation = useDeleteTask()

  // Local state
  const [localTasks, setLocalTasks] = useState<Task[]>([])
  const [editingTask, setEditingTask] = useState<Task | null>(null)
  const [deletingTask, setDeletingTask] = useState<Task | null>(null)
  const [activeTaskId, setActiveTaskId] = useState<string | null>(null)
  const [pendingStatusChange, setPendingStatusChange] = useState<PendingStatusChange | null>(null)

  // Form
  const form = useForm<TaskForm>({
    resolver: zodResolver(createTaskSchema(t)),
    defaultValues: {
      priority: "medium" as const,
    },
    mode: "onChange",
  })

  const { formState: { isValid }, reset } = form

  // Sync local tasks with prop
  useEffect(() => {
    if (Array.isArray(tasks)) {
      setLocalTasks((prev) => {
        if (JSON.stringify(prev) === JSON.stringify(tasks)) return prev
        return tasks
      })
    }
  }, [tasks])

  // Reset form when editingTask changes
  useEffect(() => {
    if (editingTask) {
      reset({
        title: editingTask.Title || editingTask.title || "",
        description: editingTask.Description || editingTask.description || "",
        priority: (editingTask.Priority || editingTask.priority || "medium") as TaskPriority,
        dueDate:
          editingTask.Deadline || editingTask.deadline
            ? new Date(editingTask.Deadline || editingTask.deadline || "")
              .toISOString()
              .split("T")[0]
            : "",
        assigneeId: editingTask.assigneeId,
      })
    } else {
      reset({ title: "", description: "", priority: "medium", dueDate: "", assigneeId: undefined })
    }
  }, [editingTask, reset])

  // Computed data
  const projectTasks = useMemo(() => {
    return Array.isArray(localTasks) ? localTasks : []
  }, [localTasks])

  const tasksByStatus = useMemo((): TasksByStatus => {
    const safeProjectTasks = Array.isArray(projectTasks) ? projectTasks : []
    const getStatus = (t: Task) => (t?.status || t?.Status || "").toLowerCase()

    return {
      todo: safeProjectTasks.filter((t) => getStatus(t) === "todo"),
      doing: safeProjectTasks.filter((t) => {
        const status = getStatus(t)
        return status === "in_progress" || status === "doing"
      }),
      review: safeProjectTasks.filter((t) => getStatus(t) === "review"),
      done: safeProjectTasks.filter((t) => {
        const status = getStatus(t)
        return status === "done" || status === "completed"
      }),
      archived: safeProjectTasks.filter((t) => getStatus(t) === "archived"),
      cancelled: safeProjectTasks.filter((t) => getStatus(t) === "cancelled"),
    }
  }, [projectTasks])

  // Local task status update (optimistic)
  const updateLocalTaskStatus = useCallback((taskId: string, status: string) => {
    setLocalTasks((prevTasks) =>
      prevTasks.map((task) => {
        const currentId = (task?.Id || task?.id || "").toString()
        if (currentId !== taskId) return task

        return { ...task, status, Status: status }
      })
    )
  }, [])

  // Submit task (create or update)
  const onSubmitTask = useCallback(async (data: TaskForm) => {
    if (!projectId || projectId.trim() === "") {
      toast({
        title: t("common.error"),
        description: t("tasks.projectIdMissing"),
        variant: "destructive",
      })
      return
    }

    try {
      if (editingTask) {
        await updateTask.mutateAsync({
          taskId: editingTask.Id || editingTask.id || "",
          projectId,
          updates: {
            title: data.title,
            description: data.description,
            priority: data.priority,
            dueDate: data.dueDate || undefined,
            assigneeId: data.assigneeId || undefined,
          },
        })

        toast({
          title: t("common.success"),
          description: t("tasks.taskUpdatedSuccess"),
        })
      } else {
        await createTask.mutateAsync({
          projectId,
          title: data.title,
          description: data.description,
          priority: data.priority,
          dueDate: data.dueDate || undefined,
          assigneeId: data.assigneeId || undefined,
          columnId: targetColumnId || undefined,
        })

        toast({
          title: t("common.success"),
          description: t("tasks.taskCreatedSuccess"),
        })
      }

      await new Promise((resolve) => setTimeout(resolve, 100))
      await refetchProject()

      setIsTaskDialogOpen(false)
      setEditingTask(null)
      reset({ title: "", description: "", priority: "medium", dueDate: "", assigneeId: undefined })
    } catch (error) {
      console.error("Error with task:", error)
      toast({
        title: t("common.error"),
        description:
          error instanceof Error
            ? error.message
            : editingTask ? t("tasks.taskUpdateFailed") : t("tasks.taskCreateFailed"),
        variant: "destructive",
      })
    }
  }, [projectId, editingTask, updateTask, createTask, refetchProject, reset, toast, t, setIsTaskDialogOpen, targetColumnId])

  // Drag handlers
  const handleDragStart = useCallback((event: DragStartEvent) => {
    setActiveTaskId(event.active.id as string)
  }, [])

  const handleDragEnd = useCallback((event: DragEndEvent) => {
    const { active, over } = event
    setActiveTaskId(null)

    if (!over || !canEditTasks) return

    const taskId = active.id as string
    const newStatus = over.id as string

    const task = projectTasks.find((t: Task) => (t.Id || t.id) === taskId)
    if (!task) return

    const currentStatus = (task.Status || task.status || "").toLowerCase()

    const statusMap: Record<string, string> = {
      todo: "todo",
      doing: "doing",
      review: "review",
      done: "done",
      archived: "archived",
      cancelled: "cancelled",
    }

    const mappedNewStatus = statusMap[newStatus] || newStatus
    if (currentStatus === mappedNewStatus) return

    const statusNames: Record<string, string> = {
      todo: t("tasks.todo"),
      doing: t("tasks.inProgress"),
      review: t("tasks.review"),
      done: t("tasks.done"),
      archived: t("tasks.archived"),
      cancelled: t("tasks.cancelled"),
    }

    setPendingStatusChange({
      taskId,
      taskTitle: task.Title || task.title || t("tasks.task"),
      oldStatus: statusNames[currentStatus] || currentStatus,
      newStatus: statusNames[mappedNewStatus] || mappedNewStatus,
    })
  }, [canEditTasks, projectTasks, t])

  // Confirm status change
  const confirmStatusChange = useCallback(async () => {
    if (!pendingStatusChange) return

    const { taskId, newStatus } = pendingStatusChange
    const statusMap: Record<string, string> = {
      [t("tasks.todo")]: "todo",
      [t("tasks.inProgress")]: "doing",
      [t("tasks.review")]: "review",
      [t("tasks.done")]: "done",
      [t("tasks.archived")]: "archived",
      [t("tasks.cancelled")]: "cancelled",
    }
    const mappedStatus = (statusMap[newStatus] || newStatus.toLowerCase()) as
      | "todo"
      | "doing"
      | "review"
      | "done"
      | "archived"
      | "cancelled"

    if (!projectId || projectId.trim() === "") {
      toast({
        title: t("common.error"),
        description: t("tasks.projectIdMissing"),
        variant: "destructive",
      })
      return
    }

    try {
      await updateTask.mutateAsync({
        taskId,
        projectId,
        updates: { status: mappedStatus },
      })
      updateLocalTaskStatus(taskId, mappedStatus)
      await queryClient.invalidateQueries({ queryKey: ["tasks", projectId] })

      await new Promise((resolve) => setTimeout(resolve, 100))
      await refetchProject()

      toast({
        title: t("common.success"),
        description: t("tasks.taskStatusUpdated"),
      })
      setPendingStatusChange(null)
    } catch (error) {
      console.error("Error updating task status:", error)
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("tasks.taskStatusUpdateFailed"),
        variant: "destructive",
      })
    }
  }, [pendingStatusChange, projectId, updateTask, queryClient, refetchProject, t, toast, updateLocalTaskStatus])

  const handleDeleteTask = useCallback(async () => {
    if (!deletingTask) return
    if (!projectId || projectId.trim() === "") {
      toast({
        title: t("common.error"),
        description: t("tasks.projectIdMissing"),
        variant: "destructive",
      })
      return
    }
    try {
      await deleteTaskMutation.mutateAsync({
        taskId: deletingTask.Id || deletingTask.id || "",
        projectId,
      })
      toast({
        title: t("common.success"),
        description: t("tasks.taskDeletedSuccess"),
      })
      setDeletingTask(null)
    } catch (error) {
      toast({
        title: t("common.error"),
        description: error instanceof Error ? error.message : t("tasks.taskDeleteFailed"),
        variant: "destructive",
      })
    }
  }, [deletingTask, deleteTaskMutation, projectId, t, toast])

  // UI handlers
  const handleEditTaskClick = useCallback((task: Task) => {
    setEditingTask(task)
    setIsTaskDialogOpen(true)
  }, [setIsTaskDialogOpen])

  const handleDeleteTaskClick = useCallback((task: Task) => {
    setDeletingTask(task)
  }, [])

  const handleCreateTaskClick = useCallback(() => {
    setEditingTask(null)
    setIsTaskDialogOpen(true)
  }, [setIsTaskDialogOpen])

  const handleCloseTaskDialog = useCallback(() => {
    setIsTaskDialogOpen(false)
    setEditingTask(null)
  }, [setIsTaskDialogOpen])

  const handleCloseDeleteTaskDialog = useCallback((open: boolean) => {
    if (!open) setDeletingTask(null)
  }, [])

  const handleCancelDeleteTask = useCallback(() => {
    setDeletingTask(null)
  }, [])

  const handleCloseStatusChangeDialog = useCallback((open: boolean) => {
    if (!open) setPendingStatusChange(null)
  }, [])

  return {
    // Data
    projectTasks,
    tasksByStatus,

    // Form
    form,
    isValid,

    // State
    editingTask,
    setEditingTask,
    deletingTask,
    setDeletingTask,
    activeTaskId,
    pendingStatusChange,
    setPendingStatusChange,

    // Handlers
    onSubmitTask,
    handleDragStart,
    handleDragEnd,
    confirmStatusChange,
    handleDeleteTask,
    handleEditTaskClick,
    handleDeleteTaskClick,
    handleCreateTaskClick,
    handleCloseTaskDialog,
    handleCloseDeleteTaskDialog,
    handleCancelDeleteTask,
    handleCloseStatusChangeDialog,

    // Mutation states
    isSubmitting: createTask.isPending || updateTask.isPending,
  }
}
