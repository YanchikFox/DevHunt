"use client"

import { useState, useEffect, useCallback, useMemo } from "react"
import { useTranslations } from "next-intl"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { UserPlus, Send, Check, X, Clock, Trash2, Mail, Users, ArrowRight } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { Link } from "@/i18n/routing"
import { useQueryClient } from "@tanstack/react-query"
import { cn } from "@/lib/utils"
import {
  useIncomingInvitations,
  useSentInvitations,
  useRespondToInvitation,
  useCancelInvitation,
} from "@/lib/api/queries/teams"

function InvitationMessageBlock({ message }: { message: string }) {
  return (
    <div className="relative rounded-lg bg-muted/40 p-3 text-sm text-muted-foreground italic border border-border/40">
      <span className="absolute -top-2 left-3 bg-card px-1 text-xs text-muted-foreground/60 not-italic">Message</span>
      &quot;{message}&quot;
    </div>
  )
}

interface ApiError {
  userMessage?: string
}

interface Invitation {
  id: string
  projectId: string
  inviteeId: string
  inviterId: string
  type: "invite" | "request"
  role: string
  status: string
  message?: string
  createdAt: string
}

const INVITATION_STATUS_KEYS: Record<string, string> = {
  pending: "invitations.statusPending",
  accepted: "invitations.statusAccepted",
  declined: "invitations.statusDeclined",
  cancelled: "invitations.statusCancelled",
}

function getInvitationStatusKey(status: string): string {
  const normalized = (status ?? "").trim().toLowerCase()
  return INVITATION_STATUS_KEYS[normalized] ?? ""
}

