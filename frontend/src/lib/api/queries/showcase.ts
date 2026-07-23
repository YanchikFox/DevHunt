import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

// Metrics can contain various numeric/string values
export type ShowcaseMetrics = Record<string, string | number | boolean | null>

interface ApiErrorResponse {
  response?: {
    status?: number
  }
}

export interface ShowcaseProject {
  id: string
  project: {
    projectId: string
    title: string
    description: string
  }
  summary: string | null
  demoUrl: string | null
  demoVideoUrl: string | null
  screenshots: string[]
  repositoryUrl: string | null
  metrics: ShowcaseMetrics | null
  publishedAt: string
  featured: boolean
  viewsCount: number
  likesCount: number
  updatedAt: string
  team: {
    fullName: string
    avatarUrl: string | null
    role: string
  }[]
}

export interface ShowcaseListItem {
  id: string
  projectId: string
  projectTitle: string
  summary: string | null
  demoUrl: string | null
  screenshotsJson: string | null
  repositoryUrl: string | null
  publishedAt: string
  featured: boolean
  viewsCount: number
  likesCount: number
}

export interface ShowcaseCommentAuthor {
  id: string
  fullName: string
  avatarUrl: string | null
}

export interface ShowcaseComment {
  id: string
  content: string
  createdAt: string
  updatedAt: string | null
  isEdited: boolean
  author: ShowcaseCommentAuthor
  replies?: ShowcaseComment[]
}

export interface ShowcaseCommentsResponse {
  data: ShowcaseComment[]
  totalCount: number
}

export interface CreateShowcaseDto {
  summary: string
  demoUrl?: string
  demoVideoUrl?: string
  screenshots?: string[]
  repositoryUrl?: string
  metrics?: ShowcaseMetrics
}

export interface UpdateShowcaseDto {
  summary?: string
  demoUrl?: string
  demoVideoUrl?: string
  screenshots?: string[]
  repositoryUrl?: string
  metrics?: ShowcaseMetrics
}

// ========== Showcase Queries ==========

export function useShowcase(projectId: string) {
  return useQuery({
    queryKey: ["projects", projectId, "showcase"],
    queryFn: async () => {
      try {
        const response = await apiClient.get<ShowcaseProject>(`/projects/${projectId}/showcase`)
        return response.data
      } catch (error: unknown) {
        const apiError = error as ApiErrorResponse
        if (apiError.response?.status === 404) {
          return null
        }
        throw error
      }
    },
    enabled: !!projectId,
    retry: false,
  })
}

export function useShowcaseList(options?: {
  featuredOnly?: boolean
  skip?: number
  take?: number
}) {
  const { featuredOnly = false, skip = 0, take = 20 } = options ?? {}

  return useQuery({
    queryKey: ["showcase", "list", { featuredOnly, skip, take }],
    queryFn: async () => {
      const params = new URLSearchParams()
      if (featuredOnly) params.set("featuredOnly", "true")
      if (skip > 0) params.set("skip", String(skip))
      if (take !== 20) params.set("take", String(take))
      const response = await apiClient.get<ShowcaseListItem[]>(`/showcase?${params.toString()}`)
      return response.data
    },
  })
}

export function useShowcaseComments(projectId: string, options?: { skip?: number; take?: number }) {
  const { skip = 0, take = 20 } = options ?? {}

  return useQuery({
    queryKey: ["projects", projectId, "showcase", "comments", { skip, take }],
    queryFn: async () => {
      const params = new URLSearchParams()
      if (skip > 0) params.set("skip", String(skip))
      if (take !== 20) params.set("take", String(take))
      const response = await apiClient.get<ShowcaseCommentsResponse>(
        `/projects/${projectId}/showcase/comments?${params.toString()}`
      )
      return response.data
    },
    enabled: !!projectId,
  })
}

// ========== Showcase Mutations ==========

function invalidateShowcase(queryClient: ReturnType<typeof useQueryClient>, projectId: string) {
  queryClient.invalidateQueries({ queryKey: ["projects", projectId, "showcase"] })
  queryClient.invalidateQueries({ queryKey: ["showcase", "list"] })
}

export function useCreateShowcase() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ projectId, data }: { projectId: string; data: CreateShowcaseDto }) => {
      const response = await apiClient.post<ShowcaseProject>(`/projects/${projectId}/showcase`, data)
      return response.data
    },
    onSuccess: (_, { projectId }) => invalidateShowcase(queryClient, projectId),
  })
}

export function useUpdateShowcase() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ projectId, data }: { projectId: string; data: UpdateShowcaseDto }) => {
      const response = await apiClient.put<ShowcaseProject>(`/projects/${projectId}/showcase`, data)
      return response.data
    },
    onSuccess: (_, { projectId }) => invalidateShowcase(queryClient, projectId),
  })
}

export function useDeleteShowcase() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (projectId: string) => {
      await apiClient.delete(`/projects/${projectId}/showcase`)
    },
    onSuccess: (_, projectId) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "showcase"] })
      queryClient.invalidateQueries({ queryKey: ["showcase", "list"] })
    },
  })
}

function useShowcaseLikeAction(action: "like" | "unlike") {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (projectId: string) => {
      const response = await apiClient.post<{ likesCount: number }>(`/projects/${projectId}/showcase/${action}`)
      return response.data
    },
    onSuccess: (_, projectId) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "showcase"] })
    },
  })
}

export function useLikeShowcase() { return useShowcaseLikeAction("like") }
export function useUnlikeShowcase() { return useShowcaseLikeAction("unlike") }

// ========== Comment Mutations ==========

export function useCreateShowcaseComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      content,
      parentCommentId,
    }: {
      projectId: string
      content: string
      parentCommentId?: string
    }) => {
      const response = await apiClient.post<ShowcaseComment>(
        `/projects/${projectId}/showcase/comments`,
        { content, parentCommentId }
      )
      return response.data
    },
    onSuccess: (_, { projectId }) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "showcase", "comments"] })
    },
  })
}

export function useUpdateShowcaseComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      commentId,
      content,
    }: {
      projectId: string
      commentId: string
      content: string
    }) => {
      const response = await apiClient.put<ShowcaseComment>(
        `/projects/${projectId}/showcase/comments/${commentId}`,
        { content }
      )
      return response.data
    },
    onSuccess: (_, { projectId }) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "showcase", "comments"] })
    },
  })
}

export function useDeleteShowcaseComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      commentId,
    }: {
      projectId: string
      commentId: string
    }) => {
      await apiClient.delete(`/projects/${projectId}/showcase/comments/${commentId}`)
    },
    onSuccess: (_, { projectId }) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "showcase", "comments"] })
    },
  })
}
