import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

function normalizeArrayResponse<T>(payload: unknown): T[] {
  if (Array.isArray(payload)) return payload as T[]

  if (payload && typeof payload === "object") {
    const obj = payload as Record<string, unknown>
    if (Array.isArray(obj.items)) return obj.items as T[]
    if (Array.isArray(obj.data)) return obj.data as T[]
  }

  return []
}

// Types
export interface Achievement {
  id: string
  code: string
  title: string
  description?: string
  iconUrl?: string
  category?: string
  points: number
}

export interface UserAchievement {
  id: string
  userId: string
  achievementId: string
  earnedAt: string
  progress?: number
  achievement: Achievement
}

export interface CreateAchievementDto {
  code: string
  title: string
  description?: string
  iconUrl?: string
  category?: string
  points: number
}

export interface UpdateAchievementDto {
  code?: string
  title?: string
  description?: string
  iconUrl?: string
  category?: string
  points?: number
}

export interface AwardBadgeRequest {
  userId: string
  achievementCode: string
  progress?: number
}

// Queries
export const useBadges = (category?: string, page = 1, pageSize = 50) => {
  return useQuery({
    queryKey: ["badges", category, page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      })
      if (category) params.append("category", category)

      const { data } = await api.get<unknown>(`/badges?${params.toString()}`)
      return normalizeArrayResponse<Achievement>(data)
    },
  })
}

export const useUserBadges = (userId: string, page = 1, pageSize = 50) => {
  return useQuery({
    queryKey: ["badges", "user", userId, page, pageSize],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      })
      const { data } = await api.get<unknown>(`/badges/user/${userId}?${params.toString()}`)
      return normalizeArrayResponse<UserAchievement>(data)
    },
  })
}

export const useMyBadges = () => {
  return useQuery({
    queryKey: ["badges", "me"],
    queryFn: async () => {
      const { data } = await api.get<unknown>("/badges/me")
      return normalizeArrayResponse<UserAchievement>(data)
    },
  })
}

// Mutations
export const useCreateBadge = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (dto: CreateAchievementDto) => {
      const { data } = await api.post<Achievement>("/badges", dto)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["badges"] })
    },
  })
}

export const useUpdateBadge = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, dto }: { id: string; dto: UpdateAchievementDto }) => {
      const { data } = await api.put<Achievement>(`/badges/${id}`, dto)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["badges"] })
    },
  })
}

export const useDeleteBadge = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => {
      await api.delete(`/badges/${id}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["badges"] })
    },
  })
}

export const useAwardBadge = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, achievementCode, progress }: AwardBadgeRequest) => {
      const params = new URLSearchParams()
      if (progress !== undefined) params.append("progress", progress.toString())
      
      await api.post(`/badges/award/${userId}/${achievementCode}?${params.toString()}`)
    },
    onSuccess: (_, { userId }) => {
      queryClient.invalidateQueries({ queryKey: ["badges", "user", userId] })
    },
  })
}
