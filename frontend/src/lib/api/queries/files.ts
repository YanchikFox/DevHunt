import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

interface PaginationInfo {
  page?: number
  pageSize?: number
  total?: number
  totalPages?: number
}

export type ProjectFile = {
  id: string
  fileName: string
  description?: string
  contentType: string
  fileSize: number
  category: string
  visibility: string
  uploadedAt: string
  downloadCount: number
  downloadUrl: string
  uploadedBy: {
    id: string
    fullName: string
    avatarUrl: string
  }
}

export type UploadFileRequest = {
  file: File
  description?: string
  category?: string
}

export function useProjectGallery(projectId: string) {
  return useQuery({
    queryKey: ["projects", projectId, "gallery"],
    queryFn: async () => {
      if (!projectId) return { data: [], pagination: {} }
      const response = await apiClient.get<{
        data: ProjectFile[]
        pagination: PaginationInfo
      }>(`/projects/${projectId}/gallery`)
      return response.data
    },
    enabled: Boolean(projectId),
  })
}

export function useUploadFile() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ projectId, data }: { projectId: string; data: UploadFileRequest }) => {
      const formData = new FormData()
      formData.append("file", data.file)
      if (data.description) formData.append("Description", data.description)
      if (data.category) formData.append("Category", data.category)

      const response = await apiClient.post<ProjectFile>(`/projects/${projectId}/files`, formData, {
        headers: {
          "Content-Type": "multipart/form-data",
        },
      })
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "gallery"] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "files"] })
      // Also invalidate the main project query as it might include recent media
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
    },
  })
}

export function useDeleteFile() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ projectId, fileId }: { projectId: string; fileId: string }) => {
      await apiClient.delete(`/projects/${projectId}/files/${fileId}`)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "gallery"] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "files"] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
    },
  })
}
