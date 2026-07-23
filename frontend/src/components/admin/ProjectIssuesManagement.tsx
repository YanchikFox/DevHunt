"use client"

import { useState, useCallback } from "react"
import {
  useAdminIssues,
  useAdminIssueDetails,
  useAssignIssue,
  useResolveIssue,
  useEscalateIssue,
} from "@/lib/api/queries/admin-issues"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Eye, UserPlus, AlertTriangle, CheckCircle } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { useCurrentUser } from "@/hooks/use-current-user"

const ISSUE_STATUSES = ["open", "investigating", "resolved", "dismissed", "escalated"] as const
const ISSUE_TYPES = ["technical", "legal", "conflict", "abuse", "violation", "security", "other"] as const
const ISSUE_ACTIONS = ["warn", "suspend", "remove", "dismiss"] as const

const statusColors: Record<string, string> = {
  open: "bg-blue-50 text-blue-700 border-blue-200",
  investigating: "bg-yellow-50 text-yellow-700 border-yellow-200",
  resolved: "bg-green-50 text-green-700 border-green-200",
  dismissed: "bg-gray-50 text-gray-700 border-gray-200",
  escalated: "bg-red-50 text-red-700 border-red-200",
}

const priorityColors: Record<string, string> = {
  low: "bg-gray-50 text-gray-700 border-gray-200",
  medium: "bg-blue-50 text-blue-700 border-blue-200",
  high: "bg-orange-50 text-orange-700 border-orange-200",
  urgent: "bg-red-50 text-red-700 border-red-200",
}

interface IssueRowItem {
  id: string
  title: string
  type: string
  projectTitle: string
  status: string
  priority: string
  createdAt: string
}

interface IssueRowProps {
  issue: IssueRowItem
  onView: (issueId: string) => void
  onAssignToMe: (issueId: string) => void
  onEscalate: (issueId: string) => void
  onResolve: (issueId: string) => void
}

function IssueRow({ issue, onView, onAssignToMe, onEscalate, onResolve }: IssueRowProps) {
  const handleView = useCallback(() => onView(issue.id), [onView, issue.id])
  const handleAssignToMe = useCallback(() => onAssignToMe(issue.id), [onAssignToMe, issue.id])
  const handleEscalate = useCallback(() => onEscalate(issue.id), [onEscalate, issue.id])
  const handleResolve = useCallback(() => onResolve(issue.id), [onResolve, issue.id])

  return (
    <TableRow key={issue.id}>
      <TableCell className="font-medium max-w-[200px] truncate">{issue.title}</TableCell>
      <TableCell>
        <Badge variant="outline">{issue.type}</Badge>
      </TableCell>
      <TableCell className="max-w-[150px] truncate">{issue.projectTitle}</TableCell>
      <TableCell>
        <Badge variant="outline" className={statusColors[issue.status] ?? ""}>
          {issue.status}
        </Badge>
      </TableCell>
      <TableCell>
        <Badge variant="outline" className={priorityColors[issue.priority] ?? ""}>
          {issue.priority}
        </Badge>
      </TableCell>
      <TableCell>{new Date(issue.createdAt).toLocaleDateString()}</TableCell>
      <TableCell>
        <div className="flex gap-1">
          <Button size="sm" variant="ghost" onClick={handleView}>
            <Eye className="h-4 w-4" />
          </Button>
          <Button size="sm" variant="ghost" onClick={handleAssignToMe}>
            <UserPlus className="h-4 w-4" />
          </Button>
          <Button size="sm" variant="ghost" onClick={handleEscalate}>
            <AlertTriangle className="h-4 w-4" />
          </Button>
          <Button
            size="sm"
            variant="ghost"
            className="text-green-600"
            onClick={handleResolve}
          >
            <CheckCircle className="h-4 w-4" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  )
}

interface IssueDetailItem {
  title: string; type: string; status: string; priority: string
  description?: string | null; adminResolution?: string | null
  project?: { title?: string } | null
  reporter?: { fullName?: string; email?: string } | null
  relatedUser?: { fullName?: string } | null
  assignedToAdmin?: { fullName?: string } | null
}

