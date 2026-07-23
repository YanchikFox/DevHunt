"use client"

import { useState, useCallback } from "react"
import { ROLES } from "@/lib/constants/roles"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Textarea } from "@/components/ui/textarea"
import { PasswordConfirmDialog } from "./PasswordConfirmDialog"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import {
  Shield,
  ShieldAlert,
  Activity,
  Users,
  Trash2,
  UserPlus,
  UserMinus,
  Loader2,
  Search,
  AlertTriangle,
  UserCheck,
  UserX,
  FolderKanban,
  ShieldCheck,
  GraduationCap,
  ScrollText,
  Settings,
  BellRing,
} from "lucide-react"
import {
  useSuperAdminSystemInfo,
  useSuperAdminAuditLogs,
  useSuperAdminListAdmins,
  useHardDeleteUser,
  useHardDeleteProject,
  usePromoteToAdmin,
  useDemoteAdmin,
  useBroadcastNotification,
  type AuditLogEntry,
  type AuditLogFilters,
  type AdminUser,
} from "@/lib/api/queries/superadmin"
import { StatsCard } from "@/components/ui/stats-card"
import { SystemSettings } from "./SystemSettings"

// ── Sub-components ─────────────────────────────────────────

function SystemOverview() {
  const t = useTranslations("superAdmin")
  const { data: info, isLoading, isError, error } = useSuperAdminSystemInfo()

  if (isLoading) {
    return <div className="flex justify-center p-8"><Loader2 className="h-6 w-6 animate-spin" /></div>
  }
  if (isError || !info) {
    const msg = (error as { userMessage?: string })?.userMessage ?? (error as Error)?.message ?? "Failed to load system info"
    return (
      <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive font-mono">
        <strong>API Error (system-info):</strong> {msg}
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatsCard title={t("totalUsers")} value={info.totalUsers} icon={Users} />
        <StatsCard title={t("activeUsers")} value={info.activeUsers} icon={UserCheck} />
        <StatsCard title={t("blockedUsers")} value={info.blockedUsers} icon={UserX} colorScheme={info.blockedUsers > 0 ? "orange" : undefined} />
        <StatsCard title={t("totalProjects")} value={info.totalProjects} icon={FolderKanban} />
        <StatsCard title={t("admins")} value={info.adminCount} icon={Shield} />
        <StatsCard title={t("curators")} value={info.curatorCount} icon={GraduationCap} />
        <StatsCard title={t("superAdmins")} value={info.superAdminCount} icon={ShieldCheck} />
        <StatsCard title={t("auditEntries")} value={info.totalAuditLogs} icon={ScrollText} />
      </div>

      {info.recentCriticalActions.length > 0 && (
        <Card className="border-destructive/30">
          <CardHeader className="pb-3">
            <CardTitle className="text-sm flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-destructive" />
              {t("recentCritical")}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              {info.recentCriticalActions.map((action, i) => (
                <div key={`${action.createdAt}-${i}`} className="flex items-start justify-between text-sm border-b last:border-0 pb-2">
                  <div>
                    <span className="font-medium">{action.action}</span>
                    {action.details && <p className="text-xs text-muted-foreground mt-0.5">{action.details}</p>}
                  </div>
                  <div className="text-xs text-muted-foreground text-right whitespace-nowrap ml-4">
                    <div>{new Date(action.createdAt).toLocaleString()}</div>
                    {action.ipAddress && <div>{action.ipAddress}</div>}
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}

const SEVERITY_COLOR: Record<string, string> = {
  info: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-400",
  warning: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/40 dark:text-yellow-400",
  critical: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400",
}

function AuditLogTableRow({ entry, onRowClick }: { entry: AuditLogEntry; onRowClick: (entry: AuditLogEntry) => void }) {
  const handleClick = useCallback(() => onRowClick(entry), [onRowClick, entry])
  return (
    <TableRow
      key={entry.id}
      className="cursor-pointer hover:bg-muted/60"
      onClick={handleClick}
    >
      <TableCell className="text-xs">{new Date(entry.createdAt).toLocaleString()}</TableCell>
      <TableCell>
        <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded-full ${SEVERITY_COLOR[entry.severity] ?? ""}`}>
          {entry.severity}
        </span>
      </TableCell>
      <TableCell className="text-xs font-mono">{entry.action}</TableCell>
      <TableCell className="text-xs max-w-[300px] truncate text-muted-foreground">{entry.details ?? "—"}</TableCell>
      <TableCell className="text-xs font-mono">{entry.ipAddress ?? "—"}</TableCell>
    </TableRow>
  )
}

interface AuditLogFiltersBarProps {
  filters: AuditLogFilters
  total: number
  onFilterChange: (key: keyof AuditLogFilters, value: string | boolean | undefined) => void
  onAdminOnlyToggle: () => void
  onSeverityChange: (value: string) => void
  t: ReturnType<typeof useTranslations>
}

function AuditLogFiltersBar({ filters, total, onFilterChange, onAdminOnlyToggle, onSeverityChange, t }: AuditLogFiltersBarProps) {
  return (
    <>
      <div className="flex flex-wrap gap-2">
        <div className="flex items-center gap-2 flex-1 min-w-[160px]">
          <Search className="h-4 w-4 shrink-0 text-muted-foreground" />
          <Input placeholder={t("filterByAction")} onChange={(e) => onFilterChange("action", e.target.value)} className="h-9" />
        </div>
        <Input placeholder="IP address" onChange={(e) => onFilterChange("ip", e.target.value)} className="h-9 w-[150px]" />
        <Select defaultValue="all" onValueChange={onSeverityChange}>
          <SelectTrigger className="w-[130px] h-9"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("allSeverities")}</SelectItem>
            <SelectItem value="info">Info</SelectItem>
            <SelectItem value="warning">Warning</SelectItem>
            <SelectItem value="critical">Critical</SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="flex flex-wrap gap-2 items-center">
        <Input type="date" onChange={(e) => onFilterChange("dateFrom", e.target.value)} className="h-9 w-[150px]" />
        <span className="text-xs text-muted-foreground">—</span>
        <Input type="date" onChange={(e) => onFilterChange("dateTo", e.target.value)} className="h-9 w-[150px]" />
        <button
          type="button"
          onClick={onAdminOnlyToggle}
          className={`flex items-center gap-2 rounded-md border px-3 h-9 text-xs font-medium transition-colors ${
            filters.adminOnly ? "bg-primary text-primary-foreground border-primary" : "bg-background text-muted-foreground"
          }`}
        >
          <Shield className="h-3.5 w-3.5" /> Admin only
        </button>
        <span className="text-xs text-muted-foreground ml-auto">{t("totalEntries", { count: total })}</span>
      </div>
    </>
  )
}

function AuditLogDetailDialog({ entry, onClose }: { entry: AuditLogEntry | null; onClose: (open: boolean) => void }) {
  return (
    <Dialog open={!!entry} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="font-mono text-sm">{entry?.action}</DialogTitle>
        </DialogHeader>
        {entry && (
          <div className="space-y-3 text-sm">
            <div className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-xs">
              <span className="text-muted-foreground font-medium">Date</span>
              <span className="font-mono">{new Date(entry.createdAt).toLocaleString()}</span>
              <span className="text-muted-foreground font-medium">Severity</span>
              <span className={`inline-block w-fit text-[10px] font-semibold px-1.5 py-0.5 rounded-full ${SEVERITY_COLOR[entry.severity] ?? ""}`}>
                {entry.severity}
              </span>
              {entry.entityType && (
                <>
                  <span className="text-muted-foreground font-medium">Entity</span>
                  <span className="font-mono">{entry.entityType}{entry.entityId ? ` / ${entry.entityId}` : ""}</span>
                </>
              )}
              {entry.userId && (
                <>
                  <span className="text-muted-foreground font-medium">User</span>
                  <span className="font-mono">{entry.userId}{entry.userRole ? ` (${entry.userRole})` : ""}</span>
                </>
              )}
              {entry.ipAddress && (
                <>
                  <span className="text-muted-foreground font-medium">IP</span>
                  <span className="font-mono">{entry.ipAddress}</span>
                </>
              )}
            </div>
            {entry.details && (
              <div className="space-y-1">
                <p className="text-xs text-muted-foreground font-medium">Details</p>
                <pre className="whitespace-pre-wrap break-words rounded-md bg-muted p-3 text-xs font-mono leading-relaxed">
                  {entry.details}
                </pre>
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

function AuditLogViewer() {
  const t = useTranslations("superAdmin")
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<AuditLogFilters>({ adminOnly: true })
  const [selectedEntry, setSelectedEntry] = useState<AuditLogEntry | null>(null)

  const { data, isLoading, isError, error } = useSuperAdminAuditLogs(page, 30, filters)

  const resetPage = useCallback(() => setPage(1), [])

  const handleFilterChange = useCallback((key: keyof AuditLogFilters, value: string | boolean | undefined) => {
    setFilters((prev) => ({ ...prev, [key]: value || undefined }))
    setPage(1)
  }, [])

  const handleAdminOnlyToggle = useCallback(() => {
    setFilters((prev) => ({ ...prev, adminOnly: !prev.adminOnly }))
    resetPage()
  }, [resetPage])

  const handleSeverityChange = useCallback((value: string) => {
    handleFilterChange("severity", value !== "all" ? value : undefined)
  }, [handleFilterChange])

  const handlePrevPage = useCallback(() => setPage((p) => Math.max(1, p - 1)), [])
  const handleNextPage = useCallback(() => setPage((p) => p + 1), [])
  const handleRowClick = useCallback((entry: AuditLogEntry) => setSelectedEntry(entry), [])
  const handleCloseDetailDialog = useCallback((open: boolean) => { if (!open) setSelectedEntry(null) }, [])

  return (
    <div className="space-y-4">
      <AuditLogFiltersBar
        filters={filters}
        total={data?.total ?? 0}
        onFilterChange={handleFilterChange}
        onAdminOnlyToggle={handleAdminOnlyToggle}
        onSeverityChange={handleSeverityChange}
        t={t}
      />
      <AuditLogDetailDialog entry={selectedEntry} onClose={handleCloseDetailDialog} />

      {isLoading ? (
        <div className="flex justify-center p-8"><Loader2 className="h-6 w-6 animate-spin" /></div>
      ) : isError ? (
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive font-mono">
          <strong>API Error (audit-logs):</strong> {(error as { userMessage?: string })?.userMessage ?? (error as Error)?.message ?? "Failed to load audit logs"}
        </div>
      ) : (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-[140px]">{t("date")}</TableHead>
                <TableHead className="w-[80px]">{t("severity")}</TableHead>
                <TableHead>{t("action")}</TableHead>
                <TableHead>{t("details")}</TableHead>
                <TableHead className="w-[110px]">IP</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(data?.data ?? []).map((entry) => (
                <AuditLogTableRow key={entry.id} entry={entry} onRowClick={handleRowClick} />
              ))}
            </TableBody>
          </Table>

          <div className="flex justify-end gap-2">
            <Button variant="outline" size="sm" disabled={page <= 1} onClick={handlePrevPage}>
              {t("prev")}
            </Button>
            <Button variant="outline" size="sm" disabled={(data?.data.length ?? 0) < 30} onClick={handleNextPage}>
              {t("next")}
            </Button>
          </div>
        </>
      )}
    </div>
  )
}

function AdminTableRow({ admin, onDemote }: { admin: AdminUser; onDemote: (id: string) => void }) {
  const t = useTranslations("superAdmin")
  const handleDemote = useCallback(() => onDemote(admin.id), [onDemote, admin.id])
  return (
    <TableRow>
      <TableCell className="text-sm">{admin.email}</TableCell>
      <TableCell className="text-sm">{admin.fullName ?? "—"}</TableCell>
      <TableCell>
        <Badge variant={admin.role === ROLES.SUPER_ADMIN ? "destructive" : admin.role === ROLES.ADMIN ? "default" : "secondary"}>
          {admin.role}
        </Badge>
      </TableCell>
      <TableCell className="text-xs">{admin.lastLogin ? new Date(admin.lastLogin).toLocaleDateString() : "—"}</TableCell>
      <TableCell>
        {admin.role !== ROLES.SUPER_ADMIN && (
          <Button variant="ghost" size="sm" onClick={handleDemote}>
            <UserMinus className="h-4 w-4 mr-1" />
            {t("demote")}
          </Button>
        )}
      </TableCell>
    </TableRow>
  )
}

function AdminManagement() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const { data: admins, isLoading, isError, error } = useSuperAdminListAdmins()
  const promoteMutation = usePromoteToAdmin()
  const demoteMutation = useDemoteAdmin()

  const [promoteDialogOpen, setPromoteDialogOpen] = useState(false)
  const [demoteDialogOpen, setDemoteDialogOpen] = useState(false)
  const [selectedUserId, setSelectedUserId] = useState<string | null>(null)
  const [promoteUserId, setPromoteUserId] = useState("")

  const handlePromoteClick = useCallback(() => {
    if (promoteUserId.trim()) {
      setSelectedUserId(promoteUserId.trim())
      setPromoteDialogOpen(true)
    }
  }, [promoteUserId])

  const handleDemoteClick = useCallback((userId: string) => {
    setSelectedUserId(userId)
    setDemoteDialogOpen(true)
  }, [])

  const handlePromoteUserIdChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setPromoteUserId(e.target.value)
  }, [])

  const handlePromoteConfirm = useCallback(
    async (password: string) => {
      if (!selectedUserId) return
      try {
        await promoteMutation.mutateAsync({ userId: selectedUserId, confirmPassword: password })
        toast({ title: t("promoted") })
        setPromoteDialogOpen(false)
        setPromoteUserId("")
      } catch {
        toast({ title: t("error"), variant: "destructive" })
      }
    },
    [selectedUserId, promoteMutation, toast, t]
  )

  const handleDemoteConfirm = useCallback(
    async (password: string) => {
      if (!selectedUserId) return
      try {
        await demoteMutation.mutateAsync({ userId: selectedUserId, confirmPassword: password })
        toast({ title: t("demoted") })
        setDemoteDialogOpen(false)
      } catch {
        toast({ title: t("error"), variant: "destructive" })
      }
    },
    [selectedUserId, demoteMutation, toast, t]
  )

  if (isLoading) {
    return <div className="flex justify-center p-8"><Loader2 className="h-6 w-6 animate-spin" /></div>
  }
  if (isError) {
    const msg = (error as { userMessage?: string })?.userMessage ?? (error as Error)?.message ?? "Failed to load admins"
    return (
      <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive font-mono">
        <strong>API Error (admins):</strong> {msg}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm">{t("promoteUser")}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex gap-2">
            <Input
              placeholder={t("enterUserId")}
              value={promoteUserId}
              onChange={handlePromoteUserIdChange}
              className="flex-1"
            />
            <Button onClick={handlePromoteClick} disabled={!promoteUserId.trim()}>
              <UserPlus className="h-4 w-4 mr-2" />
              {t("promoteToAdmin")}
            </Button>
          </div>
        </CardContent>
      </Card>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>{t("email")}</TableHead>
            <TableHead>{t("name")}</TableHead>
            <TableHead>{t("role")}</TableHead>
            <TableHead>{t("lastLogin")}</TableHead>
            <TableHead>{t("actions")}</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {(admins ?? []).map((admin) => (
            <AdminTableRow key={admin.id} admin={admin} onDemote={handleDemoteClick} />
          ))}
        </TableBody>
      </Table>

      <PasswordConfirmDialog
        open={promoteDialogOpen}
        onOpenChange={setPromoteDialogOpen}
        title={t("promoteToAdmin")}
        description={t("promoteDescription")}
        severity="warning"
        isLoading={promoteMutation.isPending}
        onConfirm={handlePromoteConfirm}
      />

      <PasswordConfirmDialog
        open={demoteDialogOpen}
        onOpenChange={setDemoteDialogOpen}
        title={t("demoteAdmin")}
        description={t("demoteDescription")}
        severity="warning"
        isLoading={demoteMutation.isPending}
        onConfirm={handleDemoteConfirm}
      />
    </div>
  )
}

interface DangerDeleteCardProps {
  titleKey: string
  idPlaceholder: string
  deleteLabelKey: string
  confirmTitleKey: string
  confirmDescKey: string
  isPending: boolean
  onConfirm: (id: string, reason: string, password: string) => Promise<void>
}

function DangerDeleteCard({ titleKey, idPlaceholder, deleteLabelKey, confirmTitleKey, confirmDescKey, isPending, onConfirm }: DangerDeleteCardProps) {
  const t = useTranslations("superAdmin")
  const [id, setId] = useState("")
  const [reason, setReason] = useState("")
  const [dialogOpen, setDialogOpen] = useState(false)

  const handleIdChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setId(e.target.value), [])
  const handleReasonChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setReason(e.target.value), [])
  const handleClick = useCallback(() => { if (id.trim() && reason.trim()) setDialogOpen(true) }, [id, reason])
  const handleConfirm = useCallback(async (password: string) => {
    await onConfirm(id, reason, password)
    setDialogOpen(false)
    setId("")
    setReason("")
  }, [id, reason, onConfirm])

  return (
    <>
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm flex items-center gap-2">
            <Trash2 className="h-4 w-4" />
            {t(titleKey)}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Input placeholder={idPlaceholder} value={id} onChange={handleIdChange} />
          <Input placeholder={t("reason")} value={reason} onChange={handleReasonChange} />
          <Button variant="destructive" size="sm" onClick={handleClick} disabled={!id.trim() || !reason.trim()}>
            <Trash2 className="h-4 w-4 mr-2" />
            {t(deleteLabelKey)}
          </Button>
        </CardContent>
      </Card>
      <PasswordConfirmDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        title={t(confirmTitleKey)}
        description={t(confirmDescKey)}
        severity="critical"
        isLoading={isPending}
        onConfirm={handleConfirm}
      />
    </>
  )
}

function DangerZone() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const hardDeleteUser = useHardDeleteUser()
  const hardDeleteProject = useHardDeleteProject()

  const handleDeleteUser = useCallback(async (id: string, reason: string, password: string) => {
    try {
      await hardDeleteUser.mutateAsync({ userId: id, confirmPassword: password, reason })
      toast({ title: t("userDeleted") })
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [hardDeleteUser, toast, t])

  const handleDeleteProject = useCallback(async (id: string, reason: string, password: string) => {
    try {
      await hardDeleteProject.mutateAsync({ projectId: id, confirmPassword: password, reason })
      toast({ title: t("projectDeleted") })
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [hardDeleteProject, toast, t])

  return (
    <div className="space-y-4">
      <div className="rounded-lg border-2 border-destructive/30 bg-destructive/5 p-4">
        <div className="flex items-center gap-2 mb-4">
          <ShieldAlert className="h-5 w-5 text-destructive" />
          <h3 className="font-semibold text-destructive">{t("dangerZone")}</h3>
        </div>
        <p className="text-sm text-muted-foreground mb-4">{t("dangerZoneDescription")}</p>
        <div className="space-y-6">
          <DangerDeleteCard
            titleKey="hardDeleteUser"
            idPlaceholder="User ID"
            deleteLabelKey="deleteUser"
            confirmTitleKey="hardDeleteUser"
            confirmDescKey="hardDeleteUserWarning"
            isPending={hardDeleteUser.isPending}
            onConfirm={handleDeleteUser}
          />
          <DangerDeleteCard
            titleKey="hardDeleteProject"
            idPlaceholder="Project ID"
            deleteLabelKey="deleteProject"
            confirmTitleKey="hardDeleteProject"
            confirmDescKey="hardDeleteProjectWarning"
            isPending={hardDeleteProject.isPending}
            onConfirm={handleDeleteProject}
          />
        </div>
      </div>
    </div>
  )
}

// ── Broadcast Notification ─────────────────────────────────

function BroadcastNotificationPanel() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const broadcastMutation = useBroadcastNotification()

  const [title, setTitle] = useState("")
  const [content, setContent] = useState("")
  const [priority, setPriority] = useState("normal")
  const [dialogOpen, setDialogOpen] = useState(false)

  const handleSendClick = useCallback(() => {
    if (title.trim()) setDialogOpen(true)
  }, [title])

  const handleConfirm = useCallback(
    async (password: string) => {
      try {
        const result = await broadcastMutation.mutateAsync({
          title: title.trim(),
          content: content.trim(),
          priority,
          confirmPassword: password,
        })
        toast({ title: t("broadcastSent", { count: result.count }) })
        setDialogOpen(false)
        setTitle("")
        setContent("")
        setPriority("normal")
      } catch {
        toast({ title: t("error"), variant: "destructive" })
      }
    },
    [title, content, priority, broadcastMutation, toast, t]
  )

  const handleTitleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setTitle(e.target.value), [])
  const handleContentChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setContent(e.target.value), [])
  const handlePriorityChange = useCallback((value: string) => setPriority(value), [])

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-sm flex items-center gap-2">
            <BellRing className="h-4 w-4" />
            {t("broadcastNotification")}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">{t("broadcastDescription")}</p>
          <Input
            placeholder={t("broadcastTitle")}
            value={title}
            onChange={handleTitleChange}
          />
          <Textarea
            placeholder={t("broadcastContent")}
            value={content}
            onChange={handleContentChange}
            rows={3}
          />
          <Select value={priority} onValueChange={handlePriorityChange}>
            <SelectTrigger className="w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="low">{t("priorityLow")}</SelectItem>
              <SelectItem value="normal">{t("priorityNormal")}</SelectItem>
              <SelectItem value="high">{t("priorityHigh")}</SelectItem>
              <SelectItem value="critical">{t("priorityCritical")}</SelectItem>
            </SelectContent>
          </Select>
          <Button onClick={handleSendClick} disabled={!title.trim() || broadcastMutation.isPending}>
            {broadcastMutation.isPending && <Loader2 className="h-4 w-4 mr-2 animate-spin" />}
            <BellRing className="h-4 w-4 mr-2" />
            {t("broadcastSend")}
          </Button>
        </CardContent>
      </Card>

      <PasswordConfirmDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        title={t("broadcastNotification")}
        description={t("broadcastConfirm")}
        severity="warning"
        isLoading={broadcastMutation.isPending}
        onConfirm={handleConfirm}
      />
    </div>
  )
}

// ── Main Component ─────────────────────────────────────────

export function SuperAdminPanel() {
  const t = useTranslations("superAdmin")

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2 mb-2">
        <Shield className="h-5 w-5 text-destructive" />
        <h2 className="text-lg font-semibold">{t("title")}</h2>
        <Badge variant="destructive" className="text-[10px]">SUPERADMIN</Badge>
      </div>

      <Tabs defaultValue="overview" className="space-y-4">
        <TabsList>
          <TabsTrigger value="overview" className="gap-1">
            <Activity className="h-3.5 w-3.5" />
            {t("overview")}
          </TabsTrigger>
          <TabsTrigger value="admins" className="gap-1">
            <Users className="h-3.5 w-3.5" />
            {t("adminManagement")}
          </TabsTrigger>
          <TabsTrigger value="audit" className="gap-1">
            <Search className="h-3.5 w-3.5" />
            {t("auditLog")}
          </TabsTrigger>
          <TabsTrigger value="danger" className="gap-1">
            <ShieldAlert className="h-3.5 w-3.5" />
            {t("dangerZone")}
          </TabsTrigger>
          <TabsTrigger value="settings" className="gap-1">
            <Settings className="h-3.5 w-3.5" />
            {t("systemSettings")}
          </TabsTrigger>
          <TabsTrigger value="broadcast" className="gap-1">
            <BellRing className="h-3.5 w-3.5" />
            {t("broadcastNotification")}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="overview">
          <SystemOverview />
        </TabsContent>
        <TabsContent value="admins">
          <AdminManagement />
        </TabsContent>
        <TabsContent value="audit">
          <AuditLogViewer />
        </TabsContent>
        <TabsContent value="danger">
          <DangerZone />
        </TabsContent>
        <TabsContent value="settings">
          <SystemSettings />
        </TabsContent>
        <TabsContent value="broadcast">
          <BroadcastNotificationPanel />
        </TabsContent>
      </Tabs>
    </div>
  )
}
