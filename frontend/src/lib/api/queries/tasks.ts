import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockTasksApi } from "../adapters/mock"
import { apiClient } from "../client"
import type { Task, TaskAttachment } from "../schema"
import { mapTask } from "../adapters/backend-mappers"

/**
 * Get tasks for a project
 */
export function useTasks(projectId: string) {
  return useQuery({
    queryKey: ["tasks", projectId],
    queryFn: async () => {
      if (USE_MOCKS) {
        return await mockTasksApi.list(projectId)
      }
      // Backend endpoint: GET /api/projects/{projectId}/tasks
      type BackendTaskDto = {
        Id: string
        ProjectId: string
        Title: string
        Description: string | null
        Status: string
        Priority: string | null
        AssignedToUserId: string | null
        CreatedByUserId: string
        CreatedAt: string
        Deadline: string | null
        EstimatedHours: number | null
        ActualHours: number | null
        CompletedAt: string | null
      }
      const response = await apiClient.get<BackendTaskDto[]>(`/projects/${projectId}/tasks`)
      return response.data.map(mapTask)
    },
    enabled: Boolean(projectId),
    retry: false,
  })
}

/**
 * Create new task
 */
export function useCreateTask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: Partial<Task> & { columnId?: string }): Promise<Task> => {
      if (USE_MOCKS) {
        return await mockTasksApi.create(data)
      }
      // Backend endpoint: POST /api/projects/{projectId}/tasks
      type BackendRequest = {
        title: string
        description?: string
        priority: string
        assignedToUserId?: string
        deadline?: string
        columnId?: string
        tags?: string
      }
      type BackendResponse = {
        id: string
        projectId: string
        title: string
        description: string | null
        status: string
        priority: string | null
        assignedToUserId: string | null
        assignedToUserName: string | null
        createdByUserId: string
        createdAt: string
        deadline: string | null
        estimatedHours: number | null
        actualHours: number | null
        completedAt: string | null
        columnId: string | null
        positionInColumn: number
        tags: string | null
      }
      const response = await apiClient.post<BackendResponse>(`/projects/${data.projectId}/tasks`, {
        title: data.title,
        description: data.description,
        priority: data.priority || "medium",
        assignedToUserId: data.assigneeId,
        deadline: data.dueDate,
        columnId: data.columnId,
        tags: data.tags,
      } as BackendRequest)
      return mapTask(response.data)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projectActivity", variables.projectId] })
    },
  })
}

/**
 * Update task
 */
export function useUpdateTask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      taskId: string
      projectId: string
      updates: Partial<Task>
    }): Promise<void> => {
      if (USE_MOCKS) {
        await mockTasksApi.update(data.taskId, data.updates)
        return
      }
      // Backend endpoint: PUT /api/projects/{projectId}/tasks/{taskId}
      // After JSON camelCase fix, API now expects camelCase properties
      type BackendRequest = {
        title?: string
        description?: string
        status?: string
        priority?: string
        assignedToUserId?: string
        deadline?: string
        estimatedHours?: number
        actualHours?: number
        tags?: string
      }
      await apiClient.put(`/projects/${data.projectId}/tasks/${data.taskId}`, {
        title: data.updates.title,
        description: data.updates.description,
        status: data.updates.status,
        priority: data.updates.priority,
        assignedToUserId: data.updates.assigneeId,
        deadline: data.updates.dueDate,
        estimatedHours: data.updates.estimatedHours,
        actualHours: data.updates.actualHours,
        tags: data.updates.tags,
      } as BackendRequest)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projectActivity", variables.projectId] })
    },
  })
}

/**
 * Delete task (soft delete)
 */
export function useDeleteTask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: { taskId: string; projectId: string }): Promise<void> => {
      if (USE_MOCKS) {
        await mockTasksApi.delete(data.taskId)
        return
      }
      // Backend endpoint: DELETE /api/projects/{projectId}/tasks/{taskId}
      await apiClient.delete(`/projects/${data.projectId}/tasks/${data.taskId}`)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projectActivity", variables.projectId] })
    },
  })
}

/**
 * Reorder tasks (for drag-drop)
 */
export function useReorderTasks() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      updates: Array<{ taskId: string; columnId?: string; position: number }>
    }): Promise<{ updated: number }> => {
      const response = await apiClient.post<{ updated: number }>(
        `/projects/${data.projectId}/tasks/reorder`,
        { updates: data.updates }
      )
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projectActivity", variables.projectId] })
    },
  })
}

/**
 * Move task to a different column
 */
export function useMoveTask() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      taskId: string
      columnId: string
      positionInColumn?: number
    }): Promise<void> => {
      await apiClient.put(`/projects/${data.projectId}/tasks/${data.taskId}`, {
        columnId: data.columnId,
        positionInColumn: data.positionInColumn,
      })
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["projectActivity", variables.projectId] })
    },
  })
}

/**
 * Task attachments
 */
export function useTaskAttachments(projectId?: string, taskId?: string | null) {
  return useQuery({
    queryKey: ["task-attachments", projectId, taskId],
    queryFn: async () => {
      const response = await apiClient.get<TaskAttachment[]>(
        `/projects/${projectId}/tasks/${taskId}/attachments`
      )
      return response.data
    },
    enabled: Boolean(projectId && taskId),
    retry: false,
  })
}

export function useUploadTaskAttachment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: { projectId: string; taskId: string; file: File }): Promise<void> => {
      const formData = new FormData()
      formData.append("file", data.file)
      await apiClient.post(`/projects/${data.projectId}/tasks/${data.taskId}/attachments`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      })
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["task-attachments", variables.projectId, variables.taskId] })
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
    },
  })
}

export function useDeleteTaskAttachment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: { projectId: string; taskId: string; attachmentId: string }): Promise<void> => {
      await apiClient.delete(
        `/projects/${data.projectId}/tasks/${data.taskId}/attachments/${data.attachmentId}`
      )
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
        queryKey: ["task-attachments", variables.projectId, variables.taskId],
      })
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
    },
  })
}

/**
 * Task links (dependencies)
 */
export interface TaskLink {
  id: string
  sourceTaskId: string
  sourceTaskTitle: string
  targetTaskId: string
  targetTaskTitle: string
  linkType: string
  createdByUserId: string
  createdAt: string
}

export interface TaskLinksResponse {
  outgoing: TaskLink[]
  incoming: TaskLink[]
}

export function useTaskLinks(projectId?: string, taskId?: string | null) {
  return useQuery({
    queryKey: ["task-links", projectId, taskId],
    queryFn: async () => {
      const response = await apiClient.get<TaskLinksResponse>(
        `/projects/${projectId}/tasks/${taskId}/links`
      )
      return response.data
    },
    enabled: Boolean(projectId && taskId),
    retry: false,
  })
}

export function useCreateTaskLink() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      taskId: string
      targetTaskId: string
      linkType: string
    }): Promise<TaskLink> => {
      const response = await apiClient.post<TaskLink>(
        `/projects/${data.projectId}/tasks/${data.taskId}/links`,
        {
          targetTaskId: data.targetTaskId,
          linkType: data.linkType,
        }
      )
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["task-links", variables.projectId, variables.taskId] })
      queryClient.invalidateQueries({ queryKey: ["task-links", variables.projectId, variables.targetTaskId] })
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
    },
  })
}

export function useDeleteTaskLink() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      taskId: string
      linkId: string
    }): Promise<void> => {
      await apiClient.delete(
        `/projects/${data.projectId}/tasks/${data.taskId}/links/${data.linkId}`
      )
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["task-links", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
    },
  })
}
