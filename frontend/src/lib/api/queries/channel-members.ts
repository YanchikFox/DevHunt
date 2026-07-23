import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"
import type { ChannelRole } from "./project-channels"

/**
 * Channel participant with resolved permissions. `can*Override` carries the
 * raw nullable column so a settings UI can render "inherit" vs. "explicit
 * true / false" as a tri-state; `can*` fields already collapse the override
 * against the role default and should be used for most gates.
 */
export type ChannelMember = {
  userId: string
  fullName: string
  avatarUrl: string | null
  username: string | null
  role: ChannelRole
  roleDefinitionId: string | null
  roleDisplayName: string | null
  state: "active" | "left" | "banned"
  canPost: boolean
  canManageMembers: boolean
  canEditChannel: boolean
  canDeleteChannel: boolean
  canPinMessages: boolean
  canDeleteMessages: boolean
  canPostOverride: boolean | null
  canManageMembersOverride: boolean | null
  canEditChannelOverride: boolean | null
  canDeleteChannelOverride: boolean | null
  canPinMessagesOverride: boolean | null
  canDeleteMessagesOverride: boolean | null
  joinedAt: string
  isProjectOwner: boolean
  isChannelCreator: boolean
  banReason: string | null
  bannedAt: string | null
}

export type ChannelCandidate = {
  userId: string
  fullName: string
  avatarUrl: string | null
  username: string | null
  isProjectOwner: boolean
}

export type ChannelRoleDefinition = {
  id: string
  name: string
  canPost: boolean
  canManageMembers: boolean
  canEditChannel: boolean
  canDeleteChannel: boolean
  canPinMessages: boolean
  canDeleteMessages: boolean
  createdAt: string
  updatedAt: string
}

const membersKey = (projectId: string, channelId: string) =>
  ["project", projectId, "channels", channelId, "members"] as const
const bannedKey = (projectId: string, channelId: string) =>
  ["project", projectId, "channels", channelId, "members", "banned"] as const
const candidatesKey = (projectId: string, channelId: string) =>
  ["project", projectId, "channels", channelId, "members", "candidates"] as const
const rolesKey = (projectId: string, channelId: string) =>
  ["project", projectId, "channels", channelId, "members", "roles"] as const
const channelsKey = (projectId: string) =>
  ["project", projectId, "channels"] as const

export function useChannelMembers(
  projectId: string | null | undefined,
  channelId: string | null | undefined,
) {
  return useQuery({
    queryKey:
      projectId && channelId
        ? membersKey(projectId, channelId)
        : ["project", "_none", "channels", "_none", "members"],
    queryFn: async () => {
      if (!projectId || !channelId) return [] as ChannelMember[]
      const res = await apiClient.get<ChannelMember[]>(
        `/projects/${projectId}/channels/${channelId}/members`,
      )
      return res.data
    },
    enabled: Boolean(projectId && channelId),
    staleTime: 15_000,
  })
}

export function useBannedChannelMembers(
  projectId: string | null | undefined,
  channelId: string | null | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey:
      projectId && channelId
        ? bannedKey(projectId, channelId)
        : ["project", "_none", "channels", "_none", "members", "banned"],
    queryFn: async () => {
      if (!projectId || !channelId) return [] as ChannelMember[]
      const res = await apiClient.get<ChannelMember[]>(
        `/projects/${projectId}/channels/${channelId}/members/banned`,
      )
      return res.data
    },
    enabled: Boolean(enabled && projectId && channelId),
    staleTime: 15_000,
  })
}

export function useChannelCandidates(
  projectId: string | null | undefined,
  channelId: string | null | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey:
      projectId && channelId
        ? candidatesKey(projectId, channelId)
        : ["project", "_none", "channels", "_none", "members", "candidates"],
    queryFn: async () => {
      if (!projectId || !channelId) return [] as ChannelCandidate[]
      const res = await apiClient.get<ChannelCandidate[]>(
        `/projects/${projectId}/channels/${channelId}/members/candidates`,
      )
      return res.data
    },
    enabled: Boolean(enabled && projectId && channelId),
    staleTime: 15_000,
  })
}

export type InviteChannelMemberPayload = {
  userId: string
  role?: ChannelRole
  roleDefinitionId?: string | null
}

