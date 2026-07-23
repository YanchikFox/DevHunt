import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"
import type { TaskColumn } from "../schema"

type BackendColumnDto = {
  id: string
  projectId: string
  name: string
  position: number
  color: string | null
  isDefault: boolean
  isCompleted: boolean
  wipLimit: number | null
  canvasX: number | null
  canvasY: number | null
  canvasWidth: number | null
  canvasHeight: number | null
  taskCount: number
}

function mapColumn(dto: BackendColumnDto): TaskColumn {
  return {
    id: dto.id,
    projectId: dto.projectId,
    name: dto.name,
    position: dto.position,
    color: dto.color ?? undefined,
    isDefault: dto.isDefault,
    isCompleted: dto.isCompleted,
    wipLimit: dto.wipLimit ?? undefined,
    canvasX: dto.canvasX ?? undefined,
    canvasY: dto.canvasY ?? undefined,
    canvasWidth: dto.canvasWidth ?? undefined,
    canvasHeight: dto.canvasHeight ?? undefined,
    taskCount: dto.taskCount,
  }
}

/**
 * Get columns for a project
 */
export function useColumns(projectId: string) {
  return useQuery({
    queryKey: ["columns", projectId],
    queryFn: async () => {
      const response = await apiClient.get<BackendColumnDto[]>(`/projects/${projectId}/columns`)
      return response.data.map(mapColumn)
    },
    enabled: Boolean(projectId),
  })
}

/**
 * Create a new column
 */
export function useCreateColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      name: string
      color?: string
      wipLimit?: number
      position?: number
    }): Promise<TaskColumn> => {
      const response = await apiClient.post<BackendColumnDto>(`/projects/${data.projectId}/columns`, {
        name: data.name,
        color: data.color,
        wipLimit: data.wipLimit,
        position: data.position,
      })
      return mapColumn(response.data)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
    },
  })
}

/**
 * Update a column
 */
export function useUpdateColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      columnId: string
      updates: Partial<{
        name: string
        color: string
        wipLimit: number
        isCompleted: boolean
        canvasX: number
        canvasY: number
        canvasWidth: number
        canvasHeight: number
      }>
    }): Promise<TaskColumn> => {
      const response = await apiClient.put<BackendColumnDto>(
        `/projects/${data.projectId}/columns/${data.columnId}`,
        data.updates
      )
      return mapColumn(response.data)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
    },
  })
}

/**
 * Delete a column
 */
export function useDeleteColumn() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      columnId: string
      moveTasksTo?: string
    }): Promise<void> => {
      const params = data.moveTasksTo ? `?moveTasksTo=${data.moveTasksTo}` : ""
      await apiClient.delete(`/projects/${data.projectId}/columns/${data.columnId}${params}`)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["tasks", variables.projectId] })
    },
  })
}

/**
 * Reorder columns
 */
export function useReorderColumns() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      columnIds: string[]
    }): Promise<TaskColumn[]> => {
      const response = await apiClient.post<BackendColumnDto[]>(
        `/projects/${data.projectId}/columns/reorder`,
        { columnIds: data.columnIds }
      )
      return response.data.map(mapColumn)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["columns", variables.projectId] })
    },
  })
}
