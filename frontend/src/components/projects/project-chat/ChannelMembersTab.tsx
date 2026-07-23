"use client"

import { useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { toast } from "@/hooks/use-toast"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { Checkbox } from "@/components/ui/checkbox"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  useChannelCandidates,
  useChannelMembers,
  useInviteChannelMember,
  useKickChannelMember,
  useBanChannelMember,
  useUpdateChannelMember,
  useChannelRoles,
  useCreateChannelRole,
  type ChannelMember,
  type ChannelRoleDefinition,
  type ChannelRoleDefinitionPayload,
} from "@/lib/api/queries/channel-members"
import {
  MoreHorizontal,
  UserMinus,
  ShieldBan,
  Shield,
  ShieldAlert,
  Search,
  Plus,
  Loader2,
  UserCircle2,
} from "lucide-react"
import type { ProjectChannel } from "@/lib/api/queries/project-channels"

type Props = {
  readonly projectId: string
  readonly channel: ProjectChannel
}

/**
 * Active members tab — list, invite from project team, promote/demote, kick or
 * ban. Admin-only for all mutations (permission checked server-side). The tab
 * is hidden by the parent when the viewer isn't an admin of the channel.
 */
export function ChannelMembersTab({ projectId, channel }: Props) {
  const t = useTranslations("chat.channels")
  const tCommon = useTranslations("common")

  const { data: members = [], isLoading } = useChannelMembers(projectId, channel.id)
  const { data: roles = [] } = useChannelRoles(projectId, channel.id)
  const [showInvite, setShowInvite] = useState(false)
  const [query, setQuery] = useState("")

  const kickMutation = useKickChannelMember(projectId, channel.id)
  const banMutation = useBanChannelMember(projectId, channel.id)
  const updateMutation = useUpdateChannelMember(projectId, channel.id)
  const createRoleMutation = useCreateChannelRole(projectId, channel.id)

  const [confirmKick, setConfirmKick] = useState<ChannelMember | null>(null)
  const [confirmBan, setConfirmBan] = useState<ChannelMember | null>(null)
  const [banReason, setBanReason] = useState("")

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return members
    return members.filter(
      (m) =>
        m.fullName.toLowerCase().includes(q) ||
        (m.username ?? "").toLowerCase().includes(q),
    )
  }, [members, query])

  const handleToggleRole = async (m: ChannelMember) => {
    if (m.isProjectOwner) return
    const nextRole = m.role === "admin" ? "member" : "admin"
    try {
      await updateMutation.mutateAsync({
        targetUserId: m.userId,
        payload: { role: nextRole },
      })
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Update failed",
        variant: "destructive",
      })
    }
  }

  const handleAssignRole = async (member: ChannelMember, role: ChannelRoleDefinition | "admin" | "member") => {
    if (member.isProjectOwner) return
    try {
      await updateMutation.mutateAsync({
        targetUserId: member.userId,
        payload: typeof role === "string"
          ? { role, roleDefinitionId: null }
          : { role: "member", roleDefinitionId: role.id },
      })
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Update failed",
        variant: "destructive",
      })
    }
  }

  const handleKick = async () => {
    if (!confirmKick) return
    try {
      await kickMutation.mutateAsync(confirmKick.userId)
      setConfirmKick(null)
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Kick failed",
        variant: "destructive",
      })
    }
  }

  const handleBan = async () => {
    if (!confirmBan) return
    try {
      await banMutation.mutateAsync({
        targetUserId: confirmBan.userId,
        reason: banReason.trim() || undefined,
      })
      setConfirmBan(null)
      setBanReason("")
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Ban failed",
        variant: "destructive",
      })
    }
  }

  return (
    <div className="space-y-3 py-1">
      <div className="flex items-center gap-2">
        <div className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("searchMembers")}
            className="pl-8"
          />
        </div>
        <Button size="sm" onClick={() => setShowInvite(true)} className="gap-1.5">
          <Plus className="size-3.5" /> {t("addMember")}
        </Button>
      </div>

      <RoleCreator
        isPending={createRoleMutation.isPending}
        onCreate={(payload) => createRoleMutation.mutateAsync(payload)}
      />

      <div className="max-h-[360px] space-y-1 overflow-y-auto pr-1">
        {isLoading ? (
          <div className="flex items-center justify-center py-8 text-sm text-muted-foreground">
            <Loader2 className="mr-2 size-4 animate-spin" /> {tCommon("loading")}
          </div>
        ) : filtered.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">
            {t("membersEmpty")}
          </p>
        ) : (
          filtered.map((m) => (
            <MemberRow
              key={m.userId}
              member={m}
              roles={roles}
              onAssignRole={handleAssignRole}
              onToggleRole={() => handleToggleRole(m)}
              onKick={() => setConfirmKick(m)}
              onBan={() => setConfirmBan(m)}
            />
          ))
        )}
      </div>

      {showInvite && (
        <InvitePicker
          projectId={projectId}
          channel={channel}
          onClose={() => setShowInvite(false)}
        />
      )}

      <KickConfirmDialog
        member={confirmKick}
        channelSlug={channel.slug}
        onConfirm={handleKick}
        onClose={() => setConfirmKick(null)}
      />
      <BanConfirmDialog
        member={confirmBan}
        channelSlug={channel.slug}
        banReason={banReason}
        onBanReasonChange={setBanReason}
        onConfirm={handleBan}
        onClose={() => { setConfirmBan(null); setBanReason("") }}
      />
    </div>
  )
}

