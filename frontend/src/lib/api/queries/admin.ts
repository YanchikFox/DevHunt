import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"
// Types
export interface AdminUserListResponse {
  total: number
  page: number
  pageSize: number
  data: AdminUserSummary[]
}

export interface AdminUserSummary {
  id: string
  email: string
  fullName: string
  username?: string
  role: string
  isActive: boolean
  isVerified: boolean
  createdAt: string
  rating: number
  suspendedUntil?: string
  suspensionReason?: string
}

export interface BlockUserRequest {
  userId: string
  reason: string
  isPermanent: boolean
}

export interface ChangeUserRoleRequest {
  userId: string
  newRole: string
}

export interface AdminProjectActionRequest {
  projectId: string
  action: "hide" | "archive" | "feature" | "unfeature"
  reason: string
}

export interface ChangeUsernameRequest {
  userId: string
  newUsername: string
  reason: string
}

export interface AdminEditProfileRequest {
  userId: string
  fullName?: string | null
  username?: string | null
  bio?: string | null
  reason: string
}

// Queries
export const useAdminUsers = (page = 1, pageSize = 20, role?: string, active?: boolean) => {
  return useQuery({
    queryKey: ["admin", "users", page, pageSize, role, active],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
      })
      if (role) params.append("role", role)
      if (active !== undefined) params.append("active", active.toString())

      const { data } = await api.get<AdminUserListResponse>(`/admin/users?${params.toString()}`)
      return data
    },
  })
}

export const useAdminStats = () => {
  return useQuery({
    queryKey: ["admin", "stats"],
    queryFn: async () => {
      const { data } = await api.get("/admin/stats")
      return data
    },
  })
}

// Mutations
export const useBlockUser = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: BlockUserRequest) => {
      await api.post("/admin/users/block", req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

function useAdminUserAction(endpoint: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (userId: string) => {
      await api.post(endpoint, userId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useUnblockUser = () => useAdminUserAction("/admin/users/unblock")
export const useVerifyUser = () => useAdminUserAction("/admin/users/verify")

export const useChangeUserRole = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: ChangeUserRoleRequest) => {
      await api.post("/admin/users/change-role", req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useAdminProjectAction = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: AdminProjectActionRequest) => {
      await api.post("/admin/projects/action", req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] })
    },
  })
}

// ── User Details & Suspension ─────────────────────────────

export interface AdminUserDetail {
  id: string
  email: string
  fullName: string | null
  username: string | null
  role: string
  isActive: boolean
  isVerified: boolean
  bio: string | null
  avatarUrl: string | null
  github: string | null
  linkedin: string | null
  website: string | null
  skills: string[]
  experience: number | null
  rating: number | null
  language: string | null
  timezone: string | null
  createdAt: string
  updatedAt: string | null
  lastLogin: string | null
  suspendedUntil: string | null
  suspensionReason: string | null
  isEmailVerified: boolean
  githubUsername: string | null
  googleId: string | null
  stats: {
    projectsOwned: number
    teamsCount: number
    messagesCount: number
    notesCount: number
  }
}

export interface AdminActivityLog {
  id: string
  action: string
  entityType: string
  entityId: string
  details: string | null
  ipAddress: string | null
  severity: string
  createdAt: string
}

export interface AdminNote {
  id: string
  content: string
  createdAt: string
  authorId: string
  authorName: string
}

export interface SuspendUserRequest {
  userId: string
  suspendedUntil: string
  reason: string
}

export interface BulkUserActionRequest {
  userIds: string[]
  action: "block" | "unblock" | "verify"
}

export const useChangeUsername = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, newUsername, reason }: ChangeUsernameRequest) => {
      const { data } = await api.put<{ username: string }>(`/admin/users/${userId}/change-name`, { newUsername, reason })
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useAdminEditProfile = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, reason, ...fields }: AdminEditProfileRequest) => {
      await api.put(`/admin/users/${userId}/edit-profile`, { ...fields, reason })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useAdminUserDetails = (userId: string | null) => {
  return useQuery({
    queryKey: ["admin", "users", userId, "details"],
    queryFn: async () => {
      const { data } = await api.get<AdminUserDetail>(`/admin/users/${userId}/details`)
      return data
    },
    enabled: Boolean(userId),
  })
}

export const useAdminUserActivity = (userId: string | null, page = 1, pageSize = 30) => {
  return useQuery({
    queryKey: ["admin", "users", userId, "activity", page],
    queryFn: async () => {
      const params = new URLSearchParams({ page: page.toString(), pageSize: pageSize.toString() })
      const { data } = await api.get<{ total: number; page: number; pageSize: number; data: AdminActivityLog[] }>(
        `/admin/users/${userId}/activity?${params.toString()}`
      )
      return data
    },
    enabled: Boolean(userId),
  })
}

export const useSuspendUser = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: SuspendUserRequest) => {
      await api.post("/admin/users/suspend", req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useUnsuspendUser = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (userId: string) => {
      await api.post("/admin/users/unsuspend", userId)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useAdminNotes = (userId: string | null) => {
  return useQuery({
    queryKey: ["admin", "users", userId, "notes"],
    queryFn: async () => {
      const { data } = await api.get<AdminNote[]>(`/admin/users/${userId}/notes`)
      return data
    },
    enabled: Boolean(userId),
  })
}

export const useAddAdminNote = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ userId, content }: { userId: string; content: string }) => {
      await api.post(`/admin/users/${userId}/notes`, content)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useDeleteAdminNote = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (noteId: string) => {
      await api.delete(`/admin/users/notes/${noteId}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

export const useBulkUserAction = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: BulkUserActionRequest) => {
      const { data } = await api.post<{ affected: number; total: number }>("/admin/users/bulk-action", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
    },
  })
}

