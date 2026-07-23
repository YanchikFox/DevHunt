import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"
import { USE_MOCKS } from "@/lib/feature-flags"
import { useSession } from "next-auth/react"
import { mapUserProfileDto } from "./profile-mappers"

import type {
  UserProfile,
  UpdateProfileData,
  PrivacySettings,
  UserActivityFeedItem,
  PaginationMetadata,
  UserActivityFeedPage,
  UserStatsDto,
  SuggestedUser,
} from "./profile-types"

export type {
  UserProfile,
  UpdateProfileData,
  PrivacySettings,
  UserActivityFeedItem,
  PaginationMetadata,
  UserActivityFeedPage,
  UserStatsDto,
  SuggestedUser,
}

/**
 * Hook to fetch the current user's profile.
 * @returns Query result containing the user profile.
 */
export function useProfile() {
  const { status } = useSession()
  const isAuthenticated = status === "authenticated"

  return useQuery<UserProfile>({
    queryKey: ["profile", "me"],
    queryFn: async () => {
      const response = await apiClient.get("/Profile/me")
      return mapUserProfileDto(response.data as Record<string, unknown>)
    },
    enabled: !USE_MOCKS && isAuthenticated,
    staleTime: 1000 * 60 * 5, // 5 minutes
    retry: false, // Don't retry on 404 - profile might not exist yet
  })
}

/**
 * Hook to fetch a public user profile.
 * @param userId - The ID of the user.
 * @returns Query result containing the user profile.
 */
export function usePublicProfile(userId?: string) {
  return useQuery<UserProfile>({
    queryKey: ["profile", "public", userId],
    queryFn: async () => {
      const response = await apiClient.get(`/Profile/${userId}`)
      return mapUserProfileDto(response.data as Record<string, unknown>)
    },
    enabled: Boolean(userId) && !USE_MOCKS,
  })
}

/**
 * Hook to fetch a user profile by ID (admin or internal use).
 * @param userId - The ID of the user.
 * @returns Query result containing the user profile.
 */
export function useProfileByUserId(userId?: string) {
  return useQuery<UserProfile>({
    queryKey: ["profile", "byId", userId],
    queryFn: async () => {
      if (USE_MOCKS) {
        const response = await apiClient.get(`/Profile/${userId}`)
        return response.data
      }
      const response = await apiClient.get(`/users/${userId}`)
      return mapUserProfileDto(response.data)
    },
    enabled: Boolean(userId),
  })
}

/**
 * Custom hook factory for profile mutations that invalidate queries on success.
 */
function useProfileMutation<TData, TVariables>(
  mutationFn: (variables: TVariables) => Promise<TData>,
  invalidateKeys: string[][]
) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn,
    onSuccess: () => {
      invalidateKeys.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: key })
      })
    },
  })
}

/**
 * Hook to update the current user's profile.
 * @returns Mutation result for updating the profile.
 */
export function useUpdateProfile() {
  return useProfileMutation(
    async (data: UpdateProfileData) => {
      const response = await apiClient.put("/Profile/me", data)
      return response.data
    },
    [["profile", "me"]]
  )
}

/**
 * Hook to upload a new avatar for the current user.
 * @returns Mutation result for uploading the avatar.
 */
export function useUploadAvatar() {
  return useProfileMutation(
    async (file: File) => {
      const formData = new FormData()
      formData.append("file", file)

      const response = await apiClient.post("/Profile/avatar", formData, {
        headers: {
          "Content-Type": "multipart/form-data",
        },
      })
      return response.data
    },
    [["profile", "me"]]
  )
}

/**
 * Hook to delete the current user's avatar.
 * @returns Mutation result for deleting the avatar.
 */
export function useDeleteAvatar() {
  return useProfileMutation<unknown, void>(async () => {
    const response = await apiClient.delete("/Profile/avatar")
    return response.data
  }, [["profile", "me"]])
}

/**
 * Hook to fetch the current user's privacy settings.
 * @returns Query result containing the privacy settings.
 */
export function usePrivacySettings() {
  return useQuery<PrivacySettings>({
    queryKey: ["profile", "privacy"],
    queryFn: async () => {
      const response = await apiClient.get("/Profile/privacy")
      return response.data
    },
    enabled: !USE_MOCKS,
  })
}

