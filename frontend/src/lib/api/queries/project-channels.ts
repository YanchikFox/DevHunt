import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

/**
 * Discord/Slack-style channel scoped to a project. On the backend this is
 * simply a `Conversation` with `Type = ProjectChannel` — messages are fetched
 * from the regular `/chat/conversations/{id}/messages` endpoint using the
 * channel id as conversation id.
 */
export type ChannelRole = "admin" | "member"

export type ProjectChannel = {
  id: string
  projectId: string
  slug: string
  title: string | null
  topic: string | null
  isPrivate: boolean
  position: number
  createdAt: string
  lastMessageAt: string | null
  unreadCount: number
  isMember: boolean
  /** Shortcut — true if the viewer can at least edit the channel or manage members. */
  canManage: boolean
  canPost: boolean
  canManageMembers: boolean
  canEditChannel: boolean
  canDeleteChannel: boolean
  canPinMessages: boolean
  canDeleteMessages: boolean
  /** Viewer's role label; null when they have no participant row yet. */
  viewerRole: ChannelRole | null
}

type ChannelFromApi = Omit<ProjectChannel, "id" | "projectId"> & {
  id: string
  projectId: string
}

const channelsKey = (projectId: string) => ["project", projectId, "channels"] as const

function useInvalidateChannels(projectId: string) {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: channelsKey(projectId) })
}

/**
 * List visible channels for a project. Private channels are filtered out
 * server-side for users who aren't participants.
 */
export function useProjectChannels(projectId: string | null | undefined) {
  return useQuery({
    queryKey: projectId ? channelsKey(projectId) : ["project", "_none", "channels"],
    queryFn: async () => {
      if (!projectId) return [] as ProjectChannel[]
      const res = await apiClient.get<ChannelFromApi[]>(`/projects/${projectId}/channels`)
      return res.data
    },
    enabled: Boolean(projectId),
    staleTime: 30_000,
  })
}

export type CreateChannelPayload = {
  slug: string
  title?: string | null
  topic?: string | null
  isPrivate?: boolean
}

/** Create a new channel in the project. */
export function useCreateProjectChannel(projectId: string) {
  const onSuccess = useInvalidateChannels(projectId)
  return useMutation({
    mutationFn: async (payload: CreateChannelPayload) => {
      const res = await apiClient.post<ChannelFromApi>(`/projects/${projectId}/channels`, payload)
      return res.data
    },
    onSuccess,
  })
}

export type UpdateChannelPayload = {
  slug?: string
  title?: string | null
  topic?: string | null
  isPrivate?: boolean
  position?: number
}

/** Update channel metadata. */
export function useUpdateProjectChannel(projectId: string) {
  const onSuccess = useInvalidateChannels(projectId)
  return useMutation({
    mutationFn: async ({ channelId, payload }: { channelId: string; payload: UpdateChannelPayload }) => {
      const res = await apiClient.patch<ChannelFromApi>(`/projects/${projectId}/channels/${channelId}`, payload)
      return res.data
    },
    onSuccess,
  })
}

/** Delete a channel. #general cannot be deleted (server-enforced). */
export function useDeleteProjectChannel(projectId: string) {
  const onSuccess = useInvalidateChannels(projectId)
  return useMutation({
    mutationFn: async (channelId: string) => {
      await apiClient.delete(`/projects/${projectId}/channels/${channelId}`)
      return channelId
    },
    onSuccess,
  })
}

/** Join a channel (mostly used for private channels after accepting an invite). */
export function useJoinProjectChannel(projectId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (channelId: string) => {
      const res = await apiClient.post<ChannelFromApi>(
        `/projects/${projectId}/channels/${channelId}/join`,
      )
      return res.data
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: channelsKey(projectId) })
    },
  })
}

/** Leave a channel. #general cannot be left (server-enforced). */
export function useLeaveProjectChannel(projectId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: async (channelId: string) => {
      await apiClient.post(`/projects/${projectId}/channels/${channelId}/leave`)
      return channelId
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: channelsKey(projectId) })
    },
  })
}
