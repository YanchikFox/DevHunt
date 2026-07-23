"use client"

import { useState, useCallback } from "react"
import {
  useAdminChatStats,
  useAdminConversations,
  useAdminConversationMessages,
  useDeleteConversation,
  useDeleteChatMessage,
  useCleanupOrphanedChats,
  type AdminConversation,
} from "@/lib/api/queries/admin-chat"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Trash2, Eye, AlertTriangle, MessageSquare, Users, MessagesSquare } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

const typeColors: Record<string, string> = {
  Direct: "bg-blue-50 text-blue-700 border-blue-200",
  Group: "bg-purple-50 text-purple-700 border-purple-200",
}

// ── MessageRow sub-component ─────────────────────────────────────────────────

interface MessageRowProps {
  msg: {
    id: string
    senderName: string
    createdAt: string
    isAiGenerated: boolean
    isDeleted: boolean
    content: string
  }
  onDelete: (messageId: string) => void
  t: (key: string) => string
}

function MessageRow({ msg, onDelete, t }: MessageRowProps) {
  const handleDelete = useCallback(() => onDelete(msg.id), [onDelete, msg.id])

  return (
    <div className="flex items-start gap-3 p-3 rounded-lg bg-muted/50">
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 mb-1">
          <span className="font-medium text-sm">{msg.senderName}</span>
          <span className="text-xs text-muted-foreground">
            {new Date(msg.createdAt).toLocaleString()}
          </span>
          {msg.isAiGenerated && <Badge variant="outline" className="text-xs">AI</Badge>}
          {msg.isDeleted && <Badge variant="destructive" className="text-xs">{t("deleted")}</Badge>}
        </div>
        <p className="text-sm break-words">{msg.content}</p>
      </div>
      <Button
        variant="ghost"
        size="icon"
        className="shrink-0 h-7 w-7"
        onClick={handleDelete}
      >
        <Trash2 className="h-3.5 w-3.5 text-destructive" />
      </Button>
    </div>
  )
}

// ── State hook ───────────────────────────────────────────────────────────────

function useChatManagementState() {
  const t = useTranslations("admin")
  const { toast } = useToast()
  const [page, setPage] = useState(1)
  const [typeFilter, setTypeFilter] = useState("all")
  const [searchQuery, setSearchQuery] = useState("")
  const [orphanedOnly, setOrphanedOnly] = useState(false)
  const [viewConversation, setViewConversation] = useState<AdminConversation | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<AdminConversation | null>(null)
  const [showCleanupConfirm, setShowCleanupConfirm] = useState(false)
  const [messagesPage, setMessagesPage] = useState(1)

  const { data: stats } = useAdminChatStats()
  const { data: conversationsData, isLoading } = useAdminConversations(page, 20, { type: typeFilter, search: searchQuery || undefined, orphaned: orphanedOnly || undefined })
  const { data: messagesData, isLoading: messagesLoading } = useAdminConversationMessages(viewConversation?.id ?? null, messagesPage)
  const deleteConversation = useDeleteConversation()
  const deleteMessage = useDeleteChatMessage()
  const cleanupOrphaned = useCleanupOrphanedChats()

  const handleDelete = useCallback(async () => {
    if (!deleteTarget) return
    try { await deleteConversation.mutateAsync(deleteTarget.id); toast({ title: t("chatDeleted") }); setDeleteTarget(null) }
    catch { toast({ title: t("error"), variant: "destructive" }) }
  }, [deleteTarget, deleteConversation, toast, t])

  const handleDeleteMessage = useCallback(async (messageId: string) => {
    if (!viewConversation) return
    try { await deleteMessage.mutateAsync({ conversationId: viewConversation.id, messageId }); toast({ title: t("messageDeleted") }) }
    catch { toast({ title: t("error"), variant: "destructive" }) }
  }, [viewConversation, deleteMessage, toast, t])

  const handleCleanup = useCallback(async () => {
    try {
      const result = await cleanupOrphaned.mutateAsync()
      toast({ title: t("cleanupComplete"), description: `${result.deletedCount} conversations, ${result.deletedMessages} messages deleted` })
      setShowCleanupConfirm(false)
    } catch { toast({ title: t("error"), variant: "destructive" }) }
  }, [cleanupOrphaned, toast, t])

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => { setSearchQuery(e.target.value); setPage(1) }, [])
  const handleTypeChange = useCallback((value: string) => { setTypeFilter(value); setOrphanedOnly(value === "orphaned"); setPage(1) }, [])
  const handlePrevPage = useCallback(() => setPage(p => p - 1), [])
  const handleNextPage = useCallback(() => setPage(p => p + 1), [])
  const handlePrevMessagesPage = useCallback(() => setMessagesPage(p => p - 1), [])
  const handleNextMessagesPage = useCallback(() => setMessagesPage(p => p + 1), [])
  const handleCloseViewDialog = useCallback((open: boolean) => { if (!open) { setViewConversation(null); setMessagesPage(1) } }, [])
  const handleCloseDeleteDialog = useCallback((open: boolean) => { if (!open) setDeleteTarget(null) }, [])
  const handleOpenCleanupConfirm = useCallback(() => setShowCleanupConfirm(true), [])
  const handleCancelCleanup = useCallback(() => setShowCleanupConfirm(false), [])
  const handleCancelDelete = useCallback(() => setDeleteTarget(null), [])

  return {
    t, stats, isLoading, page, conversations: conversationsData?.data ?? [], pagination: conversationsData?.pagination,
    viewConversation, setViewConversation, deleteTarget, setDeleteTarget, showCleanupConfirm, setShowCleanupConfirm, messagesPage,
    messagesData, messagesLoading, deleteConversation, cleanupOrphaned,
    handleDelete, handleDeleteMessage, handleCleanup, handleSearchChange, handleTypeChange,
    handlePrevPage, handleNextPage, handlePrevMessagesPage, handleNextMessagesPage,
    handleCloseViewDialog, handleCloseDeleteDialog, handleOpenCleanupConfirm, handleCancelCleanup, handleCancelDelete,
    searchQuery, typeFilter,
  }
}