function KickConfirmDialog({
  member, channelSlug, onConfirm, onClose,
}: {
  readonly member: ChannelMember | null
  readonly channelSlug: string
  readonly onConfirm: () => void
  readonly onClose: () => void
}) {
  const t = useTranslations("chat.channels")
  const tCommon = useTranslations("common")
  return (
    <AlertDialog open={member !== null} onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("kickConfirm", { name: member?.fullName ?? "", slug: channelSlug })}</AlertDialogTitle>
          <AlertDialogDescription>{t("kickConfirmDescription")}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{tCommon("cancel")}</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>{t("kickMember")}</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

function BanConfirmDialog({
  member, channelSlug, banReason, onBanReasonChange, onConfirm, onClose,
}: {
  readonly member: ChannelMember | null
  readonly channelSlug: string
  readonly banReason: string
  readonly onBanReasonChange: (v: string) => void
  readonly onConfirm: () => void
  readonly onClose: () => void
}) {
  const t = useTranslations("chat.channels")
  const tCommon = useTranslations("common")
  return (
    <AlertDialog open={member !== null} onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("banConfirmTitle", { name: member?.fullName ?? "" })}</AlertDialogTitle>
          <AlertDialogDescription>{t("banConfirmDescription", { slug: channelSlug })}</AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-1.5 py-1">
          <Label htmlFor="ban-reason" className="text-sm">{t("banReasonLabel")}</Label>
          <Textarea
            id="ban-reason"
            value={banReason}
            onChange={(e) => onBanReasonChange(e.target.value)}
            placeholder={t("banReasonPlaceholder")}
            rows={2}
            maxLength={280}
            className="resize-none"
          />
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel>{tCommon("cancel")}</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
            {t("banSubmit")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

function MemberRow({
  member,
  roles,
  onAssignRole,
  onToggleRole,
  onKick,
  onBan,
}: {
  readonly member: ChannelMember
  readonly roles: ChannelRoleDefinition[]
  readonly onAssignRole: (member: ChannelMember, role: ChannelRoleDefinition | "admin" | "member") => void
  readonly onToggleRole: () => void
  readonly onKick: () => void
  readonly onBan: () => void
}) {
  const t = useTranslations("chat.channels")
  const initials = member.fullName.slice(0, 1).toUpperCase() || "?"
  const isActionable = !member.isProjectOwner

  return (
    <div className="flex items-center gap-3 rounded-md border border-transparent px-2 py-1.5 hover:border-border/60 hover:bg-muted/30">
      <Avatar className="h-8 w-8">
        <AvatarImage src={member.avatarUrl ?? undefined} alt={member.fullName} />
        <AvatarFallback className="bg-primary/10 text-xs font-semibold text-primary">
          {initials}
        </AvatarFallback>
      </Avatar>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-1.5">
          <span className="truncate text-sm font-medium">{member.fullName}</span>
          {member.isProjectOwner && (
            <Badge variant="secondary" className="h-4 gap-1 px-1.5 text-[10px]">
              <ShieldAlert className="size-3" /> {t("ownerBadge")}
            </Badge>
          )}
          {!member.isProjectOwner && member.isChannelCreator && (
            <Badge variant="outline" className="h-4 px-1.5 text-[10px]">
              {t("creatorBadge")}
            </Badge>
          )}
          {member.roleDisplayName && (
            <Badge variant="outline" className="h-4 px-1.5 text-[10px]">
              {member.roleDisplayName}
            </Badge>
          )}
          {member.role === "admin" && !member.isProjectOwner && !member.roleDisplayName && (
            <Badge
              variant="outline"
              className="h-4 gap-1 border-primary/40 px-1.5 text-[10px] text-primary"
            >
              <Shield className="size-3" /> {t("roleAdmin")}
            </Badge>
          )}
        </div>
        {member.username && (
          <div className="truncate font-mono text-[11px] text-muted-foreground">
            @{member.username}
          </div>
        )}
      </div>
      {isActionable && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="h-7 w-7">
              <MoreHorizontal className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onSelect={onToggleRole}>
              <Shield className="mr-2 size-4" />
              {member.role === "admin" ? t("roleDemote") : t("rolePromote")}
            </DropdownMenuItem>
            {roles.length > 0 && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={() => onAssignRole(member, "member")}>
                  {t("roleMember")}
                </DropdownMenuItem>
                <DropdownMenuItem onSelect={() => onAssignRole(member, "admin")}>
                  {t("roleAdmin")}
                </DropdownMenuItem>
                {roles.map((role) => (
                  <DropdownMenuItem key={role.id} onSelect={() => onAssignRole(member, role)}>
                    {role.name}
                  </DropdownMenuItem>
                ))}
              </>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={onKick}>
              <UserMinus className="mr-2 size-4" /> {t("kickMember")}
            </DropdownMenuItem>
            <DropdownMenuItem
              onSelect={onBan}
              className="text-destructive focus:text-destructive"
            >
              <ShieldBan className="mr-2 size-4" /> {t("banMember")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}

function RoleCreator({
  isPending,
  onCreate,
}: {
  readonly isPending: boolean
  readonly onCreate: (payload: ChannelRoleDefinitionPayload) => Promise<ChannelRoleDefinition>
}) {
  const [name, setName] = useState("")
  const [canPost, setCanPost] = useState(true)
  const [canManageMembers, setCanManageMembers] = useState(false)
  const [canEditChannel, setCanEditChannel] = useState(false)
  const [canDeleteChannel, setCanDeleteChannel] = useState(false)
  const [canPinMessages, setCanPinMessages] = useState(false)
  const [canDeleteMessages, setCanDeleteMessages] = useState(false)

  const submit = async () => {
    const trimmed = name.trim()
    if (!trimmed) return
    try {
      await onCreate({
        name: trimmed,
        canPost,
        canManageMembers,
        canEditChannel,
        canDeleteChannel,
        canPinMessages,
        canDeleteMessages,
      })
      setName("")
      setCanPost(true)
      setCanManageMembers(false)
      setCanEditChannel(false)
      setCanDeleteChannel(false)
      setCanPinMessages(false)
      setCanDeleteMessages(false)
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Role create failed",
        variant: "destructive",
      })
    }
  }

  return (
    <div className="rounded-[10px] border border-border bg-bg-subtle p-3">
      <div className="flex items-center gap-2">
        <Input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="New role name"
          className="h-8 bg-background text-[13px]"
          maxLength={64}
        />
        <Button size="sm" onClick={submit} disabled={isPending || !name.trim()} className="h-8">
          Create
        </Button>
      </div>
      <div className="mt-2 grid grid-cols-2 gap-2 text-[12px]">
        <RoleFlag label="Post" checked={canPost} onCheckedChange={setCanPost} />
        <RoleFlag label="Pin messages" checked={canPinMessages} onCheckedChange={setCanPinMessages} />
        <RoleFlag label="Delete messages" checked={canDeleteMessages} onCheckedChange={setCanDeleteMessages} />
        <RoleFlag label="Manage members" checked={canManageMembers} onCheckedChange={setCanManageMembers} />
        <RoleFlag label="Edit channel" checked={canEditChannel} onCheckedChange={setCanEditChannel} />
        <RoleFlag label="Delete channel" checked={canDeleteChannel} onCheckedChange={setCanDeleteChannel} />
      </div>
    </div>
  )
}

function RoleFlag({
  label,
  checked,
  onCheckedChange,
}: {
  readonly label: string
  readonly checked: boolean
  readonly onCheckedChange: (value: boolean) => void
}) {
  return (
    <label className="flex items-center gap-2 text-muted-foreground">
      <Checkbox checked={checked} onCheckedChange={(value) => onCheckedChange(value === true)} />
      <span>{label}</span>
    </label>
  )
}

function InvitePicker({
  projectId,
  channel,
  onClose,
}: {
  readonly projectId: string
  readonly channel: ProjectChannel
  readonly onClose: () => void
}) {
  const t = useTranslations("chat.channels")
  const tCommon = useTranslations("common")
  const { data: candidates = [], isLoading } = useChannelCandidates(
    projectId,
    channel.id,
  )
  const inviteMutation = useInviteChannelMember(projectId, channel.id)
  const [query, setQuery] = useState("")

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return candidates
    return candidates.filter(
      (c) =>
        c.fullName.toLowerCase().includes(q) ||
        (c.username ?? "").toLowerCase().includes(q),
    )
  }, [candidates, query])

  const handleInvite = async (userId: string) => {
    try {
      await inviteMutation.mutateAsync({ userId, role: "member" })
    } catch (err) {
      toast({
        description: err instanceof Error ? err.message : "Invite failed",
        variant: "destructive",
      })
    }
  }

  return (
    <AlertDialog open onOpenChange={(open) => !open && onClose()}>
      <AlertDialogContent className="max-w-md">
        <AlertDialogHeader>
          <AlertDialogTitle>{t("addMembers")}</AlertDialogTitle>
        </AlertDialogHeader>
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("searchMembers")}
            className="pl-8"
            autoFocus
          />
        </div>
        <div className="max-h-[320px] space-y-1 overflow-y-auto pr-1">
          {isLoading ? (
            <div className="flex items-center justify-center py-6 text-sm text-muted-foreground">
              <Loader2 className="mr-2 size-4 animate-spin" /> {tCommon("loading")}
            </div>
          ) : filtered.length === 0 ? (
            <p className="py-6 text-center text-sm text-muted-foreground">
              <UserCircle2 className="mx-auto mb-2 size-6 opacity-50" />
              {t("noCandidates")}
            </p>
          ) : (
            filtered.map((c) => {
              const initials = c.fullName.slice(0, 1).toUpperCase() || "?"
              return (
                <div
                  key={c.userId}
                  className="flex items-center gap-3 rounded-md px-2 py-1.5 hover:bg-muted/40"
                >
                  <Avatar className="h-7 w-7">
                    <AvatarImage src={c.avatarUrl ?? undefined} alt={c.fullName} />
                    <AvatarFallback className="bg-primary/10 text-[11px] font-semibold text-primary">
                      {initials}
                    </AvatarFallback>
                  </Avatar>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm">{c.fullName}</div>
                    {c.username && (
                      <div className="truncate font-mono text-[10px] text-muted-foreground">
                        @{c.username}
                      </div>
                    )}
                  </div>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => handleInvite(c.userId)}
                    disabled={inviteMutation.isPending}
                  >
                    <Plus className="mr-1 size-3.5" /> {t("addMember")}
                  </Button>
                </div>
              )
            })
          )}
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel>{tCommon("close")}</AlertDialogCancel>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
