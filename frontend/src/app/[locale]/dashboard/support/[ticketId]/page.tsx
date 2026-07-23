"use client"

import { useState, useCallback } from "react"
import { useParams } from "next/navigation"
import {
  useTicketDetails,
  useTicketHistory,
  useAddTicketMessage,
  useCloseTicket,
  useReopenTicket,
} from "@/lib/api/queries/support"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Badge } from "@/components/ui/badge"
import { Loader2, ArrowLeft, Send, Lock, RotateCcw, AlertCircle, CheckCircle2, MessageSquare, History } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { Link } from "@/i18n/routing"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { cn } from "@/lib/utils"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"

const statusColors: Record<string, string> = {
  open: "bg-blue-500/10 text-blue-500 border-blue-500/20",
  in_progress: "bg-yellow-500/10 text-yellow-500 border-yellow-500/20",
  waiting_user: "bg-purple-500/10 text-purple-500 border-purple-500/20",
  resolved: "bg-green-500/10 text-green-500 border-green-500/20",
  closed: "bg-muted text-muted-foreground border-border",
}

const priorityColors: Record<string, string> = {
  low: "bg-muted text-muted-foreground border-border",
  medium: "bg-blue-500/10 text-blue-500 border-blue-500/20",
  high: "bg-orange-500/10 text-orange-500 border-orange-500/20",
  urgent: "bg-red-500/10 text-red-500 border-red-500/20",
}

