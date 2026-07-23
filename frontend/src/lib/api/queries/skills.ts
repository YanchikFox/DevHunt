import { useQuery } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { apiClient } from "../client"

export interface SkillSuggestItem {
  id: string
  name: string
  category: string
  match: string
}

export interface SkillSuggestResponse {
  items: SkillSuggestItem[]
  count: number
}

export interface SkillResolveItem {
  raw: string
  id: string
  name: string
  category: string
  match: string
}

export interface SkillResolveResponse {
  items: SkillResolveItem[]
  count: number
}

export function useSkillSuggestions(params: {
  q: string
  category?: string
  limit?: number
}) {
  const q = params.q.trim()
  const limit = typeof params.limit === "number" ? params.limit : 8

  return useQuery<SkillSuggestResponse>({
    queryKey: ["skills", "suggest", { q, category: params.category, limit }],
    queryFn: async () => {
      if (USE_MOCKS) {
        return { items: [], count: 0 }
      }

      const response = await apiClient.get<SkillSuggestResponse>("/skills/suggest", {
        params: {
          q,
          ...(params.category ? { category: params.category } : {}),
          limit,
        },
      })

      return response.data
    },
    enabled: !USE_MOCKS && q.length > 0,
    staleTime: 1000 * 30,
  })
}

export function useSkillCategories() {
  return useQuery<string[]>({
    queryKey: ["skills", "categories"],
    queryFn: async () => {
      if (USE_MOCKS) {
        return []
      }

      const response = await apiClient.get<string[]>("/skills/categories")
      return response.data
    },
    enabled: !USE_MOCKS,
    staleTime: 1000 * 60 * 10,
  })
}

export function useResolveSkills(params: { names: string[] }) {
  const names = params.names
    .map((n) => n.trim())
    .filter(Boolean)

  // Keep the queryKey stable regardless of input order.
  const key = [...names].sort((a, b) => a.localeCompare(b))

  return useQuery<SkillResolveResponse>({
    queryKey: ["skills", "resolve", key],
    queryFn: async () => {
      if (USE_MOCKS) {
        return { items: [], count: 0 }
      }

      const response = await apiClient.get<SkillResolveResponse>("/skills/resolve", {
        params: { name: names },
      })

      return response.data
    },
    enabled: !USE_MOCKS && names.length > 0,
    staleTime: 1000 * 60 * 5,
  })
}