function IssueFilters({ statusFilter, typeFilter, onStatus, onType, t }: {
  statusFilter?: string; typeFilter?: string
  onStatus: (v: string) => void; onType: (v: string) => void
  t: (key: string) => string
}) {
  return (
    <div className="flex gap-4">
      <Select value={statusFilter ?? "all"} onValueChange={onStatus}>
        <SelectTrigger className="w-[180px]"><SelectValue placeholder={t("filterByStatus")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allStatus")}</SelectItem>
          {ISSUE_STATUSES.map((s) => <SelectItem key={s} value={s}>{t(`issueStatus_${s}`)}</SelectItem>)}
        </SelectContent>
      </Select>
      <Select value={typeFilter ?? "all"} onValueChange={onType}>
        <SelectTrigger className="w-[180px]"><SelectValue placeholder={t("filterByType")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allTypes")}</SelectItem>
          {ISSUE_TYPES.map((tp) => <SelectItem key={tp} value={tp}>{t(`issueType_${tp}`)}</SelectItem>)}
        </SelectContent>
      </Select>
    </div>
  )
}

function IssueDetailDialog({ issue, isLoading, open, onClose, t }: {
  issue?: IssueDetailItem | null; isLoading: boolean; open: boolean
  onClose: (open: boolean) => void; t: (key: string) => string
}) {
  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t("issueDetails")}</DialogTitle>
          <DialogDescription className="sr-only">{t("issueDetails")}</DialogDescription>
        </DialogHeader>
        {isLoading ? (
          <div className="flex justify-center p-4"><Loader2 className="h-6 w-6 animate-spin" /></div>
        ) : issue ? (
          <div className="space-y-3 text-sm">
            <div><span className="font-medium">{t("issueTitle")}:</span> {issue.title}</div>
            <div><span className="font-medium">{t("type")}:</span> {issue.type}</div>
            <div><span className="font-medium">{t("status")}:</span> {issue.status}</div>
            <div><span className="font-medium">{t("priority")}:</span> {issue.priority}</div>
            <div><span className="font-medium">{t("project")}:</span> {issue.project?.title}</div>
            <div><span className="font-medium">{t("reporter")}:</span> {issue.reporter?.fullName} ({issue.reporter?.email})</div>
            {issue.relatedUser && <div><span className="font-medium">{t("relatedUser")}:</span> {issue.relatedUser.fullName}</div>}
            {issue.assignedToAdmin && <div><span className="font-medium">{t("assignedTo")}:</span> {issue.assignedToAdmin.fullName}</div>}
            <div>
              <span className="font-medium">{t("description")}:</span>
              <p className="mt-1 text-muted-foreground whitespace-pre-wrap">{issue.description}</p>
            </div>
            {issue.adminResolution && (
              <div>
                <span className="font-medium">{t("resolution")}:</span>
                <p className="mt-1 text-muted-foreground">{issue.adminResolution}</p>
              </div>
            )}
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  )
}

function IssueResolveDialog({ open, resolution, actionTaken, isPending, onClose, onResolutionChange, onActionChange, onResolve, t }: {
  open: boolean; resolution: string; actionTaken: string; isPending: boolean
  onClose: (open: boolean) => void
  onResolutionChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void
  onActionChange: (v: string) => void
  onResolve: () => void; t: (key: string) => string
}) {
  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("resolveIssue")}</DialogTitle>
          <DialogDescription className="sr-only">{t("resolveIssue")}</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("actionTaken")}</label>
            <Select value={actionTaken} onValueChange={onActionChange}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {ISSUE_ACTIONS.map((a) => <SelectItem key={a} value={a}>{t(`issueAction_${a}`)}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("resolution")}</label>
            <Textarea placeholder={t("resolutionPlaceholder")} value={resolution} onChange={onResolutionChange} />
          </div>
        </div>
        <DialogFooter>
          <Button onClick={onResolve} disabled={!resolution || isPending}>
            {isPending ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : null}
            {t("resolveIssue")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function ProjectIssuesManagement() {
  const t = useTranslations("admin")
  const { toast } = useToast()
  const { user } = useCurrentUser()

  const [page, setPage] = useState(1)
  const [statusFilter, setStatusFilter] = useState<string | undefined>(undefined)
  const [typeFilter, setTypeFilter] = useState<string | undefined>(undefined)

  // Detail dialog
  const [selectedIssueId, setSelectedIssueId] = useState<string | null>(null)
  // Resolve dialog
  const [resolveIssueId, setResolveIssueId] = useState<string | null>(null)
  const [resolution, setResolution] = useState("")
  const [actionTaken, setActionTaken] = useState<string>("dismiss")

  const { data, isLoading } = useAdminIssues(statusFilter, typeFilter, page)
  const { data: issueDetail, isLoading: detailLoading } = useAdminIssueDetails(selectedIssueId ?? "")
  const assignIssue = useAssignIssue()
  const resolveIssue = useResolveIssue()
  const escalateIssue = useEscalateIssue()

  const handleAssignToMe = useCallback(async (issueId: string) => {
    if (!user?.id) return
    try {
      await assignIssue.mutateAsync({ issueId, adminUserId: user.id })
      toast({ title: t("issueAssigned") })
    } catch {
      toast({ title: t("issueAssignFailed"), variant: "destructive" })
    }
  }, [assignIssue, user, toast, t])

  const handleEscalate = useCallback(async (issueId: string) => {
    try {
      await escalateIssue.mutateAsync(issueId)
      toast({ title: t("issueEscalated") })
    } catch {
      toast({ title: t("issueEscalateFailed"), variant: "destructive" })
    }
  }, [escalateIssue, toast, t])

  const handleResolve = useCallback(async () => {
    if (!resolveIssueId || !resolution) return
    try {
      await resolveIssue.mutateAsync({
        issueId: resolveIssueId,
        resolution,
        actionTaken: actionTaken as "warn" | "suspend" | "remove" | "dismiss",
      })
      toast({ title: t("issueResolved") })
      setResolveIssueId(null)
      setResolution("")
      setActionTaken("dismiss")
    } catch {
      toast({ title: t("issueResolveFailed"), variant: "destructive" })
    }
  }, [resolveIssueId, resolution, actionTaken, resolveIssue, toast, t])

  const handleStatusFilterChange = useCallback((v: string) => setStatusFilter(v === "all" ? undefined : v), [])
  const handleTypeFilterChange = useCallback((v: string) => setTypeFilter(v === "all" ? undefined : v), [])

  const handlePrevPage = useCallback(() => setPage((p) => p - 1), [])
  const handleNextPage = useCallback(() => setPage((p) => p + 1), [])

  const handleCloseDetailDialog = useCallback((open: boolean) => { if (!open) setSelectedIssueId(null) }, [])
  const handleCloseResolveDialog = useCallback((open: boolean) => { if (!open) setResolveIssueId(null) }, [])
  const handleResolutionChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setResolution(e.target.value), [])

  const handleRowView = useCallback((issueId: string) => setSelectedIssueId(issueId), [])
  const handleRowResolve = useCallback((issueId: string) => { setResolveIssueId(issueId); setResolution(""); setActionTaken("dismiss") }, [])

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin" />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <IssueFilters statusFilter={statusFilter} typeFilter={typeFilter} onStatus={handleStatusFilterChange} onType={handleTypeFilterChange} t={t} />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("issueTitle")}</TableHead>
              <TableHead>{t("type")}</TableHead>
              <TableHead>{t("project")}</TableHead>
              <TableHead>{t("status")}</TableHead>
              <TableHead>{t("priority")}</TableHead>
              <TableHead>{t("reportDate")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data?.data?.map((issue) => (
              <IssueRow key={issue.id} issue={issue} onView={handleRowView} onAssignToMe={handleAssignToMe} onEscalate={handleEscalate} onResolve={handleRowResolve} />
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="flex justify-center gap-2">
        <Button variant="outline" disabled={page === 1} onClick={handlePrevPage}>{t("previous")}</Button>
        <Button variant="outline" disabled={!data || (data.data?.length ?? 0) < 20} onClick={handleNextPage}>{t("next")}</Button>
      </div>

      <IssueDetailDialog issue={issueDetail} isLoading={detailLoading} open={!!selectedIssueId} onClose={handleCloseDetailDialog} t={t} />
      <IssueResolveDialog
        open={!!resolveIssueId}
        resolution={resolution}
        actionTaken={actionTaken}
        isPending={resolveIssue.isPending}
        onClose={handleCloseResolveDialog}
        onResolutionChange={handleResolutionChange}
        onActionChange={setActionTaken}
        onResolve={handleResolve}
        t={t}
      />
    </div>
  )
}
