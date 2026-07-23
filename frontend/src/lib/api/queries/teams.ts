import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockTeamsApi, mockInvitationsApi } from "../adapters/mock"
import { apiClient } from "../client"
import type { Invitation } from "../schema"
import { mapTeamMember, mapInvitation } from "../adapters/backend-mappers"

/**
 * Search users for team invitations
 */
export function useSearchUsers(
  params?: {
    skills?: string[]
    experienceLevel?: string
    timezone?: string
  },
  options?: { enabled?: boolean },
) {
  return useQuery({
    queryKey: ["teams", "search", params],
    enabled: options?.enabled !== false,
    queryFn: async () => {
      if (USE_MOCKS) {
        return await mockTeamsApi.search(params || {})
      }
      // Backend endpoint: GET /api/users/search
      type BackendUserDto = {
        id: string
        email?: string
        fullName: string | null
        avatarUrl: string | null
        bio: string | null
        skills: string[] | null
        experience: number | null
        rating: number | null
        timezone: string | null
      }
      type BackendResponse = {
        data: BackendUserDto[]
        pagination: {
          page: number
          pageSize: number
          total: number
          totalPages: number
          hasNext: boolean
          hasPrevious: boolean
        }
      }
      const response = await apiClient.get<BackendResponse>("/users/search", {
        params: {
          ...(params?.skills && params.skills.length > 0 && { skills: params.skills.join(",") }),
          ...(params?.experienceLevel && { experienceLevel: params.experienceLevel }),
          ...(params?.timezone && { timezone: params.timezone }),
        },
      })
      return response.data.data.map((dto) => ({
        id: dto.id,
        email: dto.email || "",
        name: dto.fullName || dto.email || "Unknown User",
        role: "participant" as const,
        avatar: dto.avatarUrl || undefined,
        bio: dto.bio || undefined,
        skills: dto.skills || [],
        experienceLevel: undefined, // Not provided by API
        timezone: dto.timezone || undefined,
        language: "pl" as const, // Default language
        createdAt: new Date().toISOString(), // Not provided by API
        updatedAt: new Date().toISOString(), // Not provided by API
      }))
    },
  })
}

/**
 * Get team members for a project
 */
export function useTeamMembers(
  projectId: string,
  options?: {
    includePermissions?: boolean
  }
) {
  const includePermissions = Boolean(options?.includePermissions)
  return useQuery({
    queryKey: ["teams", "members", projectId, { includePermissions }],
    queryFn: async () => {
      if (USE_MOCKS) {
        return await mockTeamsApi.getMembers(projectId)
      }
      // Backend endpoint: GET /api/projects/{projectId}/team
      type BackendTeamMemberDto = {
        Id: string
        UserId: string
        Name: string | null
        Role: string
        IsLeader: boolean
        Avatar: string | null
        Status: string
        CanPublishNews?: boolean
        CanManageTasks?: boolean
        CanManageFiles?: boolean
        CanManageGallery?: boolean
      }
      const response = await apiClient.get<BackendTeamMemberDto[]>(`/projects/${projectId}/team`, {
        params: includePermissions ? { includePermissions: true } : undefined,
      })
      return response.data.map((dto) => mapTeamMember(dto, projectId))
    },
    enabled: Boolean(projectId),
    retry: false,
  })
}

/**
 * Send invitation or request
 */
export function useSendInvitation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: {
      projectId: string
      role: string
      type: "invite" | "request"
      message?: string
      userId?: string
      username?: string
    }): Promise<Invitation> => {
      if (USE_MOCKS) {
        return await mockInvitationsApi.create({
          projectId: data.projectId,
          inviteeId: data.userId || "",
          role: data.role,
          type: data.type,
          message: data.message,
        })
      }
      // Backend endpoint: POST /api/invitations/send
      const response = await apiClient.post<Record<string, unknown>>(`/invitations/send`, {
        projectId: data.projectId,
        userId: data.userId,
        username: data.username,
        role: data.role,
        type: data.type,
        message: data.message,
      })
      return mapInvitation(response.data)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "members", variables.projectId] })
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations", "incoming"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations", "sent"] })
    },
  })
}

type InvitationsPageResponse = {
  items: Invitation[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasNext: boolean
  hasPrevious: boolean
}

/**
 * Get incoming invitations for current user
 */
export function useIncomingInvitations() {
  return useQuery({
    queryKey: ["teams", "invitations", "incoming"],
    queryFn: async () => {
      if (USE_MOCKS) {
        return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasNext: false, hasPrevious: false }
      }
      // Backend endpoint: GET /api/invitations/incoming
      const response = await apiClient.get<{
        items: Record<string, unknown>[]
        totalCount: number
        page: number
        pageSize: number
        totalPages: number
        hasNext: boolean
        hasPrevious: boolean
      }>("/invitations/incoming")
      return {
        ...response.data,
        items: (response.data.items || []).map(mapInvitation),
      } as InvitationsPageResponse
    },
  })
}

/**
 * Get sent invitations/requests for current user
 */
export function useSentInvitations() {
  return useQuery({
    queryKey: ["teams", "invitations", "sent"],
    queryFn: async () => {
      if (USE_MOCKS) {
        return { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0, hasNext: false, hasPrevious: false }
      }
      const response = await apiClient.get<{
        items: Record<string, unknown>[]
        totalCount: number
        page: number
        pageSize: number
        totalPages: number
        hasNext: boolean
        hasPrevious: boolean
      }>("/invitations/sent")

      return {
        ...response.data,
        items: (response.data.items || []).map(mapInvitation),
      } as InvitationsPageResponse
    },
  })
}

/**
 * Respond to invitation (accept/reject)
 */
export function useRespondToInvitation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      invitationId,
      action,
    }: {
      invitationId: string
      action: "accept" | "decline"
    }): Promise<void> => {
      if (USE_MOCKS) {
        return
      }
      // Backend endpoint: POST /api/invitations/respond
      await apiClient.post(`/invitations/respond`, { invitationId, action })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations", "incoming"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations", "sent"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "members"] })
    },
  })
}

export type VacancyDto = {
  role: string
  totalNeeded: number
  currentFilled: number
  hoursPerWeek: number | null
  equityOptional: boolean
}

export type OpenRoleInput = {
  role: string
  totalNeeded: number
  hoursPerWeek: number | null
  equityOptional: boolean
}

/**
 * Get open role vacancies for a project
 */
export function useProjectRoles(projectId: string) {
  return useQuery({
    queryKey: ["teams", "roles", projectId],
    queryFn: async () => {
      const response = await apiClient.get<VacancyDto[]>(`/projects/${projectId}/team/roles`)
      return response.data
    },
    enabled: Boolean(projectId),
  })
}

/**
 * Cancel invitation (sender only)
 */
export function useCancelInvitation() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ invitationId }: { invitationId: string }): Promise<void> => {
      if (USE_MOCKS) {
        return
      }
      await apiClient.delete(`/invitations/${invitationId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations", "incoming"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "invitations", "sent"] })
    },
  })
}
