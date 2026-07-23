import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

/**
 * Represents a project integration with external services (GitHub, GitLab, etc.)
 */
export interface Integration {
    /** Unique identifier */
    id: string
    /** Project ID this integration belongs to */
    projectId: string
    /** Service type (github, gitlab, jira) */
    serviceType: string
    /** Whether the integration is active */
    isActive: boolean
    /** Configuration settings */
    config?: Record<string, unknown>
    /** Last synchronization timestamp */
    lastSyncAt?: string
    /** Creation timestamp */
    createdAt: string
    /** Last update timestamp */
    updatedAt?: string
}

/**
 * Request to create a new integration
 */
export interface CreateIntegrationRequest {
    /** Service type (github, gitlab, jira) */
    serviceType: string
    /** Optional configuration */
    config?: Record<string, unknown>
}

/**
 * Hook to fetch all integrations for a project
 */
export function useProjectIntegrations(projectId?: string) {
    return useQuery<Integration[]>({
        queryKey: ["integrations", projectId],
        queryFn: async () => {
            const response = await apiClient.get(`/integrations/project/${projectId}`)
            return response.data
        },
        enabled: Boolean(projectId),
    })
}

/**
 * Hook to create a new integration
 */
export function useCreateIntegration(projectId: string) {
    const queryClient = useQueryClient()

    return useMutation({
        mutationFn: async (data: CreateIntegrationRequest) => {
            const response = await apiClient.post(`/integrations/project/${projectId}`, data)
            return response.data
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["integrations", projectId] })
        },
    })
}

/**
 * Hook to sync an integration (fetch data from external service)
 */
export function useSyncIntegration(projectId: string) {
    const queryClient = useQueryClient()

    return useMutation<{ message: string; lastSyncAt: string }, Error, string>({
        mutationFn: async (integrationId: string) => {
            const response = await apiClient.post(`/integrations/${integrationId}/sync`)
            return response.data
        },
        onSuccess: () => {
            // Invalidate both integrations and tasks as sync may create new tasks
            queryClient.invalidateQueries({ queryKey: ["integrations", projectId] })
            queryClient.invalidateQueries({ queryKey: ["project-tasks", projectId] })
        },
    })
}

/**
 * Hook to toggle integration active state
 */
export function useToggleIntegration(projectId: string) {
    const queryClient = useQueryClient()

    return useMutation<Integration, Error, { integrationId: string; isActive: boolean }>({
        mutationFn: async ({ integrationId, isActive }) => {
            const response = await apiClient.patch(`/integrations/${integrationId}`, {
                isActive,
            })
            return response.data
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["integrations", projectId] })
        },
    })
}

/**
 * Hook to delete an integration
 */
export function useDeleteIntegration(projectId: string) {
    const queryClient = useQueryClient()

    return useMutation<void, Error, string>({
        mutationFn: async (integrationId: string) => {
            await apiClient.delete(`/integrations/${integrationId}`)
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["integrations", projectId] })
        },
    })
}

/**
 * Get OAuth URL for connecting an integration
 */
export function useGetOAuthUrl(projectId: string) {
    return useMutation<{ oauthUrl: string; provider: string; state: string }, Error, string>({
        mutationFn: async (serviceType: string) => {
            const response = await apiClient.get(`/integrations/oauth/${serviceType}/authorize?projectId=${projectId}`)
            return response.data
        },
    })
}

/**
 * GitHub repository info returned by the API
 */
export interface GitHubRepo {
    fullName: string
    name: string
    owner: string
    isPrivate: boolean
    description: string | null
    updatedAt: string
}

/**
 * Hook to list GitHub repositories accessible via integration's OAuth token
 */
export function useGitHubRepos(integrationId?: string) {
    return useQuery<GitHubRepo[]>({
        queryKey: ["github-repos", integrationId],
        queryFn: async () => {
            const response = await apiClient.get(`/integrations/${integrationId}/github-repos`)
            return response.data
        },
        enabled: Boolean(integrationId),
    })
}

/**
 * Hook to update integration config (e.g. set repository after OAuth)
 */
export function useUpdateIntegration(projectId: string) {
    const queryClient = useQueryClient()

    return useMutation<Integration, Error, { integrationId: string; config?: Record<string, unknown>; isActive?: boolean }>({
        mutationFn: async ({ integrationId, config, isActive }) => {
            const response = await apiClient.put(`/integrations/${integrationId}`, { config, isActive })
            return response.data
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["integrations", projectId] })
        },
    })
}
