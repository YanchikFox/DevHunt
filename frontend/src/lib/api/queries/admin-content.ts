import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

// Types

export interface AdminContentStats {
  projects: number
  news: number
  showcaseComments: number
  newsComments: number
  showcase: number
}

export interface AdminContentProject {
  id: string
  title: string
  status: string
  visibility: string
  featured: boolean
  showcasePublished: boolean
  createdAt: string
  updatedAt: string | null
  ownerName: string | null
  teamCount: number
  taskCount: number
}

export interface AdminContentProjectDetail {
  id: string
  title: string
  description: string
  shortDescription: string | null
  status: string
  visibility: string
  featured: boolean
  showcasePublished: boolean
  techStack: string[]
  difficultyLevel: string | null
  maxTeamSize: number | null
  createdAt: string
  updatedAt: string | null
  startDate: string | null
  endDate: string | null
  owner: { id: string; fullName: string | null; email: string } | null
  teamCount: number
  taskCount: number
  newsCount: number
}

export interface AdminNewsPost {
  id: string
  title: string
  projectId: string
  projectTitle: string | null
  authorName: string | null
  visibility: string
  createdAt: string
  likesCount: number
  commentsCount: number
}

export interface AdminComment {
  id: string
  type: "showcase" | "news"
  content: string
  authorName: string
  createdAt: string
}

export interface AdminShowcaseEntry {
  id: string
  projectId: string
  projectTitle: string | null
  publishedAt: string
  commentsCount: number
}

export interface UpdateProjectRequest {
  title?: string
  description?: string
  status?: string
  visibility?: string
  featured?: boolean
}

interface PaginatedResponse<T> {
  data: T[]
  pagination: { page: number; pageSize: number; total: number; totalPages: number }
}

// Queries

export const useAdminContentStats = () => {
  return useQuery({
    queryKey: ["admin", "content", "stats"],
    queryFn: async () => {
      const { data } = await api.get<AdminContentStats>("/admin/content/stats")
      return data
    },
  })
}

export const useAdminContentProjects = (page = 1, pageSize = 20, filters?: { status?: string; visibility?: string; featured?: boolean; search?: string }) => {
  return useQuery({
    queryKey: ["admin", "content", "projects", page, pageSize, filters],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      if (filters?.status) params.append("status", filters.status)
      if (filters?.visibility) params.append("visibility", filters.visibility)
      if (filters?.featured !== undefined) params.append("featured", filters.featured.toString())
      if (filters?.search) params.append("search", filters.search)
      const { data } = await api.get<PaginatedResponse<AdminContentProject>>(`/admin/content/projects?${params.toString()}`)
      return data
    },
  })
}

export const useAdminContentProjectDetail = (projectId: string | null) => {
  return useQuery({
    queryKey: ["admin", "content", "projects", projectId],
    queryFn: async () => {
      const { data } = await api.get<AdminContentProjectDetail>(`/admin/content/projects/${projectId}`)
      return data
    },
    enabled: Boolean(projectId),
  })
}

export const useAdminUpdateProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ projectId, ...req }: UpdateProjectRequest & { projectId: string }) => {
      await api.put(`/admin/content/projects/${projectId}`, req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "content"] })
    },
  })
}

export const useAdminDeleteProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (projectId: string) => {
      await api.delete(`/admin/content/projects/${projectId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "content"] })
    },
  })
}

export const useAdminContentNews = (page = 1, pageSize = 20, filters?: { projectId?: string; search?: string }) => {
  return useQuery({
    queryKey: ["admin", "content", "news", page, pageSize, filters],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      if (filters?.projectId) params.append("projectId", filters.projectId)
      if (filters?.search) params.append("search", filters.search)
      const { data } = await api.get<PaginatedResponse<AdminNewsPost>>(`/admin/content/news?${params.toString()}`)
      return data
    },
  })
}

export const useAdminDeleteNews = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (newsId: string) => {
      await api.delete(`/admin/content/news/${newsId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "content"] })
    },
  })
}

export const useAdminContentComments = (page = 1, pageSize = 20, filters?: { type?: string; search?: string }) => {
  return useQuery({
    queryKey: ["admin", "content", "comments", page, pageSize, filters],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      if (filters?.type) params.append("type", filters.type)
      if (filters?.search) params.append("search", filters.search)
      const { data } = await api.get<PaginatedResponse<AdminComment>>(`/admin/content/comments?${params.toString()}`)
      return data
    },
  })
}

export const useAdminDeleteComment = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ commentId, type }: { commentId: string; type: string }) => {
      await api.delete(`/admin/content/comments/${commentId}?type=${type}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "content"] })
    },
  })
}

export const useAdminContentShowcase = (page = 1, pageSize = 20) => {
  return useQuery({
    queryKey: ["admin", "content", "showcase", page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      const { data } = await api.get<PaginatedResponse<AdminShowcaseEntry>>(`/admin/content/showcase?${params.toString()}`)
      return data
    },
  })
}

export const useAdminDeleteShowcase = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (showcaseId: string) => {
      await api.delete(`/admin/content/showcase/${showcaseId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "content"] })
    },
  })
}
