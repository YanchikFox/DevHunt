import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

export type ProjectNewsItem = {
  id: string
  projectId: string
  authorId: string
  authorName: string
  authorAvatarUrl: string
  title: string
  content: string
  visibility: "public" | "subscribers" | "members"
  isPinned: boolean
  createdAt: string
  updatedAt: string
  attachments: Array<{
    type: string
    fileId?: string
    url?: string
    fileName?: string
    contentType?: string
    downloadUrl?: string
  }>
}

export type CreateNewsRequest = {
  title: string
  content: string
  visibility: "public" | "subscribers" | "members"
  isPinned?: boolean
  attachmentIds?: string[]
  attachmentUrls?: string[]
}

export function useProjectNews(projectId: string) {
  return useQuery({
    queryKey: ["projects", projectId, "news"],
    queryFn: async () => {
      if (!projectId) return []
      const response = await apiClient.get<{ data: ProjectNewsItem[]; pagination: unknown }>(`/projects/${projectId}/news`)
      return response.data.data ?? []
    },
    enabled: Boolean(projectId),
  })
}

export function useCreateNews() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ projectId, data }: { projectId: string; data: CreateNewsRequest }) => {
      const response = await apiClient.post<ProjectNewsItem>(`/projects/${projectId}/news`, data)
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "news"] })
      // Also invalidate the main project query as it might include recent news
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
    },
  })
}

export function useDeleteNews() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ projectId, newsId }: { projectId: string; newsId: string }) => {
      await apiClient.delete(`/projects/${projectId}/news/${newsId}`)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "news"] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
      // Invalidate global feed as news appears there too
      queryClient.invalidateQueries({ queryKey: ["globalFeed"] })
    },
  })
}

export function useUpdateNews() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      newsId,
      data,
    }: {
      projectId: string
      newsId: string
      data: Partial<CreateNewsRequest>
    }) => {
      const response = await apiClient.put<ProjectNewsItem>(`/projects/${projectId}/news/${newsId}`, data)
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "news"] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["globalFeed"] })
    },
  })
}

// ── Likes ─────────────────────────────────────────

export type NewsLikeStatus = {
  isLiked: boolean
  likesCount: number
}

export function useToggleNewsLike() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ projectId, newsId }: { projectId: string; newsId: string }) => {
      const response = await apiClient.post<NewsLikeStatus>(`/projects/${projectId}/news/${newsId}/like`)
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "news"] })
      queryClient.invalidateQueries({ queryKey: ["newsLike", variables.projectId, variables.newsId] })
      queryClient.invalidateQueries({ queryKey: ["globalFeed"] })
    },
  })
}

export function useNewsLikeStatus(projectId: string, newsId: string) {
  return useQuery({
    queryKey: ["newsLike", projectId, newsId],
    queryFn: async () => {
      const response = await apiClient.get<NewsLikeStatus>(`/projects/${projectId}/news/${newsId}/like`)
      return response.data
    },
    enabled: Boolean(projectId && newsId),
  })
}

// ── Comments ──────────────────────────────────────

export type NewsComment = {
  id: string
  authorId: string
  authorName: string
  authorAvatarUrl?: string
  content: string
  createdAt: string
  updatedAt?: string
}

export function useNewsComments(projectId: string, newsId: string) {
  return useQuery({
    queryKey: ["newsComments", projectId, newsId],
    queryFn: async () => {
      const response = await apiClient.get<NewsComment[]>(`/projects/${projectId}/news/${newsId}/comments`)
      return response.data
    },
    enabled: Boolean(projectId && newsId),
  })
}

export function useCreateNewsComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ projectId, newsId, content }: { projectId: string; newsId: string; content: string }) => {
      const response = await apiClient.post<NewsComment>(`/projects/${projectId}/news/${newsId}/comments`, { content })
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["newsComments", variables.projectId, variables.newsId] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "news"] })
      queryClient.invalidateQueries({ queryKey: ["globalFeed"] })
    },
  })
}

export function useUpdateNewsComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      newsId,
      commentId,
      content,
    }: {
      projectId: string
      newsId: string
      commentId: string
      content: string
    }) => {
      const response = await apiClient.put<NewsComment>(
        `/projects/${projectId}/news/${newsId}/comments/${commentId}`,
        { content }
      )
      return response.data
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["newsComments", variables.projectId, variables.newsId] })
    },
  })
}

export function useDeleteNewsComment() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      newsId,
      commentId,
    }: {
      projectId: string
      newsId: string
      commentId: string
    }) => {
      await apiClient.delete(`/projects/${projectId}/news/${newsId}/comments/${commentId}`)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["newsComments", variables.projectId, variables.newsId] })
      queryClient.invalidateQueries({ queryKey: ["projects", variables.projectId, "news"] })
      queryClient.invalidateQueries({ queryKey: ["globalFeed"] })
    },
  })
}
