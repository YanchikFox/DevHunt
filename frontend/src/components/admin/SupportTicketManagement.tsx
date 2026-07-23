"use client"

import { useState, useCallback } from "react"
import {
  useAdminTickets,
  useAdminSupportStats,
  useAssignTicket,
  useResolveTicket,
  useUpdateTicketPriority,
  useEscalateTicket,
  useAddTicketMessage,
  useTicketDetails,
  useTicketHistory,
  type AdminTicketFilters,
} from "@/lib/api/queries/support"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Eye, UserPlus, AlertTriangle, CheckCircle, MessageSquare, Send } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { useCurrentUser } from "@/hooks/use-current-user"

const TICKET_STATUSES = ["open", "in_progress", "waiting_user", "resolved", "closed"] as const
const TICKET_PRIORITIES = ["low", "medium", "high", "urgent"] as const
const TICKET_CATEGORIES = ["question", "bug", "feature", "billing", "other"] as const

const statusColors: Record<string, string> = {
  open: "bg-blue-50 text-blue-700 border-blue-200",
  in_progress: "bg-yellow-50 text-yellow-700 border-yellow-200",
  waiting_user: "bg-purple-50 text-purple-700 border-purple-200",
  resolved: "bg-green-50 text-green-700 border-green-200",
  closed: "bg-gray-50 text-gray-700 border-gray-200",
}

const priorityColors: Record<string, string> = {
  low: "bg-gray-50 text-gray-700 border-gray-200",
  medium: "bg-blue-50 text-blue-700 border-blue-200",
  high: "bg-orange-50 text-orange-700 border-orange-200",
  urgent: "bg-red-50 text-red-700 border-red-200",
}

interface TicketRowItem {
  id: string
  subject: string
  category: string
  status: string
  priority: string
  userEmail?: string
  assignedToName?: string | null
  messageCount: number
}

interface TicketRowProps {
  ticket: TicketRowItem
  onPriorityChange: (ticketId: string, priority: string) => void
  onView: (ticketId: string) => void
  onAssignToMe: (ticketId: string) => void
  onEscalate: (ticketId: string) => void
  onResolve: (ticketId: string) => void
}

