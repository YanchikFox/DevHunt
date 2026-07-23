import { useQuery } from "@tanstack/react-query"
import { apiClient } from "../client"

/**
 * Batch-fetch online status for a list of user IDs.
 * Uses GET to avoid CSRF validation issues with background polling.
 * Polls every 15 seconds, stale after 10 seconds.
 */
export function useOnlineStatus(userIds: string[]) {
  return useQuery<Record<string, boolean>>({
    queryKey: ["onlineStatus", ...userIds.slice().sort()],
    queryFn: async () => {
      if (userIds.length === 0) return {}
      const ids = userIds.join(",")
      const response = await apiClient.get("/users/online-status", {
        params: { ids },
      })
      return response.data
    },
    enabled: userIds.length > 0,
    staleTime: 10_000,
    refetchOnWindowFocus: true,
  })
}