/**
 * Hook to update the current user's privacy settings.
 * @returns Mutation result for updating the privacy settings.
 */
export function useUpdatePrivacySettings() {
  return useProfileMutation(
    async (data: Partial<PrivacySettings>) => {
      const response = await apiClient.put("/Profile/privacy", data)
      return response.data
    },
    [["profile", "privacy"]]
  )
}

/**
 * Hook to deactivate the current user's account.
 * @returns Mutation result for deactivating the account.
 */
export function useDeactivateAccount() {
  return useMutation({
    mutationFn: async (reason?: string) => {
      const response = await apiClient.post("/Profile/deactivate", { reason })
      return response.data
    },
  })
}

/**
 * Hook to activate the current user's account.
 * @returns Mutation result for activating the account.
 */
export function useActivateAccount() {
  return useMutation({
    mutationFn: async () => {
      const response = await apiClient.post("/Profile/activate")
      return response.data
    },
  })
}

/**
 * Hook to fetch the user activity feed.
 * @param userId - The ID of the user.
 * @param visibility - The visibility level (e.g., public, followers).
 * @param enabled - Whether the query is enabled.
 * @param pageSize - The number of items per page.
 * @returns Infinite query result containing the activity feed.
 */
export function useUserActivityFeed(
  userId?: string,
  visibility?: string,
  enabled = true,
  pageSize = 8
) {
  // P2-11: no TData override — default InfiniteData<UserActivityFeedPage> is correct
  return useInfiniteQuery<UserActivityFeedPage, unknown>({
    queryKey: ["userActivityFeed", userId, visibility],
    queryFn: async ({ pageParam = 1 }) => {
      const response = await apiClient.get(`/users/${userId}/activities`, {
        params: { page: pageParam, pageSize, visibility },
      })

      return response.data
    },
    initialPageParam: 1,
    enabled: Boolean(userId && visibility && enabled),
    getNextPageParam: (lastPage) => {
      if (lastPage.pagination.hasNext) {
        return lastPage.pagination.page + 1
      }
      return undefined
    },
  })
}

/**
 * Hook to fetch the global activity feed.
 * @param pageSize - The number of items per page.
 * @param enabled - Whether the query is enabled.
 * @returns Infinite query result containing the global feed.
 */
export function useGlobalFeed(pageSize = 8, enabled = true) {
  // P2-11: Remove incorrect TData type override — default InfiniteData<UserActivityFeedPage> is correct
  return useInfiniteQuery<UserActivityFeedPage, unknown>({
    queryKey: ["globalFeed", pageSize],
    queryFn: async ({ pageParam = 1 }) => {
      const response = await apiClient.get("/feed", {
        params: { page: pageParam, limit: pageSize },
      })

      return response.data
    },
    initialPageParam: 1,
    staleTime: 1000 * 60 * 2,
    enabled,
    getNextPageParam: (lastPage) => {
      if (lastPage.pagination.hasNext) {
        return lastPage.pagination.page + 1
      }
      return undefined
    },
  })
}

/**
 * Hook to fetch user statistics.
 * @param userId - The ID of the user.
 * @param enabled - Whether the query is enabled.
 * @returns Query result containing the user statistics.
 */
export function useUserStats(userId?: string, enabled = true) {
  return useQuery<UserStatsDto>({
    queryKey: ["userStats", userId],
    queryFn: async () => {
      const response = await apiClient.get(`/users/${userId}/stats`)
      return response.data
    },
    enabled: Boolean(userId) && enabled,
  })
}

/**
 * Hook to fetch suggested users to follow.
 * @param limit - The maximum number of suggestions to return.
 * @param enabled - Whether the query is enabled.
 * @returns Query result containing the list of suggested users.
 */
export function useSuggestedUsers(limit = 6, enabled = true) {
  return useQuery<SuggestedUser[]>({
    queryKey: ["suggestedUsers", limit],
    queryFn: async () => {
      const response = await apiClient.get("/users/suggested", {
        params: { limit },
      })
      return response.data
    },
    enabled,
    staleTime: 1000 * 60 * 5,
  })
}