function TicketRow({ ticket, onPriorityChange, onView, onAssignToMe, onEscalate, onResolve }: TicketRowProps) {
  const handlePriorityChange = useCallback((v: string) => onPriorityChange(ticket.id, v), [onPriorityChange, ticket.id])
  const handleView = useCallback(() => onView(ticket.id), [onView, ticket.id])
  const handleAssignToMe = useCallback(() => onAssignToMe(ticket.id), [onAssignToMe, ticket.id])
  const handleEscalate = useCallback(() => onEscalate(ticket.id), [onEscalate, ticket.id])
  const handleResolve = useCallback(() => onResolve(ticket.id), [onResolve, ticket.id])

  return (
    <TableRow key={ticket.id}>
      <TableCell className="font-medium max-w-[200px] truncate">{ticket.subject}</TableCell>
      <TableCell>
        <Badge variant="outline">{ticket.category}</Badge>
      </TableCell>
      <TableCell>
        <Badge variant="outline" className={statusColors[ticket.status] ?? ""}>
          {ticket.status}
        </Badge>
      </TableCell>
      <TableCell>
        <Select
          defaultValue={ticket.priority}
          onValueChange={handlePriorityChange}
        >
          <SelectTrigger className="h-7 w-[100px]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {TICKET_PRIORITIES.map((p) => (
              <SelectItem key={p} value={p}>{p}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </TableCell>
      <TableCell className="text-xs">{ticket.userEmail}</TableCell>
      <TableCell className="text-xs">{ticket.assignedToName ?? "—"}</TableCell>
      <TableCell>
        <Badge variant="secondary">{ticket.messageCount}</Badge>
      </TableCell>
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

interface SupportFiltersRowProps {
  filters: AdminTicketFilters
  onStatusChange: (v: string) => void
  onPriorityChange: (v: string) => void
  onCategoryChange: (v: string) => void
  t: ReturnType<typeof useTranslations>
}

function SupportFiltersRow({ filters, onStatusChange, onPriorityChange, onCategoryChange, t }: SupportFiltersRowProps) {
  return (
    <div className="flex flex-wrap gap-3">
      <Select value={filters.status ?? "all"} onValueChange={onStatusChange}>
        <SelectTrigger className="w-[160px]"><SelectValue placeholder={t("filterByStatus")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allStatus")}</SelectItem>
          {TICKET_STATUSES.map((s) => <SelectItem key={s} value={s}>{t(`ticketStatus_${s}`)}</SelectItem>)}
        </SelectContent>
      </Select>
      <Select value={filters.priority ?? "all"} onValueChange={onPriorityChange}>
        <SelectTrigger className="w-[160px]"><SelectValue placeholder={t("filterByPriority")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allPriorities")}</SelectItem>
          {TICKET_PRIORITIES.map((p) => <SelectItem key={p} value={p}>{t(`priority_${p}`)}</SelectItem>)}
        </SelectContent>
      </Select>
      <Select value={filters.category ?? "all"} onValueChange={onCategoryChange}>
        <SelectTrigger className="w-[160px]"><SelectValue placeholder={t("filterByCategory")} /></SelectTrigger>
        <SelectContent>
          <SelectItem value="all">{t("allCategories")}</SelectItem>
          {TICKET_CATEGORIES.map((c) => <SelectItem key={c} value={c}>{t(`category_${c}`)}</SelectItem>)}
        </SelectContent>
      </Select>
    </div>
  )
}

interface TicketDetailDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  ticketDetail: ReturnType<typeof useTicketDetails>["data"]
  ticketHistory: ReturnType<typeof useTicketHistory>["data"]
  detailLoading: boolean
  replyContent: string
  replyInternal: boolean
  addMessagePending: boolean
  onReplyContentChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void
  onReplyInternalChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  onReply: () => void
  t: ReturnType<typeof useTranslations>
}

function TicketDetailDialog({ open, onOpenChange, ticketDetail, ticketHistory, detailLoading, replyContent, replyInternal, addMessagePending, onReplyContentChange, onReplyInternalChange, onReply, t }: TicketDetailDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <MessageSquare className="h-5 w-5" />{t("ticketDetails")}
          </DialogTitle>
          <DialogDescription className="sr-only">{t("ticketDetails")}</DialogDescription>
        </DialogHeader>
        {detailLoading ? (
          <div className="flex justify-center p-4"><Loader2 className="h-6 w-6 animate-spin" /></div>
        ) : ticketDetail ? (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div><span className="font-medium">{t("subject")}:</span> {ticketDetail.subject}</div>
              <div><span className="font-medium">{t("category")}:</span> {ticketDetail.category}</div>
              <div><span className="font-medium">{t("status")}:</span> <Badge variant="outline" className={statusColors[ticketDetail.status] ?? ""}>{ticketDetail.status}</Badge></div>
              <div><span className="font-medium">{t("priority")}:</span> <Badge variant="outline" className={priorityColors[ticketDetail.priority] ?? ""}>{ticketDetail.priority}</Badge></div>
            </div>
            <div className="border rounded-lg p-3">
              <p className="text-sm font-medium mb-1">{t("description")}:</p>
              <p className="text-sm text-muted-foreground whitespace-pre-wrap">{ticketDetail.description}</p>
            </div>
            <div className="space-y-3">
              <h4 className="text-sm font-medium">{t("conversation")}</h4>
              {ticketDetail.messages?.map((msg) => (
                <div key={msg.id} className={`border rounded-lg p-3 text-sm ${msg.isInternal ? "bg-yellow-50 dark:bg-yellow-950 border-yellow-200 dark:border-yellow-800" : ""}`}>
                  <div className="flex items-center justify-between mb-1">
                    <span className="font-medium">
                      {msg.authorName}
                      {msg.isInternal && <Badge variant="outline" className="ml-2 text-xs bg-yellow-100 text-yellow-700 border-yellow-300">{t("internalNote")}</Badge>}
                    </span>
                    <span className="text-xs text-muted-foreground">{new Date(msg.createdAt).toLocaleString()}</span>
                  </div>
                  <p className="whitespace-pre-wrap">{msg.content}</p>
                </div>
              ))}
            </div>
            <div className="space-y-2 border-t pt-4">
              <Textarea placeholder={t("replyPlaceholder")} value={replyContent} onChange={onReplyContentChange} rows={3} />
              <div className="flex items-center justify-between">
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={replyInternal} onChange={onReplyInternalChange} className="rounded border-gray-300" />
                  {t("internalNote")}
                </label>
                <Button size="sm" onClick={onReply} disabled={!replyContent.trim() || addMessagePending}>
                  {addMessagePending ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : <Send className="h-4 w-4 mr-1" />}
                  {t("sendReply")}
                </Button>
              </div>
            </div>
            {ticketHistory?.history && ticketHistory.history.length > 0 && (
              <div className="space-y-2 border-t pt-4">
                <h4 className="text-sm font-medium">{t("changeHistory")}</h4>
                {ticketHistory.history.map((entry) => (
                  <div key={entry.id} className="text-xs text-muted-foreground flex items-center gap-2">
                    <span>{new Date(entry.createdAt).toLocaleString()}</span>
                    <span className="font-medium">{entry.changeType}:</span>
                    {entry.oldValue && <span>{entry.oldValue} →</span>}
                    <span>{entry.newValue}</span>
                    {entry.reason && <span className="italic">({entry.reason})</span>}
                  </div>
                ))}
              </div>
            )}
          </div>
        ) : null}
      </DialogContent>
    </Dialog>
  )
}

interface ResolveTicketDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  resolutionText: string
  isPending: boolean
  onResolutionChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void
  onResolve: () => void
  t: ReturnType<typeof useTranslations>
}

function ResolveTicketDialog({ open, onOpenChange, resolutionText, isPending, onResolutionChange, onResolve, t }: ResolveTicketDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("resolveTicket")}</DialogTitle>
          <DialogDescription className="sr-only">{t("resolutionPlaceholder")}</DialogDescription>
        </DialogHeader>
        <div className="py-4">
          <Textarea placeholder={t("resolutionPlaceholder")} value={resolutionText} onChange={onResolutionChange} rows={3} />
        </div>
        <DialogFooter>
          <Button onClick={onResolve} disabled={!resolutionText || isPending}>
            {isPending ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : null}
            {t("resolveTicket")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

export function SupportTicketManagement() {
  const t = useTranslations("admin")
  const { toast } = useToast()
  const { user } = useCurrentUser()

  // Filters
  const [filters, setFilters] = useState<AdminTicketFilters>({ page: 1, pageSize: 20 })

  // Dialogs
  const [viewTicketId, setViewTicketId] = useState<string | null>(null)
  const [resolveTicketId, setResolveTicketId] = useState<string | null>(null)
  const [resolutionText, setResolutionText] = useState("")

  // Reply state
  const [replyContent, setReplyContent] = useState("")
  const [replyInternal, setReplyInternal] = useState(false)

  // Data
  const { data, isLoading } = useAdminTickets(filters)
  const { data: stats } = useAdminSupportStats()
  const { data: ticketDetail, isLoading: detailLoading } = useTicketDetails(viewTicketId ?? "")
  const { data: ticketHistory } = useTicketHistory(viewTicketId ?? "")

  // Mutations
  const assignTicket = useAssignTicket()
  const resolveTicket = useResolveTicket()
  const updatePriority = useUpdateTicketPriority()
  const escalateTicket = useEscalateTicket()
  const addMessage = useAddTicketMessage()

  const handleAssignToMe = useCallback(async (ticketId: string) => {
    if (!user?.id) return
    try {
      await assignTicket.mutateAsync({ ticketId, adminUserId: user.id })
      toast({ title: t("ticketAssigned") })
    } catch {
      toast({ title: t("ticketAssignFailed"), variant: "destructive" })
    }
  }, [assignTicket, user, toast, t])

  const handleEscalate = useCallback(async (ticketId: string) => {
    try {
      await escalateTicket.mutateAsync({ ticketId })
      toast({ title: t("ticketEscalated") })
    } catch {
      toast({ title: t("ticketEscalateFailed"), variant: "destructive" })
    }
  }, [escalateTicket, toast, t])

  const handlePriorityChange = useCallback(async (ticketId: string, priority: string) => {
    try {
      await updatePriority.mutateAsync({ ticketId, priority })
      toast({ title: t("ticketPriorityUpdated") })
    } catch {
      toast({ title: t("ticketPriorityFailed"), variant: "destructive" })
    }
  }, [updatePriority, toast, t])

  const handleResolve = useCallback(async () => {
    if (!resolveTicketId || !resolutionText) return
    try {
      await resolveTicket.mutateAsync({ ticketId: resolveTicketId, resolution: resolutionText })
      toast({ title: t("ticketResolved") })
      setResolveTicketId(null)
      setResolutionText("")
    } catch {
      toast({ title: t("ticketResolveFailed"), variant: "destructive" })
    }
  }, [resolveTicketId, resolutionText, resolveTicket, toast, t])

  const handleReply = useCallback(async () => {
    if (!viewTicketId || !replyContent.trim()) return
    try {
      await addMessage.mutateAsync({ ticketId: viewTicketId, content: replyContent, isInternal: replyInternal })
      toast({ title: t("messageSent") })
      setReplyContent("")
      setReplyInternal(false)
    } catch {
      toast({ title: t("messageFailed"), variant: "destructive" })
    }
  }, [viewTicketId, replyContent, replyInternal, addMessage, toast, t])

  const updateFilter = useCallback((key: keyof AdminTicketFilters, value: string | undefined) => {
    setFilters((prev) => ({ ...prev, [key]: value, page: 1 }))
  }, [])

  const handleStatusFilterChange = useCallback((v: string) => updateFilter("status", v === "all" ? undefined : v), [updateFilter])
  const handlePriorityFilterChange = useCallback((v: string) => updateFilter("priority", v === "all" ? undefined : v), [updateFilter])
  const handleCategoryFilterChange = useCallback((v: string) => updateFilter("category", v === "all" ? undefined : v), [updateFilter])

  const handlePrevPage = useCallback(() => setFilters((f) => ({ ...f, page: (f.page ?? 1) - 1 })), [])
  const handleNextPage = useCallback(() => setFilters((f) => ({ ...f, page: (f.page ?? 1) + 1 })), [])

  const handleCloseViewDialog = useCallback((open: boolean) => { if (!open) setViewTicketId(null) }, [])
  const handleReplyContentChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setReplyContent(e.target.value), [])
  const handleReplyInternalChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => setReplyInternal(e.target.checked), [])

  const handleCloseResolveDialog = useCallback((open: boolean) => { if (!open) setResolveTicketId(null) }, [])
  const handleResolutionTextChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setResolutionText(e.target.value), [])

  const handleRowView = useCallback((ticketId: string) => setViewTicketId(ticketId), [])
  const handleRowResolve = useCallback((ticketId: string) => { setResolveTicketId(ticketId); setResolutionText("") }, [])

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin" />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {stats && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
          {[
            { label: t("ticketOpen"), value: stats.openTickets, color: "text-blue-600" },
            { label: t("ticketInProgress"), value: stats.inProgressTickets, color: "text-yellow-600" },
            { label: t("ticketWaiting"), value: stats.waitingUserTickets, color: "text-purple-600" },
            { label: t("ticketUnassigned"), value: stats.unassignedTickets, color: "text-red-600" },
            { label: t("ticketTotal"), value: stats.totalTickets, color: "text-foreground" },
          ].map((stat) => (
            <div key={stat.label} className="border rounded-lg p-3 text-center">
              <div className={`text-2xl font-bold ${stat.color}`}>{stat.value}</div>
              <div className="text-xs text-muted-foreground">{stat.label}</div>
            </div>
          ))}
        </div>
      )}

      <SupportFiltersRow filters={filters} onStatusChange={handleStatusFilterChange} onPriorityChange={handlePriorityFilterChange} onCategoryChange={handleCategoryFilterChange} t={t} />

      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("subject")}</TableHead><TableHead>{t("category")}</TableHead>
              <TableHead>{t("status")}</TableHead><TableHead>{t("priority")}</TableHead>
              <TableHead>{t("author")}</TableHead><TableHead>{t("assignedTo")}</TableHead>
              <TableHead>{t("messages")}</TableHead><TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data?.data?.map((ticket) => (
              <TicketRow key={ticket.id} ticket={ticket} onPriorityChange={handlePriorityChange} onView={handleRowView} onAssignToMe={handleAssignToMe} onEscalate={handleEscalate} onResolve={handleRowResolve} />
            ))}
          </TableBody>
        </Table>
      </div>

      <div className="flex justify-center gap-2">
        <Button variant="outline" disabled={(filters.page ?? 1) <= 1} onClick={handlePrevPage}>{t("previous")}</Button>
        <Button variant="outline" disabled={!data || (data.data?.length ?? 0) < 20} onClick={handleNextPage}>{t("next")}</Button>
      </div>

      <TicketDetailDialog
        open={!!viewTicketId} onOpenChange={handleCloseViewDialog}
        ticketDetail={ticketDetail} ticketHistory={ticketHistory}
        detailLoading={detailLoading} replyContent={replyContent} replyInternal={replyInternal}
        addMessagePending={addMessage.isPending}
        onReplyContentChange={handleReplyContentChange} onReplyInternalChange={handleReplyInternalChange}
        onReply={handleReply} t={t}
      />
      <ResolveTicketDialog
        open={!!resolveTicketId} onOpenChange={handleCloseResolveDialog}
        resolutionText={resolutionText} isPending={resolveTicket.isPending}
        onResolutionChange={handleResolutionTextChange} onResolve={handleResolve} t={t}
      />
    </div>
  )
}