// ── Dialogs sub-component ─────────────────────────────────────────────────────

type ChatManagementDialogsProps = ReturnType<typeof useChatManagementState>

function ChatManagementDialogs(s: ChatManagementDialogsProps) {
  const { t } = s
  return (
    <>
      <Dialog open={s.viewConversation !== null} onOpenChange={s.handleCloseViewDialog}>
        <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{s.viewConversation?.title || `Conversation ${s.viewConversation?.id?.slice(0, 8)}`}</DialogTitle>
            <DialogDescription>{s.viewConversation?.type} &middot; {s.viewConversation?.messageCount} {t("messages")}</DialogDescription>
          </DialogHeader>
          {s.messagesLoading ? (
            <div className="flex justify-center py-4"><Loader2 className="h-5 w-5 animate-spin" /></div>
          ) : (
            <div className="space-y-3 max-h-96 overflow-y-auto">
              {(s.messagesData?.data ?? []).map((msg) => (
                <MessageRow key={msg.id} msg={msg} onDelete={s.handleDeleteMessage} t={t} />
              ))}
              {(s.messagesData?.data ?? []).length === 0 && (
                <p className="text-center text-muted-foreground py-4">{t("noMessages")}</p>
              )}
            </div>
          )}
          {s.messagesData?.pagination && s.messagesData.pagination.totalPages > 1 && (
            <div className="flex justify-center gap-2 pt-2">
              <Button size="sm" variant="outline" disabled={s.messagesPage <= 1} onClick={s.handlePrevMessagesPage}>{t("prev")}</Button>
              <span className="text-sm self-center">{s.messagesPage} / {s.messagesData.pagination.totalPages}</span>
              <Button size="sm" variant="outline" disabled={s.messagesPage >= s.messagesData.pagination.totalPages} onClick={s.handleNextMessagesPage}>{t("next")}</Button>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={s.deleteTarget !== null} onOpenChange={s.handleCloseDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("deleteConversation")}</DialogTitle>
            <DialogDescription>{t("deleteConversationConfirm", { title: s.deleteTarget?.title || s.deleteTarget?.id?.slice(0, 8) || "" })}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={s.handleCancelDelete}>{t("cancel")}</Button>
            <Button variant="destructive" onClick={s.handleDelete} disabled={s.deleteConversation.isPending}>
              {s.deleteConversation.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("delete")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={s.showCleanupConfirm} onOpenChange={s.setShowCleanupConfirm}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("cleanupOrphaned")}</DialogTitle>
            <DialogDescription>{t("cleanupOrphanedConfirm", { count: s.stats?.orphaned ?? 0 })}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={s.handleCancelCleanup}>{t("cancel")}</Button>
            <Button variant="destructive" onClick={s.handleCleanup} disabled={s.cleanupOrphaned.isPending}>
              {s.cleanupOrphaned.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("confirmCleanup")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

// ── Main component ───────────────────────────────────────────────────────────

export function ChatManagement() {
  const s = useChatManagementState()
  const { t, stats, isLoading, conversations, pagination } = s

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <StatCard icon={<MessagesSquare className="h-5 w-5" />} label={t("totalConversations")} value={stats?.total ?? 0} />
        <StatCard icon={<Users className="h-5 w-5" />} label={t("groupChats")} value={stats?.groups ?? 0} />
        <StatCard icon={<MessageSquare className="h-5 w-5" />} label={t("totalMessages")} value={stats?.totalMessages ?? 0} />
        <StatCard icon={<AlertTriangle className="h-5 w-5 text-red-500" />} label={t("orphanedChats")} value={stats?.orphaned ?? 0} variant="destructive" />
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Input placeholder={t("searchByTitle")} value={s.searchQuery} onChange={s.handleSearchChange} className="w-64" />
        <Select value={s.typeFilter} onValueChange={s.handleTypeChange}>
          <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("allTypes")}</SelectItem>
            <SelectItem value="Direct">{t("direct")}</SelectItem>
            <SelectItem value="Group">{t("group")}</SelectItem>
            <SelectItem value="orphaned">{t("orphanedOnly")}</SelectItem>
          </SelectContent>
        </Select>
        {(stats?.orphaned ?? 0) > 0 && (
          <Button variant="destructive" size="sm" onClick={s.handleOpenCleanupConfirm}>
            <Trash2 className="h-4 w-4 mr-1" />
            {t("cleanupOrphaned")} ({stats?.orphaned})
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="flex justify-center py-8"><Loader2 className="h-6 w-6 animate-spin" /></div>
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("title")}</TableHead><TableHead>{t("type")}</TableHead>
              <TableHead>{t("participants")}</TableHead><TableHead>{t("messages")}</TableHead>
              <TableHead>{t("lastMessage")}</TableHead><TableHead>{t("status")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {conversations.map((conv) => (
              <ConversationRow key={conv.id} conversation={conv} onView={s.setViewConversation} onDelete={s.setDeleteTarget} t={t} />
            ))}
            {conversations.length === 0 && (
              <TableRow><TableCell colSpan={7} className="text-center text-muted-foreground py-8">{t("noConversations")}</TableCell></TableRow>
            )}
          </TableBody>
        </Table>
      )}

      {pagination && pagination.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">
            {t("showing")} {(s.page - 1) * 20 + 1}-{Math.min(s.page * 20, pagination.total)} {t("of")} {pagination.total}
          </span>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" disabled={s.page <= 1} onClick={s.handlePrevPage}>{t("prev")}</Button>
            <Button size="sm" variant="outline" disabled={s.page >= pagination.totalPages} onClick={s.handleNextPage}>{t("next")}</Button>
          </div>
        </div>
      )}

      <ChatManagementDialogs {...s} />
    </div>
  )
}

// Sub-components

interface StatCardProps {
  icon: React.ReactNode
  label: string
  value: number
  variant?: "default" | "destructive"
}

function StatCard({ icon, label, value, variant = "default" }: StatCardProps) {
  return (
    <div className={`rounded-lg border p-4 ${variant === "destructive" ? "border-red-200 bg-red-50/50" : "bg-card"}`}>
      <div className="flex items-center gap-2 mb-1">
        {icon}
        <span className="text-sm text-muted-foreground">{label}</span>
      </div>
      <span className="text-2xl font-bold">{value.toLocaleString()}</span>
    </div>
  )
}

interface ConversationRowProps {
  conversation: AdminConversation
  onView: (c: AdminConversation) => void
  onDelete: (c: AdminConversation) => void
  t: (key: string) => string
}

function ConversationRow({ conversation, onView, onDelete, t }: ConversationRowProps) {
  const handleView = useCallback(() => onView(conversation), [onView, conversation])
  const handleDelete = useCallback(() => onDelete(conversation), [onDelete, conversation])

  return (
    <TableRow>
      <TableCell className="font-medium max-w-[200px] truncate">
        {conversation.title || conversation.id.slice(0, 8) + "..."}
      </TableCell>
      <TableCell>
        <Badge variant="outline" className={typeColors[conversation.type] || ""}>
          {conversation.type}
        </Badge>
      </TableCell>
      <TableCell>{conversation.participantCount}</TableCell>
      <TableCell>{conversation.messageCount}</TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {conversation.lastMessageAt ? new Date(conversation.lastMessageAt).toLocaleDateString() : "—"}
      </TableCell>
      <TableCell>
        {conversation.isOrphaned && (
          <Badge variant="destructive" className="text-xs">
            {t("orphaned")}
          </Badge>
        )}
      </TableCell>
      <TableCell>
        <div className="flex gap-1">
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleView}>
            <Eye className="h-4 w-4" />
          </Button>
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleDelete}>
            <Trash2 className="h-4 w-4 text-destructive" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  )
}
