import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { apiClient } from "../client"

export type UserPrivacySettings = {
  Id?: string
  UserId: string
  ProfileVisibility: string
  ShowEmail: boolean
  ShowSkills: boolean
  ShowExperience: boolean
  ShowRating: boolean
  ShowProjects: boolean
  ShowSocialLinks: boolean
  ShowAchievements: boolean
  AllowEmailSearch: boolean
  NotifyOnMessages: boolean
  NotifyOnInvitations: boolean
  ActivityVisibility: string
}

export function useMySettings() {
  return useQuery({
    queryKey: ["users", "me", "settings"],
    queryFn: async () => {
      if (USE_MOCKS) {
        return {
          UserId: "mock-user",
          ProfileVisibility: "public",
          ShowEmail: false,
          ShowSkills: true,
          ShowExperience: true,
          ShowRating: true,
          ShowProjects: true,
          ShowSocialLinks: true,
          ShowAchievements: true,
          AllowEmailSearch: false,
          NotifyOnMessages: true,
          NotifyOnInvitations: true,
          ActivityVisibility: "public",
        } as UserPrivacySettings
      }
      const response = await apiClient.get<UserPrivacySettings>("/users/me/settings")
      return response.data
    },
  })
}

/** Returns a key→boolean map of platform feature flags from the server. */
export function usePlatformFeatureFlags() {
  return useQuery({
    queryKey: ["platform", "feature-flags"],
    queryFn: async () => {
      const response = await apiClient.get<Record<string, boolean>>("/feature-flags")
      return response.data
    },
    staleTime: 30_000,
  })
}

export function useUpdateMySettings() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (settings: Partial<UserPrivacySettings>) => {
      if (USE_MOCKS) return
      await apiClient.put("/users/me/settings", settings)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["users", "me", "settings"] })
    },
  })
}
