"use client"

import Image from "next/image"
import { useCallback, useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Plus, MoreHorizontal } from "lucide-react"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "@/lib/api/client"
import { useToast } from "@/hooks/use-toast"
import { cn } from "@/lib/utils"

interface TeamMember {
  Id?: string
  id?: string
  userId?: string
  UserId?: string
  FullName?: string
  fullName?: string
  username?: string
  Email?: string
  email?: string
  Role?: string
  role?: string
  joinedAt?: string
  avatarUrl?: string
  AvatarUrl?: string
  canPublishNews?: boolean
  CanPublishNews?: boolean
  canManageTasks?: boolean
  CanManageTasks?: boolean
  canManageFiles?: boolean
  CanManageFiles?: boolean
  canManageGallery?: boolean
  CanManageGallery?: boolean
  IsLeader?: boolean
  isLeader?: boolean
}

interface Invitation {
  id: string
  role: string
  message?: string
  status?: string
  createdAt: string
}

interface TeamTabContentProps {
  projectTeam: TeamMember[]
  teamLoading: boolean
  projectId: string
  ownerId: string
  canManageTeam: boolean
  isCurrentUserInTeam: boolean
  currentUserId?: string
  pendingJoinRequests: Invitation[]
  incomingInvitationsLoading: boolean
  respondingInvitationId: string | null
  onRespondToJoinRequest: (invitationId: string, action: "accept" | "decline") => Promise<void>
  pendingSentInvites: Invitation[]
  sentInvitationsLoading: boolean
  cancellingInvitationId: string | null
  onCancelInvitation: (invitationId: string) => Promise<void>
  isInviteDialogOpen: boolean
  setIsInviteDialogOpen: (open: boolean) => void
  inviteEmail: string
  setInviteEmail: (email: string) => void
  inviteRole: string
  setInviteRole: (role: string) => void
  inviteMessage: string
  setInviteMessage: (message: string) => void
  isInviting: boolean
  onSubmitInvite: () => Promise<void>
  onCloseInviteDialog: () => void
  pendingMyJoinRequest: Invitation | null
  onOpenRequestDialog: () => void
}

function MemberAvatar({ member }: { member: TeamMember }) {
  const name = member.fullName || member.FullName || member.email || member.Email || "?"
  const avatar = member.avatarUrl || member.AvatarUrl
  const initials = name
    .split(" ")
    .map((n) => n[0])
    .join("")
    .slice(0, 2)
    .toUpperCase()

  if (avatar) {
    return (
      <Image
        src={avatar}
        alt={name}
        width={36}
        height={36}
        className="h-9 w-9 flex-shrink-0 rounded-full object-cover ring-1 ring-border"
      />
    )
  }
  return (
    <div className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full bg-primary/10 font-mono text-[12px] font-semibold text-primary ring-1 ring-border">
      {initials}
    </div>
  )
}

interface MemberCardProps {
  member: TeamMember
  ownerId: string
  canManage: boolean
  projectId: string
  onOpenPermissions: (id: string) => void
}

