import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient as api } from "../client"

// ── Types ──────────────────────────────────────────────────

export interface AuditLogEntry {
  id: string
  userId: string | null
  userRole: string | null
  action: string
  entityType: string
  entityId: string | null
  details: string | null
  ipAddress: string | null
  severity: string
  createdAt: string
}

export interface AuditLogResponse {
  data: AuditLogEntry[]
  total: number
  page: number
  pageSize: number
}

export interface SystemInfo {
  totalUsers: number
  activeUsers: number
  blockedUsers: number
  adminCount: number
  curatorCount: number
  superAdminCount: number
  totalProjects: number
  totalAuditLogs: number
  recentCriticalActions: {
    action: string
    details: string | null
    createdAt: string
    ipAddress: string | null
  }[]
}

export interface AdminUser {
  id: string
  email: string
  fullName: string | null
  role: string
  isActive: boolean
  isVerified: boolean
  createdAt: string
  lastLogin: string | null
}

// ── Queries ────────────────────────────────────────────────

export interface AuditLogFilters {
  action?: string
  userId?: string
  severity?: string
  ip?: string
  entityType?: string
  dateFrom?: string
  dateTo?: string
  adminOnly?: boolean
}

export const useSuperAdminAuditLogs = (
  page = 1,
  pageSize = 30,
  filters: AuditLogFilters = {}
) => {
  const { action, userId, severity, ip, entityType, dateFrom, dateTo, adminOnly = true } = filters
  return useQuery({
    queryKey: ["superadmin", "audit-logs", page, pageSize, filters],
    queryFn: async () => {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: pageSize.toString(),
        adminOnly: adminOnly.toString(),
      })
      if (action) params.append("action", action)
      if (userId) params.append("userId", userId)
      if (severity) params.append("severity", severity)
      if (ip) params.append("ip", ip)
      if (entityType) params.append("entityType", entityType)
      if (dateFrom) params.append("dateFrom", dateFrom)
      if (dateTo) params.append("dateTo", dateTo)
      const { data } = await api.get<AuditLogResponse>(`/superadmin/audit-logs?${params.toString()}`)
      return data
    },
    retry: 1,
  })
}

export const useSuperAdminSystemInfo = () => {
  return useQuery({
    queryKey: ["superadmin", "system-info"],
    queryFn: async () => {
      const { data } = await api.get<SystemInfo>("/superadmin/system-info")
      return data
    },
    retry: 1,
  })
}

export const useSuperAdminListAdmins = () => {
  return useQuery({
    queryKey: ["superadmin", "admins"],
    queryFn: async () => {
      const { data } = await api.get<AdminUser[]>("/superadmin/admins")
      return data
    },
    retry: 1,
  })
}

// ── Mutations ──────────────────────────────────────────────

export const useVerifySuperAdminPassword = () => {
  return useMutation({
    mutationFn: async (confirmPassword: string) => {
      await api.post("/superadmin/verify-password", { confirmPassword })
    },
  })
}

export const useHardDeleteUser = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { userId: string; confirmPassword: string; reason: string }) => {
      const { data } = await api.post("/superadmin/hard-delete/user", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
      queryClient.invalidateQueries({ queryKey: ["superadmin"] })
    },
  })
}

export const useHardDeleteProject = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { projectId: string; confirmPassword: string; reason: string }) => {
      const { data } = await api.post("/superadmin/hard-delete/project", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] })
      queryClient.invalidateQueries({ queryKey: ["superadmin"] })
    },
  })
}

export const usePromoteToAdmin = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { userId: string; confirmPassword: string }) => {
      const { data } = await api.post("/superadmin/promote-admin", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
      queryClient.invalidateQueries({ queryKey: ["superadmin", "admins"] })
    },
  })
}

export const useDemoteAdmin = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { userId: string; confirmPassword: string; newRole?: string }) => {
      const { data } = await api.post("/superadmin/demote-admin", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin", "users"] })
      queryClient.invalidateQueries({ queryKey: ["superadmin", "admins"] })
    },
  })
}

// ── Platform Settings ─────────────────────────────────────────

export interface PlatformSettingEntry {
  key: string
  value: string
  description: string | null
  updatedAt: string
  updatedById: string | null
}

export const usePlatformSettings = () => {
  return useQuery({
    queryKey: ["superadmin", "settings"],
    queryFn: async () => {
      const { data } = await api.get<PlatformSettingEntry[]>("/superadmin/settings")
      return data
    },
    retry: 1,
  })
}

export const useUpdatePlatformSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { settings: Record<string, string>; confirmPassword: string }) => {
      const { data } = await api.put("/superadmin/settings", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["superadmin", "settings"] })
    },
  })
}

// ── Feature Flags ─────────────────────────────────────────────

export interface FeatureFlagEntry {
  key: string
  enabled: boolean
  description: string | null
  updatedAt: string
  updatedById: string | null
}

export const useFeatureFlags = () => {
  return useQuery({
    queryKey: ["superadmin", "feature-flags"],
    queryFn: async () => {
      const { data } = await api.get<FeatureFlagEntry[]>("/superadmin/feature-flags")
      return data
    },
    retry: 1,
  })
}

export const useToggleFeatureFlag = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { key: string; confirmPassword: string }) => {
      const { data } = await api.put<{ key: string; enabled: boolean }>(
        `/superadmin/feature-flags/${req.key}`,
        { confirmPassword: req.confirmPassword }
      )
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["superadmin", "feature-flags"] })
    },
  })
}

// ── Maintenance Mode ──────────────────────────────────────────

export const useToggleMaintenance = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { enabled: boolean; confirmPassword: string }) => {
      const { data } = await api.post<{ enabled: boolean }>("/superadmin/maintenance", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["superadmin", "settings"] })
    },
  })
}

// ── Broadcast Notification ────────────────────────────────────

export const useBroadcastNotification = () => {
  return useMutation({
    mutationFn: async (req: { title: string; content: string; priority: string; confirmPassword: string }) => {
      const { data } = await api.post<{ message: string; count: number }>("/superadmin/broadcast-notification", req)
      return data
    },
  })
}

// ── IP Whitelist ──────────────────────────────────────────────

export interface IpWhitelistEntry {
  ip: string
  updatedAt: string
}

export const useIpWhitelist = () => {
  return useQuery({
    queryKey: ["superadmin", "ip-whitelist"],
    queryFn: async () => {
      const { data } = await api.get<IpWhitelistEntry[]>("/superadmin/ip-whitelist")
      return data
    },
    retry: 1,
  })
}

export const useAddIpToWhitelist = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { ip: string; confirmPassword: string }) => {
      const { data } = await api.post("/superadmin/ip-whitelist", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["superadmin", "ip-whitelist"] })
    },
  })
}

export const useRemoveIpFromWhitelist = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (req: { ip: string; confirmPassword: string }) => {
      await api.delete(`/superadmin/ip-whitelist/${encodeURIComponent(req.ip)}?confirmPassword=${encodeURIComponent(req.confirmPassword)}`)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["superadmin", "ip-whitelist"] })
    },
  })
}