export default function InvitationsPage() {
  const t = useTranslations()
  const tCommon = useTranslations("common")
  const tInvitations = useTranslations("invitations")
  const tLoading = useTranslations("loading")
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [responding, setResponding] = useState<string | null>(null)

  const handleInviteError = useCallback((error: unknown, fallback: string) => {
    console.error(error)
    const apiError = error as ApiError
    toast({ title: tCommon("error"), description: apiError.userMessage ?? fallback, variant: "destructive" })
  }, [toast, tCommon])

  const incomingQuery = useIncomingInvitations()
  const sentQuery = useSentInvitations()
  const respondMutation = useRespondToInvitation()
  const cancelMutation = useCancelInvitation()

  const incoming = useMemo(() => incomingQuery.data?.items ?? [], [incomingQuery.data])
  const sent = useMemo(() => sentQuery.data?.items ?? [], [sentQuery.data])
  const loading = incomingQuery.isLoading || sentQuery.isLoading

  const fetchInvitations = useCallback(async () => {
    await Promise.all([incomingQuery.refetch(), sentQuery.refetch()])
  }, [incomingQuery, sentQuery])

  useEffect(() => {
    fetchInvitations()
  }, [fetchInvitations])

  const handleRespond = useCallback(async (invitationId: string, action: "accept" | "decline") => {
    const invitation = incoming.find((inv) => inv.id === invitationId)
    setResponding(invitationId)
    try {
      await respondMutation.mutateAsync({ invitationId, action })

      const typeLabel = invitation?.type === "request" ? tInvitations("request") : tInvitations("invite")
      toast({
        title: tCommon("success"),
        description:
          action === "accept"
            ? tInvitations("acceptedMessage", { type: typeLabel })
            : tInvitations("declinedMessage", { type: typeLabel }),
      })

      // Refresh invitations
      await fetchInvitations()

      // Invalidate projects cache to refresh team members list
      if (action === "accept") {
        if (invitation) {
          // Invalidate specific project cache
          queryClient.invalidateQueries({ queryKey: ["projects", invitation.projectId] })
          // Also invalidate projects list
          queryClient.invalidateQueries({ queryKey: ["projects"] })
        }
      }
    } catch (error) {
      handleInviteError(error, tInvitations("failedToRespond"))
    } finally {
      setResponding(null)
    }
  }, [incoming, fetchInvitations, queryClient, respondMutation, toast, handleInviteError, tInvitations, tCommon])

  const handleCancel = useCallback(async (invitationId: string) => {
    const invitation = sent.find((inv) => inv.id === invitationId)
    setResponding(invitationId)
    try {
      await cancelMutation.mutateAsync({ invitationId })

      const typeLabel = invitation?.type === "request" ? tInvitations("request") : tInvitations("invite")
      toast({
        title: tCommon("success"),
        description: tInvitations("cancelledMessage", { type: typeLabel }),
      })

      // Refresh invitations
      await fetchInvitations()
    } catch (error) {
      handleInviteError(error, tInvitations("failedToCancel"))
    } finally {
      setResponding(null)
    }
  }, [cancelMutation, fetchInvitations, toast, handleInviteError, tInvitations, tCommon, sent])

  interface IncomingInvitationCardProps {
    invitation: Invitation
    responding: string | null
    onRespond: (id: string, action: "accept" | "decline") => void
  }

  function IncomingInvitationCard({
    invitation,
    responding,
    onRespond,
  }: IncomingInvitationCardProps) {
    const handleAccept = useCallback(() => {
      onRespond(invitation.id, "accept")
    }, [onRespond, invitation.id])

    const handleDecline = useCallback(() => {
      onRespond(invitation.id, "decline")
    }, [onRespond, invitation.id])

    return (
      <Card className="group overflow-hidden border border-border/50 bg-card transition hover:shadow-lg hover:border-primary/20">
        <CardHeader className="pb-3 pl-6">
          <div className="flex items-start justify-between gap-3">
            <div className="space-y-1">
              <CardTitle className="text-base font-semibold text-foreground flex items-center gap-2">
                <Mail className="h-4 w-4 text-primary" />
                {invitation.type === "request" ? tInvitations("joinRequest") : tInvitations("projectInvitation")}
              </CardTitle>
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <Badge variant="secondary" className="bg-primary/10 text-primary border-primary/20 hover:bg-primary/20">{invitation.role}</Badge>
                <Badge variant="outline" className="text-xs bg-background/50">{invitation.type === "request" ? tInvitations("request") : tInvitations("invite")}</Badge>
                <div className="flex items-center gap-1 text-xs text-muted-foreground ml-2">
                  <Clock className="h-3 w-3" />
                  {new Date(invitation.createdAt).toLocaleDateString()}
                </div>
              </div>
            </div>
            <Badge
              variant={invitation.status === 'pending' ? 'default' : 'secondary'}
              className={cn("shrink-0 uppercase text-[10px] tracking-wider", invitation.status === 'pending' && "bg-primary text-primary-foreground hover:bg-primary/90")}
            >
              {t(getInvitationStatusKey(invitation.status))}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-4 pl-6">
          {invitation.message && <InvitationMessageBlock message={invitation.message} />}

          <div className="flex flex-wrap items-center justify-between gap-4 pt-2 border-t border-border/40 mt-4">
            <Link
              href={`/dashboard/projects/${invitation.projectId}`}
              className="text-sm font-medium text-primary hover:underline flex items-center gap-1 group/link"
            >
              {tInvitations("viewProject")}
              <ArrowRight className="h-3 w-3 transition-transform group-hover/link:translate-x-0.5" />
            </Link>

            <div className="flex gap-2">
              {invitation.status === "pending" && (
                <>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={handleDecline}
                    disabled={responding === invitation.id}
                    className="text-muted-foreground hover:text-destructive hover:bg-destructive/10"
                  >
                    <X className="h-4 w-4 mr-1.5" />
                    {tCommon("decline")}
                  </Button>
                  <Button
                    size="sm"
                    onClick={handleAccept}
                    disabled={responding === invitation.id}
                    className="bg-primary text-primary-foreground hover:bg-primary/90 shadow-md border-0"
                  >
                    <Check className="h-4 w-4 mr-1.5" />
                    {tCommon("accept")}
                  </Button>
                </>
              )}
            </div>
          </div>
        </CardContent>
      </Card>
    )
  }

  interface SentInvitationCardProps {
    invitation: Invitation
    responding: string | null
    onCancel: (id: string) => void
  }

  function SentInvitationCard({ invitation, responding, onCancel }: SentInvitationCardProps) {
    const handleCancel = useCallback(() => {
      onCancel(invitation.id)
    }, [onCancel, invitation.id])

    return (
      <Card className="group overflow-hidden border border-border/50 bg-card transition hover:shadow-lg hover:border-primary/20">
        <CardHeader className="pb-3 pl-6">
          <div className="flex items-start justify-between gap-3">
            <div className="space-y-1">
              <CardTitle className="text-base font-semibold text-foreground flex items-center gap-2">
                <Send className="h-4 w-4 text-muted-foreground" />
                {invitation.type === "request" ? tInvitations("sentRequest") : tInvitations("sentInvitation")}
              </CardTitle>
              <div className="flex flex-wrap items-center gap-2 pt-1">
                <Badge variant="outline" className="bg-background/50">{invitation.role}</Badge>
                <div className="flex items-center gap-1 text-xs text-muted-foreground ml-2">
                  <Clock className="h-3 w-3" />
                  {new Date(invitation.createdAt).toLocaleDateString()}
                </div>
              </div>
            </div>
            <Badge variant="secondary" className="shrink-0 uppercase text-[10px] tracking-wider">
              {t(getInvitationStatusKey(invitation.status))}
            </Badge>
          </div>
        </CardHeader>
        <CardContent className="space-y-4 pl-6">
          {invitation.message && <InvitationMessageBlock message={invitation.message} />}

          <div className="flex flex-wrap items-center justify-between gap-4 pt-2 border-t border-border/40 mt-4">
            <Link
              href={`/dashboard/projects/${invitation.projectId}`}
              className="text-sm font-medium text-muted-foreground hover:text-foreground hover:underline flex items-center gap-1"
            >
              {tInvitations("viewProject")}
            </Link>

            {invitation.status === "pending" && (
              <Button
                size="sm"
                variant="destructive"
                onClick={handleCancel}
                disabled={responding === invitation.id}
                className="bg-transparent hover:bg-destructive/10 text-destructive border border-destructive/20 hover:border-destructive/50"
              >
                <Trash2 className="h-3.5 w-3.5 mr-1.5" />
                {tCommon("cancel")}
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    )
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="text-center space-y-4">
          <div className="relative h-16 w-16 mx-auto">
            <div className="absolute inset-0 rounded-full border-4 border-primary/20" />
            <div className="absolute inset-0 rounded-full border-4 border-primary border-t-transparent animate-spin" />
            <Mail className="absolute inset-0 m-auto h-6 w-6 text-primary animate-pulse" />
          </div>
          <p className="text-muted-foreground font-medium animate-pulse">{tLoading("invitations")}</p>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6 fade-in">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <span className="caption mb-1.5 block">[Inbox]</span>
          <h1 className="font-serif text-[32px] font-normal tracking-[-0.5px] leading-none text-foreground">
            {tInvitations("title")}
          </h1>
          <p className="text-[14px] text-muted-foreground mt-2">
            {tInvitations("description")}
          </p>
        </div>

        <Button asChild size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
          <Link href="/dashboard/projects">
            <Users className="h-3.5 w-3.5" />
            {t("teams.inviteMember")}
          </Link>
        </Button>
      </div>

      <Tabs defaultValue="incoming" className="space-y-5">
        <div className="flex gap-0.5 bg-secondary p-[3px] rounded-[10px] w-fit">
          <TabsList className="bg-transparent border-0 p-0 h-auto gap-0.5">
            <TabsTrigger
              value="incoming"
              className="gap-1.5 px-3 py-[5px] rounded-[6px] text-[12px] font-medium data-[state=active]:bg-card data-[state=active]:shadow-sm data-[state=active]:text-foreground"
            >
              <UserPlus className="h-3 w-3" />
              {tInvitations("incoming")}
              <span className="chip text-[10px] ml-0.5 h-4 min-w-4 flex items-center justify-center">{incoming.length}</span>
            </TabsTrigger>
            <TabsTrigger
              value="sent"
              className="gap-1.5 px-3 py-[5px] rounded-[6px] text-[12px] font-medium data-[state=active]:bg-card data-[state=active]:shadow-sm data-[state=active]:text-foreground"
            >
              <Send className="h-3 w-3" />
              {tInvitations("sentTab")}
              <span className="chip text-[10px] ml-0.5 h-4 min-w-4 flex items-center justify-center">{sent.length}</span>
            </TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="incoming" className="space-y-4 animate-in fade-in slide-in-from-left-2 duration-300">
          {incoming.length === 0 ? (
            <div className="rounded-[14px] border border-dashed border-border bg-card p-12 text-center">
              <UserPlus className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <h3 className="text-[15px] font-semibold text-foreground mb-1">{tInvitations("noIncomingYet")}</h3>
              <p className="text-[13px] text-muted-foreground max-w-sm mx-auto">
                {tInvitations("noIncomingDescription")}
              </p>
            </div>
          ) : (
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-2 xl:grid-cols-3">
              {incoming.map((invitation) => (
                <IncomingInvitationCard
                  key={invitation.id}
                  invitation={invitation}
                  responding={responding}
                  onRespond={handleRespond}
                />
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="sent" className="space-y-4 animate-in fade-in slide-in-from-right-2 duration-300">
          {sent.length === 0 ? (
            <div className="rounded-[14px] border border-dashed border-border bg-card p-12 text-center">
              <Send className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
              <h3 className="text-[15px] font-semibold text-foreground mb-1">{tInvitations("noSentYet")}</h3>
              <p className="text-[13px] text-muted-foreground max-w-sm mx-auto mb-4">
                {tInvitations("noSentDescription")}
              </p>
              <Button asChild size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
                <Link href="/dashboard/projects">
                  <Users className="h-3.5 w-3.5" />
                  Invite someone
                </Link>
              </Button>
            </div>
          ) : (
            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-2 xl:grid-cols-3">
              {sent.map((invitation) => (
                <SentInvitationCard
                  key={invitation.id}
                  invitation={invitation}
                  responding={responding}
                  onCancel={handleCancel}
                />
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}