export function useInviteChannelMember(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (payload: InviteChannelMemberPayload) => {
      const res = await apiClient.post<ChannelMember>(
        `/projects/${projectId}/channels/${channelId}/members`,
        payload,
      )
      return res.data
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: membersKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: candidatesKey(projectId, channelId) })
    },
  })
}

export type UpdateChannelMemberPayload = {
  role?: ChannelRole
  roleDefinitionId?: string | null
  canPost?: boolean | null
  canManageMembers?: boolean | null
  canEditChannel?: boolean | null
  canDeleteChannel?: boolean | null
  canPinMessages?: boolean | null
  canDeleteMessages?: boolean | null
  resetCanPost?: boolean
  resetCanManageMembers?: boolean
  resetCanEditChannel?: boolean
  resetCanDeleteChannel?: boolean
  resetCanPinMessages?: boolean
  resetCanDeleteMessages?: boolean
}

export function useUpdateChannelMember(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (args: {
      targetUserId: string
      payload: UpdateChannelMemberPayload
    }) => {
      const res = await apiClient.patch<ChannelMember>(
        `/projects/${projectId}/channels/${channelId}/members/${args.targetUserId}`,
        args.payload,
      )
      return res.data
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: membersKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: channelsKey(projectId) })
    },
  })
}

export function useChannelRoles(
  projectId: string | null | undefined,
  channelId: string | null | undefined,
  enabled = true,
) {
  return useQuery({
    queryKey:
      projectId && channelId
        ? rolesKey(projectId, channelId)
        : ["project", "_none", "channels", "_none", "members", "roles"],
    queryFn: async () => {
      if (!projectId || !channelId) return [] as ChannelRoleDefinition[]
      const res = await apiClient.get<ChannelRoleDefinition[]>(
        `/projects/${projectId}/channels/${channelId}/members/roles`,
      )
      return res.data
    },
    enabled: Boolean(enabled && projectId && channelId),
    staleTime: 15_000,
  })
}

export type ChannelRoleDefinitionPayload = Omit<
  ChannelRoleDefinition,
  "id" | "createdAt" | "updatedAt"
>

export function useCreateChannelRole(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (payload: ChannelRoleDefinitionPayload) => {
      const res = await apiClient.post<ChannelRoleDefinition>(
        `/projects/${projectId}/channels/${channelId}/members/roles`,
        payload,
      )
      return res.data
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: rolesKey(projectId, channelId) })
    },
  })
}

export function useUpdateChannelRole(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (args: { roleId: string; payload: ChannelRoleDefinitionPayload }) => {
      const res = await apiClient.put<ChannelRoleDefinition>(
        `/projects/${projectId}/channels/${channelId}/members/roles/${args.roleId}`,
        args.payload,
      )
      return res.data
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: rolesKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: membersKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: channelsKey(projectId) })
    },
  })
}

export function useDeleteChannelRole(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (roleId: string) => {
      await apiClient.delete(
        `/projects/${projectId}/channels/${channelId}/members/roles/${roleId}`,
      )
      return roleId
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: rolesKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: membersKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: channelsKey(projectId) })
    },
  })
}

export function useKickChannelMember(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (targetUserId: string) => {
      await apiClient.delete(
        `/projects/${projectId}/channels/${channelId}/members/${targetUserId}`,
      )
      return targetUserId
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: membersKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: candidatesKey(projectId, channelId) })
    },
  })
}

export type BanChannelMemberPayload = { targetUserId: string; reason?: string }

export function useBanChannelMember(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async ({ targetUserId, reason }: BanChannelMemberPayload) => {
      await apiClient.post(
        `/projects/${projectId}/channels/${channelId}/members/${targetUserId}/ban`,
        { reason: reason ?? null },
      )
      return targetUserId
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: membersKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: bannedKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: candidatesKey(projectId, channelId) })
    },
  })
}

export function useUnbanChannelMember(projectId: string, channelId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (targetUserId: string) => {
      await apiClient.post(
        `/projects/${projectId}/channels/${channelId}/members/${targetUserId}/unban`,
      )
      return targetUserId
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: bannedKey(projectId, channelId) })
      qc.invalidateQueries({ queryKey: candidatesKey(projectId, channelId) })
    },
  })
}