export default function TicketDetailPage() {
  const params = useParams()
  const ticketId = params.ticketId as string
  const t = useTranslations("support")
  const { toast } = useToast()

  const { data: ticket, isLoading } = useTicketDetails(ticketId)
  const { data: history } = useTicketHistory(ticketId)
  const addMessage = useAddTicketMessage()
  const closeTicket = useCloseTicket()
  const reopenTicket = useReopenTicket()

  const [replyContent, setReplyContent] = useState("")

  const handleReply = useCallback(async () => {
    if (!replyContent.trim()) return
    try {
      await addMessage.mutateAsync({ ticketId, content: replyContent })
      toast({ title: t("messageSent") })
      setReplyContent("")
    } catch {
      toast({ title: t("messageFailed"), variant: "destructive" })
    }
  }, [ticketId, replyContent, addMessage, toast, t])

  const handleClose = useCallback(async () => {
    try {
      await closeTicket.mutateAsync(ticketId)
      toast({ title: t("ticketClosed") })
    } catch {
      toast({ title: t("ticketCloseFailed"), variant: "destructive" })
    }
  }, [ticketId, closeTicket, toast, t])

  const handleReopen = useCallback(async () => {
    try {
      await reopenTicket.mutateAsync({ ticketId })
      toast({ title: t("ticketReopened") })
    } catch {
      toast({ title: t("ticketReopenFailed"), variant: "destructive" })
    }
  }, [ticketId, reopenTicket, toast, t])

  const handleReplyContentChange = useCallback((e: { target: { value: string } }) => setReplyContent(e.target.value), [])

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-[400px]">
        <Loader2 className="h-8 w-8 animate-spin text-primary" />
      </div>
    )
  }

  if (!ticket) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px] text-muted-foreground">
        <AlertCircle className="h-12 w-12 mb-4 opacity-20" />
        <p className="text-lg font-medium">{t("ticketNotFound")}</p>
        <Link href="/dashboard/support" className="mt-4">
          <Button variant="outline">{t("backToTickets")}</Button>
        </Link>
      </div>
    )
  }

  const canReply = ticket.status !== "closed"
  const canClose = ticket.status !== "closed" && ticket.status !== "resolved"
  const canReopen = ticket.status === "closed" || ticket.status === "resolved"

  return (
    <div className="space-y-6 animate-in fade-in slide-in-from-bottom-4 duration-700">
      {/* Header Section */}
      <div className="flex flex-wrap items-start gap-4">
        <Link href="/dashboard/support" className="shrink-0">
          <Button variant="ghost" size="icon" className="h-10 w-10 rounded-full hover:bg-background/80" aria-label={t("backToTickets")}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
        </Link>
        <div className="min-w-0 flex-1">
          <h1 className="text-2xl font-bold text-foreground line-clamp-2 break-words" title={ticket.subject}>
            {ticket.subject}
          </h1>
          <div className="flex items-center gap-2 text-sm text-muted-foreground mt-1 flex-wrap">
            <span>#{ticket.id.slice(0, 8)}</span>
            <span aria-hidden="true">•</span>
            <span>{new Date(ticket.createdAt).toLocaleDateString()}</span>
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0 flex-wrap">
          <Badge variant="outline" className={cn("px-3 py-1 capitalize", statusColors[ticket.status])}>
            {ticket.status.replace("_", " ")}
          </Badge>
          <Badge variant="outline" className={cn("px-3 py-1 capitalize", priorityColors[ticket.priority])}>
            {ticket.priority}
          </Badge>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Main Chat Area */}
        <div className="md:col-span-2 space-y-6">
          {/* Original Issue Card */}
          <Card className="border-border/50 bg-card shadow-sm">
            <CardHeader className="pb-3 border-b border-border/40">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <Avatar className="h-8 w-8">
                    <AvatarFallback className="bg-primary/10 text-primary text-xs">U</AvatarFallback>
                  </Avatar>
                  <span className="font-semibold text-sm">Issue Description</span>
                </div>
                <Badge variant="secondary" className="text-xs">{ticket.category}</Badge>
              </div>
            </CardHeader>
            <CardContent className="pt-4">
              <p className="text-sm leading-relaxed whitespace-pre-wrap">{ticket.description}</p>
            </CardContent>
          </Card>

          {/* Conversation Thread */}
          <div className="space-y-4">
            <div className="relative flex items-center justify-center">
              <div className="absolute inset-x-0 h-px bg-border/40" />
              <span className="relative bg-background px-2 text-xs text-muted-foreground font-medium uppercase tracking-wider">
                {t("conversation")}
              </span>
            </div>

            {ticket.messages?.filter((m) => !m.isInternal).map((msg) => {
              const isSupport = msg.authorName === "Support" || msg.isInternal // Adjust logic as needed
              return (
                <div key={msg.id} className={cn("flex gap-3 max-w-[85%]", isSupport ? "ml-auto flex-row-reverse" : "")}>
                  <Avatar className="h-8 w-8 shrink-0 mt-1">
                    <AvatarFallback className={cn("text-xs", isSupport ? "bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400" : "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400")}>
                      {msg.authorName.charAt(0)}
                    </AvatarFallback>
                  </Avatar>
                  <div className={cn(
                    "rounded-2xl p-4 text-sm shadow-sm border",
                    isSupport
                      ? "bg-primary text-primary-foreground border-primary/20 rounded-tr-none"
                      : "bg-card border-border/50 rounded-tl-none"
                  )}>
                    <div className={cn("flex items-center justify-between gap-4 mb-1 text-xs opacity-70", isSupport ? "text-primary-foreground" : "text-muted-foreground")}>
                      <span className="font-medium">{msg.authorName}</span>
                      <span>{new Date(msg.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
                    </div>
                    <p className="whitespace-pre-wrap leading-relaxed">{msg.content}</p>
                  </div>
                </div>
              )
            })}
          </div>

          {/* Reply Area */}
          {canReply ? (
            <Card className="border-border/50 bg-card overflow-hidden focus-within:ring-2 focus-within:ring-primary/20 transition">
              <div className="p-4">
                <Textarea
                  placeholder={t("replyPlaceholder")}
                  value={replyContent}
                  onChange={handleReplyContentChange}
                  rows={3}
                  className="min-h-[100px] border-0 focus-visible:ring-0 resize-none p-0 bg-transparent placeholder:text-muted-foreground/50"
                />
              </div>
              <div className="flex items-center justify-between px-4 py-3 bg-muted/20 border-t border-border/40">
                <span className="text-xs text-muted-foreground flex items-center gap-1">
                  <MessageSquare className="h-3 w-3" />
                  Markdown supported
                </span>
                <Button onClick={handleReply} disabled={!replyContent.trim() || addMessage.isPending} size="sm" className="gap-2">
                  {addMessage.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Send className="h-3.5 w-3.5" />}
                  {t("sendReply")}
                </Button>
              </div>
            </Card>
          ) : (
            <div className="flex items-center justify-center p-6 rounded-xl border border-dashed border-border/60 bg-muted/10 text-muted-foreground text-sm">
              <Lock className="h-4 w-4 mr-2" />
              {t("ticketClosedMessage") || "This ticket is closed. Reopen it to reply."}
            </div>
          )}
        </div>

        {/* Sidebar Info */}
        <div className="space-y-6">
          <Card className="border-border/50 bg-card shadow-sm sticky top-6">
            <CardHeader className="pb-3 border-b border-border/40">
              <CardTitle className="text-sm font-medium uppercase tracking-wider text-muted-foreground">
                {t("actions")}
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 pt-4">
              {canClose && (
                <Button variant="outline" className="w-full justify-start text-muted-foreground hover:text-destructive hover:bg-destructive/10 hover:border-destructive/20" onClick={handleClose} disabled={closeTicket.isPending}>
                  <CheckCircle2 className="h-4 w-4 mr-2" /> {t("closeTicket")}
                </Button>
              )}
              {canReopen && (
                <Button variant="outline" className="w-full justify-start" onClick={handleReopen} disabled={reopenTicket.isPending}>
                  <RotateCcw className="h-4 w-4 mr-2" /> {t("reopenTicket")}
                </Button>
              )}

              <div className="pt-4 mt-2 border-t border-border/40">
                <h4 className="text-xs font-medium text-muted-foreground mb-3 flex items-center gap-1">
                  <History className="h-3 w-3" />
                  {t("changeHistory")}
                </h4>
                <div className="space-y-3 pl-2 border-l border-border/50">
                  {history?.history?.map((entry) => (
                    <div key={entry.id} className="relative pl-4 text-xs">
                      {/* Dot indicator */}
                      <div className="absolute -left-[5px] top-1.5 h-2 w-2 rounded-full bg-muted-foreground/30 ring-2 ring-background" />
                      <div className="text-muted-foreground/80">
                        {new Date(entry.createdAt).toLocaleDateString()}
                      </div>
                      <div className="font-medium text-foreground/80 mt-0.5">
                        {entry.changeType}
                      </div>
                    </div>
                  ))}
                  {(!history?.history || history.history.length === 0) && (
                    <div className="text-xs text-muted-foreground/50 italic pl-4">No history</div>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  )
}
