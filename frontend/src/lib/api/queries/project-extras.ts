import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockProjectsApi } from "../adapters/mock"
import { apiClient } from "../client"
import type { Project } from "../schema"
import { mapProject } from "../adapters/backend-mappers"

export function useProjectSubscription(projectId: string) {
  const queryClient = useQueryClient()

  const { data: isSubscribed, isLoading } = useQuery({
    queryKey: ["projects", projectId, "subscription"],
    queryFn: async () => {
      if (USE_MOCKS) return false
      const response = await apiClient.get<{ IsSubscribed: boolean }>(
        `/projects/${projectId}/subscribe`
      )
      return response.data.IsSubscribed
    },
    enabled: !!projectId,
  })

  const subscribeMutation = useMutation({
    mutationFn: async () => {
      if (USE_MOCKS) return
      await apiClient.post(`/projects/${projectId}/subscribe`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "subscription"] })
    },
  })

  const unsubscribeMutation = useMutation({
    mutationFn: async () => {
      if (USE_MOCKS) return
      await apiClient.delete(`/projects/${projectId}/subscribe`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "subscription"] })
    },
  })

  return {
    isSubscribed,
    isLoading,
    subscribe: subscribeMutation.mutate,
    unsubscribe: unsubscribeMutation.mutate,
    isToggling: subscribeMutation.isPending || unsubscribeMutation.isPending,
  }
}

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/**
 * Resolve a URL slug (e.g. "helix") to a full project.
 * Returns `null` when the slug is unknown; useful for pretty-URL routing that
 * needs to transparently accept either a UUID or a slug.
 */
export function useProjectBySlug(slug: string | undefined) {
  return useQuery({
    queryKey: ["projects", "by-slug", slug],
    queryFn: async (): Promise<Project | null> => {
      if (!slug) return null
      if (USE_MOCKS) {
        const all = await mockProjectsApi.list()
        return all.find((p) => p.slug === slug) ?? null
      }
      try {
        const response = await apiClient.get<Record<string, unknown>>(`/projects/by-slug/${encodeURIComponent(slug)}`)
        return mapProject(response.data)
      } catch (error) {
        const status = (error as { status?: number } | null)?.status
        if (status === 404) return null
        throw error
      }
    },
    enabled: Boolean(slug) && !UUID_PATTERN.test(slug ?? ""),
    staleTime: 60 * 1000,
  })
}

/**
 * Toggle the current user's "boost" on a project (GitHub-star-style).
 * Optimistically updates cached project data so UI reflects the change instantly.
 */
export function useToggleProjectBoost() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (projectId: string): Promise<{ boosted: boolean; boostsCount: number }> => {
      if (USE_MOCKS) {
        // No-op in mock mode — just flip the local cache.
        return { boosted: true, boostsCount: 1 }
      }
      type Response = { Boosted?: boolean; boosted?: boolean; BoostsCount?: number; boostsCount?: number }
      const response = await apiClient.post<Response>(`/projects/${projectId}/toggle-boost`)
      const data = response.data ?? {}
      return {
        boosted: Boolean(data.Boosted ?? data.boosted),
        boostsCount: Number(data.BoostsCount ?? data.boostsCount ?? 0),
      }
    },
    onSuccess: (result, projectId) => {
      queryClient.setQueriesData<Project | undefined>({ queryKey: ["projects", projectId] }, (prev) =>
        prev ? { ...prev, boostedByMe: result.boosted, boostsCount: result.boostsCount } : prev,
      )
      queryClient.invalidateQueries({ queryKey: ["projects", "list"] })
    },
  })
}