function MemberCard({ member, ownerId, canManage, onOpenPermissions }: MemberCardProps) {
  const t = useTranslations()
  const id = (member.id || member.Id || "").toString()
  const userId = (member.userId || member.UserId || "").toString()
  const name = member.fullName || member.FullName || member.email || member.Email || t("teams.unknownUser")
  const role = member.role || member.Role || ""
  const isOwner = ownerId ? userId === ownerId : false

  const handlePermissions = useCallback(() => onOpenPermissions(id), [onOpenPermissions, id])

  return (
    <div className="flex items-center gap-3 rounded-[10px] border border-border bg-background p-3">
      <MemberAvatar member={member} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[13px] font-medium text-foreground">{name}</p>
        <p className="font-mono text-[11px] capitalize text-muted-foreground">{role || t("teams.member")}</p>
      </div>
      {isOwner && (
        <span className="chip accent text-[10px]">{t("teams.owner")}</span>
      )}
      {canManage && !isOwner && id && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" className="h-7 w-7 p-0 text-muted-foreground hover:text-foreground">
              <MoreHorizontal className="h-3.5 w-3.5" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={handlePermissions}>
              {t("teams.permissions")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}

interface JoinRequestRowProps {
  inv: Invitation
  respondingInvitationId: string | null
  onRespondToJoinRequest: (id: string, action: "accept" | "decline") => Promise<void>
}

function JoinRequestRow({ inv, respondingInvitationId, onRespondToJoinRequest }: JoinRequestRowProps) {
  const t = useTranslations()
  const handleAccept = useCallback(
    () => onRespondToJoinRequest(inv.id, "accept"),
    [onRespondToJoinRequest, inv.id]
  )
  const handleDecline = useCallback(
    () => onRespondToJoinRequest(inv.id, "decline"),
    [onRespondToJoinRequest, inv.id]
  )
  const isResponding = respondingInvitationId === inv.id

  return (
    <div className="flex items-center justify-between gap-3 rounded-[8px] border border-border bg-background p-3">
      <div className="min-w-0">
        <p className="text-[13px] font-medium text-foreground">{inv.role || t("teams.member")}</p>
        <p className="font-mono text-[11px] text-muted-foreground">
          {new Date(inv.createdAt).toLocaleDateString()}
          {inv.message && ` · ${inv.message}`}
        </p>
      </div>
      <div className="flex flex-shrink-0 gap-1.5">
        <Button size="sm" onClick={handleAccept} disabled={isResponding} className="h-7 text-[12px]">
          {t("teams.accept")}
        </Button>
        <Button size="sm" variant="outline" onClick={handleDecline} disabled={isResponding} className="h-7 text-[12px]">
          {t("teams.decline")}
        </Button>
      </div>
    </div>
  )
}

interface SentInviteRowProps {
  inv: Invitation
  cancellingInvitationId: string | null
  onCancelInvitation: (id: string) => Promise<void>
}

function SentInviteRow({ inv, cancellingInvitationId, onCancelInvitation }: SentInviteRowProps) {
  const t = useTranslations()
  const handleCancel = useCallback(() => onCancelInvitation(inv.id), [onCancelInvitation, inv.id])

  return (
    <div className="flex items-center justify-between gap-3 rounded-[8px] border border-border bg-background p-3">
      <div className="min-w-0">
        <p className="text-[13px] font-medium text-foreground">{inv.role || t("teams.member")}</p>
        <p className="font-mono text-[11px] text-muted-foreground">
          {new Date(inv.createdAt).toLocaleDateString()}
        </p>
      </div>
      <Button
        size="sm"
        variant="outline"
        onClick={handleCancel}
        disabled={cancellingInvitationId === inv.id}
        className="h-7 flex-shrink-0 text-[12px]"
      >
        {cancellingInvitationId === inv.id ? t("common.cancelling") : t("common.cancel")}
      </Button>
    </div>
  )
}

interface PermissionsDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  member: TeamMember | null
  projectId: string
  isOwner: boolean
}

function PermissionsDialog({ open, onOpenChange, member, projectId, isOwner }: PermissionsDialogProps) {
  const t = useTranslations()
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [pending, setPending] = useState(false)

  const norm = useCallback((key: string) => {
    if (!member) return false
    const rec = member as Record<string, unknown>
    return Boolean(rec[key] ?? rec[key[0].toUpperCase() + key.slice(1)])
  }, [member])

  const updatePermissions = useMutation({
    mutationFn: async (payload: Record<string, boolean>) => {
      const memberId = (member?.id || member?.Id || "").toString()
      return apiClient.patch(`/projects/${projectId}/team/${memberId}/permissions`, {
        CanPublishNews: payload.canPublishNews,
        CanManageTasks: payload.canManageTasks,
        CanManageFiles: payload.canManageFiles,
        CanManageGallery: payload.canManageGallery,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teams", "members", projectId] })
      queryClient.invalidateQueries({ queryKey: ["teams", "members", projectId, { includePermissions: true }] })
    },
  })

  const handleToggle = useCallback(
    async (key: "canPublishNews" | "canManageTasks" | "canManageFiles" | "canManageGallery", value: boolean) => {
      if (!member || isOwner) return
      setPending(true)
      try {
        await updatePermissions.mutateAsync({
          canPublishNews: key === "canPublishNews" ? value : norm("canPublishNews"),
          canManageTasks: key === "canManageTasks" ? value : norm("canManageTasks"),
          canManageFiles: key === "canManageFiles" ? value : norm("canManageFiles"),
          canManageGallery: key === "canManageGallery" ? value : norm("canManageGallery"),
        })
        toast({ title: t("teams.permissionsUpdated"), description: t("teams.permissionsUpdateSuccess") })
      } catch (error) {
        toast({
          title: t("common.error"),
          description: error instanceof Error ? error.message : t("teams.permissionsUpdateFailed"),
          variant: "destructive",
        })
      } finally {
        setPending(false)
      }
    },
    [member, isOwner, norm, updatePermissions, toast, t]
  )

  const handleTogglePublishNews = useCallback((v: boolean) => handleToggle("canPublishNews", v), [handleToggle])
  const handleToggleManageTasks = useCallback((v: boolean) => handleToggle("canManageTasks", v), [handleToggle])
  const handleToggleManageFiles = useCallback((v: boolean) => handleToggle("canManageFiles", v), [handleToggle])
  const handleToggleManageGallery = useCallback((v: boolean) => handleToggle("canManageGallery", v), [handleToggle])

  const name = member?.fullName || member?.FullName || member?.email || member?.Email || "Member"

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("teams.memberPermissions")}</DialogTitle>
          <DialogDescription>{name}</DialogDescription>
        </DialogHeader>
        {member && (
          <div className="space-y-4">
            {isOwner && (
              <p className="text-sm text-muted-foreground">{t("teams.ownerPermissionsCannotBeChanged")}</p>
            )}
            {(["canPublishNews", "canManageTasks", "canManageFiles", "canManageGallery"] as const).map((key) => (
              <div key={key} className="flex items-center justify-between gap-3">
                <Label className="text-[13px]">{t(`teams.${key === "canPublishNews" ? "publishNews" : key === "canManageTasks" ? "manageTasks" : key === "canManageFiles" ? "manageFiles" : "manageGallery"}`)}</Label>
                <Switch
                  checked={norm(key)}
                  disabled={isOwner || pending}
                  onCheckedChange={key === "canPublishNews" ? handleTogglePublishNews : key === "canManageTasks" ? handleToggleManageTasks : key === "canManageFiles" ? handleToggleManageFiles : handleToggleManageGallery}
                />
              </div>
            ))}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

export function TeamTabContent({
  projectTeam,
  teamLoading,
  projectId,
  ownerId,
  canManageTeam,
  isCurrentUserInTeam,
  currentUserId,
  pendingJoinRequests,
  incomingInvitationsLoading,
  respondingInvitationId,
  onRespondToJoinRequest,
  pendingSentInvites,
  sentInvitationsLoading,
  cancellingInvitationId,
  onCancelInvitation,
  isInviteDialogOpen,
  setIsInviteDialogOpen,
  inviteEmail,
  setInviteEmail,
  inviteRole,
  setInviteRole,
  inviteMessage,
  setInviteMessage,
  isInviting,
  onSubmitInvite,
  onCloseInviteDialog,
  pendingMyJoinRequest,
  onOpenRequestDialog,
}: TeamTabContentProps) {
  const t = useTranslations()
  const [permissionsOpen, setPermissionsOpen] = useState(false)
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null)

  const selectedMember = useMemo(
    () => projectTeam.find((m) => (m.id || m.Id) === selectedMemberId) ?? null,
    [projectTeam, selectedMemberId]
  )

  const selectedIsOwner = useMemo(() => {
    if (!selectedMember) return false
    const uid = (selectedMember.userId || selectedMember.UserId || "").toString()
    return ownerId ? uid === ownerId : false
  }, [selectedMember, ownerId])

  const handleOpenPermissions = useCallback((id: string) => {
    setSelectedMemberId(id)
    setPermissionsOpen(true)
  }, [])

  const handleClosePermissions = useCallback((open: boolean) => {
    setPermissionsOpen(open)
    if (!open) setSelectedMemberId(null)
  }, [])

  const handleInviteEmail = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setInviteEmail(e.target.value),
    [setInviteEmail]
  )
  const handleInviteMessage = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setInviteMessage(e.target.value),
    [setInviteMessage]
  )
  const handleInviteRoleChange = useCallback((v: string) => setInviteRole(v), [setInviteRole])
  const handleOpenInvite = useCallback(() => setIsInviteDialogOpen(true), [setIsInviteDialogOpen])

  const pendingMyJoinRequestId = pendingMyJoinRequest?.id
  const handleCancelMyRequest = useCallback(() => {
    if (pendingMyJoinRequestId) onCancelInvitation(pendingMyJoinRequestId)
  }, [onCancelInvitation, pendingMyJoinRequestId])

  const showJoinRequests = canManageTeam && (incomingInvitationsLoading || pendingJoinRequests.length > 0)
  const showPendingSent = canManageTeam && (sentInvitationsLoading || pendingSentInvites.length > 0)

  return (
    <div className="space-y-4">
      {/* Members */}
      <section className="rounded-[14px] border border-border bg-bg-elevated">
        <div className="flex items-center justify-between border-b border-border px-5 py-4">
          <span className="caption">
            [{t("teams.teamMembers")} · {projectTeam.length}
            {(projectTeam as Array<TeamMember & { maxTeamSize?: number }>)[0]?.maxTeamSize
              ? `/${(projectTeam as Array<TeamMember & { maxTeamSize?: number }>)[0].maxTeamSize}`
              : ""}
            ]
          </span>
          {canManageTeam && (
            <Button size="sm" variant="outline" onClick={handleOpenInvite} className="h-7 gap-1.5 text-[12px]">
              <Plus className="h-3 w-3" />
              {t("teams.inviteMember")}
            </Button>
          )}
        </div>
        <div className="p-5">
          {teamLoading ? (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {[1, 2, 3].map((i) => <Skeleton key={i} className="h-16 rounded-[10px]" />)}
            </div>
          ) : projectTeam.length > 0 ? (
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              {projectTeam.map((member) => (
                <MemberCard
                  key={member.id || member.Id || member.userId || member.UserId}
                  member={member}
                  ownerId={ownerId}
                  canManage={canManageTeam}
                  projectId={projectId}
                  onOpenPermissions={handleOpenPermissions}
                />
              ))}
            </div>
          ) : (
            <p className="text-[13px] text-muted-foreground">{t("teams.noTeamMembersYet")}</p>
          )}
        </div>
      </section>

      {/* Pending join requests (managers only) */}
      {showJoinRequests && (
        <section className="rounded-[14px] border border-border bg-bg-elevated">
          <div className="flex items-center justify-between border-b border-border px-5 py-4">
            <span className="caption">[{t("teams.joinRequests")}]</span>
            <span className="chip text-[10px]">{pendingJoinRequests.length}</span>
          </div>
          <div className="space-y-2 p-5">
            {incomingInvitationsLoading ? (
              <Skeleton className="h-14 rounded-[8px]" />
            ) : pendingJoinRequests.length === 0 ? (
              <p className="text-[13px] text-muted-foreground">{t("teams.noPendingRequests")}</p>
            ) : (
              pendingJoinRequests.map((inv) => (
                <JoinRequestRow
                  key={inv.id}
                  inv={inv}
                  respondingInvitationId={respondingInvitationId}
                  onRespondToJoinRequest={onRespondToJoinRequest}
                />
              ))
            )}
          </div>
        </section>
      )}

      {/* Sent invites (managers only) */}
      {showPendingSent && (
        <section className="rounded-[14px] border border-border bg-bg-elevated">
          <div className="flex items-center justify-between border-b border-border px-5 py-4">
            <span className="caption">[{t("teams.pendingInvitations")}]</span>
            <span className="chip text-[10px]">{pendingSentInvites.length}</span>
          </div>
          <div className="space-y-2 p-5">
            {sentInvitationsLoading ? (
              <Skeleton className="h-14 rounded-[8px]" />
            ) : pendingSentInvites.length === 0 ? (
              <p className="text-[13px] text-muted-foreground">{t("teams.noPendingInvitations")}</p>
            ) : (
              pendingSentInvites.map((inv) => (
                <SentInviteRow
                  key={inv.id}
                  inv={inv}
                  cancellingInvitationId={cancellingInvitationId}
                  onCancelInvitation={onCancelInvitation}
                />
              ))
            )}
          </div>
        </section>
      )}

      {/* Request to join (non-members only) */}
      {!canManageTeam && !isCurrentUserInTeam && currentUserId && (
        <div className="flex justify-center pt-2">
          {pendingMyJoinRequest?.status === "pending" ? (
            <div className="flex items-center gap-2">
              <Button variant="outline" disabled className="text-[13px]">
                {t("teams.requestSent")}
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleCancelMyRequest}
                disabled={cancellingInvitationId === pendingMyJoinRequest.id}
                className={cn("text-[12px]", cancellingInvitationId === pendingMyJoinRequest.id && "opacity-50")}
              >
                {cancellingInvitationId === pendingMyJoinRequest.id ? t("common.cancelling") : t("common.cancel")}
              </Button>
            </div>
          ) : (
            <Button onClick={onOpenRequestDialog} className="text-[13px]">
              {t("teams.requestToJoin")}
            </Button>
          )}
        </div>
      )}

      {/* Permissions dialog */}
      <PermissionsDialog
        open={permissionsOpen}
        onOpenChange={handleClosePermissions}
        member={selectedMember}
        projectId={projectId}
        isOwner={selectedIsOwner}
      />

      {/* Invite member dialog */}
      <Dialog open={isInviteDialogOpen} onOpenChange={setIsInviteDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("teams.inviteTeamMember")}</DialogTitle>
            <DialogDescription>{t("teams.sendInvitationToJoin")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="invite-email">{t("teams.emailOrUsername")}</Label>
              <Input
                id="invite-email"
                value={inviteEmail}
                onChange={handleInviteEmail}
                placeholder={t("auth.emailPlaceholder")}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="invite-role">{t("teams.selectRole")}</Label>
              <Select value={inviteRole} onValueChange={handleInviteRoleChange}>
                <SelectTrigger id="invite-role">
                  <SelectValue placeholder={t("teams.selectRole")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="developer">{t("teams.roleDeveloper")}</SelectItem>
                  <SelectItem value="designer">{t("teams.roleDesigner")}</SelectItem>
                  <SelectItem value="tester">{t("teams.roleTester")}</SelectItem>
                  <SelectItem value="manager">{t("teams.roleManager")}</SelectItem>
                  <SelectItem value="other">{t("teams.roleOther")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="invite-message">{t("teams.messageOptional")}</Label>
              <Input
                id="invite-message"
                value={inviteMessage}
                onChange={handleInviteMessage}
                placeholder={t("teams.addPersonalMessage")}
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button type="button" variant="outline" onClick={onCloseInviteDialog}>
                {t("common.cancel")}
              </Button>
              <Button onClick={onSubmitInvite} disabled={isInviting}>
                {isInviting ? t("teams.sending") : t("teams.sendInvitation")}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}
