import { useQuery } from "@tanstack/react-query"
import { AxiosError } from "axios"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockInternshipsApi } from "../adapters/mock"
import { apiClient } from "../client"
import type { Internship } from "../schema"

export function useInternshipsList(params?: { type?: string; isRemote?: boolean }) {
  return useQuery({
    queryKey: ["internships", "list", params],
    queryFn: async () => {
      if (USE_MOCKS) {
        return mockInternshipsApi.list(params)
      }
      try {
        const response = await apiClient.get<Internship[]>("/internships", { params })
        return response.data
      } catch (error) {
        const axiosError = error as AxiosError
        if (axiosError.response?.status === 404) {
          console.warn("[Internships] API returned 404, falling back to mock data")
          return mockInternshipsApi.list(params)
        }
        throw error
      }
    },
  })
}

export function useInternship(id: string) {
  return useQuery({
    queryKey: ["internships", id],
    queryFn: async () => {
      if (USE_MOCKS) {
        return mockInternshipsApi.getById(id)
      }
      try {
        const response = await apiClient.get<Internship>(`/internships/${id}`)
        return response.data
      } catch (error) {
        const axiosError = error as AxiosError
        if (axiosError.response?.status === 404) {
          console.warn(`[Internships] Internship ${id} not found in API, using mock data`)
          return mockInternshipsApi.getById(id)
        }
        throw error
      }
    },
    enabled: Boolean(id),
  })
}
