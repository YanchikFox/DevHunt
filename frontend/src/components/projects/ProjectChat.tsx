"use client"

import { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { toast } from "@/hooks/use-toast"
import {
  Hash,
  Loader2,
  Lock,
  LogOut,
  MessageCircle,
  MoreHorizontal,
  Plus,
  Search,
  Settings2,
  Trash2,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
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
import { ChatWindow } from "@/components/chat/ChatWindow"
import { useProjectPermissions } from "@/lib/api/queries/projects"
import {
  useProjectChannels,
  useDeleteProjectChannel,
  useLeaveProjectChannel,
  type ProjectChannel,
} from "@/lib/api/queries/project-channels"
import {
  useChannelMembers,
  type ChannelMember,
} from "@/lib/api/queries/channel-members"
import { ChannelSettingsDialog } from "./project-chat/ChannelSettingsDialog"
import { cn } from "@/lib/utils"

function DeleteChannelDialog({ channel, onOpenChange, onConfirm, t }: {
  channel: ProjectChannel | null
  onOpenChange: (open: boolean) => void
  onConfirm: () => void
  t: (key: string, params?: Record<string, string>) => string
}) {
  const tChannels = useTranslations("chat.channels")
  return (
    <AlertDialog open={channel !== null} onOpenChange={(open) => !open && onOpenChange(false)}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{tChannels("deleteConfirmTitle")}</AlertDialogTitle>
          <AlertDialogDescription>{tChannels("deleteConfirmDescription", { slug: channel?.slug ?? "" })}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
            {t("common.delete")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

interface ProjectChatProps {
  readonly projectId: string
}

/**
 * Project chat tab. Layout mirrors the design mock:
 *   ┌──────────────────────────┬─────────────┐
 *   │ # general · N members  🔍│ [Channels]  │
 *   │                          │  general    │
 *   │  messages …              │  design     │
 *   │                          ├─────────────┤
 *   │ [ type a message ]  →    │ [Members·N] │
 *   └──────────────────────────┴─────────────┘
 */
export function ProjectChat({ projectId }: ProjectChatProps) {
  const t = useTranslations()
  const tChannels = useTranslations("chat.channels")
  const tChat = useTranslations("chat")

  const { data: channels, isPending, isError, refetch } = useProjectChannels(projectId)
  const { data: permissions } = useProjectPermissions(projectId)
  const deleteMutation = useDeleteProjectChannel(projectId)
  const leaveMutation = useLeaveProjectChannel(projectId)

  const [activeChannelId, setActiveChannelId] = useState<string | null>(null)
  const [dialogMode, setDialogMode] = useState<"closed" | "create" | "edit">("closed")
  const [editingChannel, setEditingChannel] = useState<ProjectChannel | null>(null)
  const [pendingDelete, setPendingDelete] = useState<ProjectChannel | null>(null)

  useEffect(() => {
    if (!channels || channels.length === 0) return
    const stillExists = activeChannelId && channels.some((c) => c.id === activeChannelId)
    if (stillExists) return
    const general = channels.find((c) => c.slug === "general")
    setActiveChannelId((general ?? channels[0]).id)
  }, [channels, activeChannelId])

  const activeChannel = useMemo(
    () => channels?.find((c) => c.id === activeChannelId) ?? null,
    [channels, activeChannelId],
  )

  const { data: members, isPending: membersLoading } = useChannelMembers(
    projectId,
    activeChannel?.id ?? null,
  )

  const handleReload = useCallback(() => {
    refetch()
  }, [refetch])

  const handleCreate = useCallback(() => {
    setEditingChannel(null)
    setDialogMode("create")
  }, [])

  const handleEdit = useCallback((channel: ProjectChannel) => {
    setEditingChannel(channel)
    setDialogMode("edit")
  }, [])

  const handleRequestDelete = useCallback((channel: ProjectChannel) => {
    setPendingDelete(channel)
  }, [])

  const handleConfirmDelete = useCallback(async () => {
    if (!pendingDelete) return
    try {
      await deleteMutation.mutateAsync(pendingDelete.id)
      if (activeChannelId === pendingDelete.id) setActiveChannelId(null)
      setPendingDelete(null)
    } catch (err) {
      toast({
        title: t("common.error"),
        description: err instanceof Error ? err.message : "Request failed",
        variant: "destructive",
      })
    }
  }, [pendingDelete, deleteMutation, activeChannelId, t])

  const handleLeave = useCallback(
    async (channel: ProjectChannel) => {
      try {
        await leaveMutation.mutateAsync(channel.id)
        if (activeChannelId === channel.id) setActiveChannelId(null)
      } catch (err) {
        toast({
          title: t("common.error"),
          description: err instanceof Error ? err.message : "Request failed",
          variant: "destructive",
        })
      }
    },
    [leaveMutation, activeChannelId, t],
  )

  if (isPending && !channels) {
    return (
      <div className="flex h-[540px] flex-col items-center justify-center text-muted-foreground">
        <Loader2 className="mb-4 h-8 w-8 animate-spin" />
        <p>{t("common.loading")}</p>
      </div>
    )
  }

  if (isError) {
    return (
      <div className="flex h-[540px] flex-col items-center justify-center text-muted-foreground">
        <MessageCircle className="mb-4 h-12 w-12 opacity-20" />
        <p className="mb-4">{tChat("failedToLoadChat")}</p>
        <Button onClick={handleReload} variant="outline">
          {t("common.retry")}
        </Button>
      </div>
    )
  }

  return (
    <div className="mt-4 grid gap-4 lg:grid-cols-[minmax(0,1fr)_260px]">
      <Card className="flex h-[calc(100vh-280px)] min-h-[440px] flex-col overflow-hidden border-border/70 bg-card/80 p-0 shadow-sm">
        {activeChannel ? (
          <>
            <ChannelHeader
              channel={activeChannel}
              memberCount={members?.length ?? 0}
              membersLoading={membersLoading}
              onOpenSettings={() => handleEdit(activeChannel)}
            />
            <div className="min-h-0 flex-1">
              <ChatWindow
                key={activeChannel.id}
                conversationId={activeChannel.id}
                className="h-full rounded-none border-0 bg-transparent"
                showHeader={false}
                projectId={projectId}
                canUseAiTools={permissions?.canManageTasks === true}
                canPinMessages={activeChannel.canPinMessages}
                canDeleteMessages={activeChannel.canDeleteMessages}
              />
            </div>
          </>
        ) : (
          <div className="flex flex-1 items-center justify-center text-sm text-muted-foreground">
            {tChannels("noChannels")}
          </div>
        )}
      </Card>

      <aside className="flex flex-col gap-3">
        <ChannelsListCard
          channels={channels ?? []}
          activeChannelId={activeChannelId}
          onSelect={(c) => setActiveChannelId(c.id)}
          onCreate={handleCreate}
          onEdit={handleEdit}
          onDelete={handleRequestDelete}
          onLeave={handleLeave}
        />
        <MembersListCard
          members={members ?? []}
          loading={membersLoading}
          onOpenAll={activeChannel ? () => handleEdit(activeChannel) : undefined}
          canOpenAll={Boolean(activeChannel?.canManageMembers)}
        />
      </aside>

      <ChannelSettingsDialog
        open={dialogMode !== "closed"}
        onOpenChange={(open) => {
          if (!open) {
            setDialogMode("closed")
            setEditingChannel(null)
          }
        }}
        projectId={projectId}
        channel={dialogMode === "edit" ? editingChannel : null}
        onCreated={(id) => setActiveChannelId(id)}
      />

      <DeleteChannelDialog channel={pendingDelete} onOpenChange={(open) => !open && setPendingDelete(null)} onConfirm={handleConfirmDelete} t={t} />
    </div>
  )
}

// ---------------------------------------------------------------------------
// Channel header (above the chat window)
// ---------------------------------------------------------------------------

interface ChannelHeaderProps {
  readonly channel: ProjectChannel
  readonly memberCount: number
  readonly membersLoading: boolean
  readonly onOpenSettings: () => void
}

function ChannelHeader({
  channel,
  memberCount,
  membersLoading,
  onOpenSettings,
}: ChannelHeaderProps) {
  const tChannels = useTranslations("chat.channels")
  const tChat = useTranslations("chat")
  const Icon = channel.isPrivate ? Lock : Hash

  // Show the slug as the primary identifier (Discord/Slack-style). The
  // optional `title` is a longer display name used elsewhere (sidebar,
  // settings dialog); we don't override the `#slug` identity here.
  const slug = channel.slug
  const topic = channel.topic?.trim()

  return (
    <div className="flex shrink-0 items-center justify-between gap-3 border-b border-border/60 px-4 py-3">
      <div className="flex min-w-0 flex-1 items-center gap-2.5">
        <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        <div className="flex min-w-0 items-baseline gap-2">
          <span className="truncate text-sm font-semibold text-foreground">
            {slug}
          </span>
          <span className="shrink-0 text-[11px] text-muted-foreground">
            ·{" "}
            {membersLoading
              ? "…"
              : tChat("memberCount", { count: memberCount })}
          </span>
        </div>
        {topic && (
          <span className="hidden truncate border-l border-border/60 pl-3 text-xs text-muted-foreground md:inline">
            {topic}
          </span>
        )}
        {channel.isPrivate && (
          <span className="ml-1 shrink-0 rounded border border-border/60 bg-muted/40 px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
            {tChannels("privateBadge")}
          </span>
        )}
      </div>

      <div className="flex shrink-0 items-center gap-0.5">
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label={tChannels("searchInChannel")}
          title={tChannels("searchInChannel")}
          className="h-7 w-7 text-muted-foreground hover:text-foreground"
        >
          <Search className="size-3.5" />
        </Button>
        {channel.canManage && (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label={tChannels("channelSettings")}
            title={tChannels("channelSettings")}
            onClick={onOpenSettings}
            className="h-7 w-7 text-muted-foreground hover:text-foreground"
          >
            <Settings2 className="size-3.5" />
          </Button>
        )}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// Right sidebar — channels list card
// ---------------------------------------------------------------------------

interface ChannelsListCardProps {
  readonly channels: ReadonlyArray<ProjectChannel>
  readonly activeChannelId: string | null
  readonly onSelect: (channel: ProjectChannel) => void
  readonly onCreate: () => void
  readonly onEdit: (channel: ProjectChannel) => void
  readonly onDelete: (channel: ProjectChannel) => void
  readonly onLeave: (channel: ProjectChannel) => void
}

function ChannelsListCard({
  channels,
  activeChannelId,
  onSelect,
  onCreate,
  onEdit,
  onDelete,
  onLeave,
}: ChannelsListCardProps) {
  const t = useTranslations("chat.channels")

  return (
    <Card className="border-border/70 bg-card/80 p-3 shadow-sm">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
          {t("sectionTitle")}
        </span>
        <Button
          variant="ghost"
          size="icon"
          className="size-6 text-muted-foreground hover:text-foreground"
          onClick={onCreate}
          aria-label={t("createChannel")}
          title={t("createChannel")}
        >
          <Plus className="size-3.5" />
        </Button>
      </div>

      {channels.length === 0 ? (
        <p className="px-1 py-1 text-xs text-muted-foreground">{t("noChannels")}</p>
      ) : (
        <div className="flex flex-col gap-0.5">
          {channels.map((channel) => (
            <ChannelRow
              key={channel.id}
              channel={channel}
              isActive={channel.id === activeChannelId}
              onSelect={() => onSelect(channel)}
              onEdit={() => onEdit(channel)}
              onDelete={() => onDelete(channel)}
              onLeave={() => onLeave(channel)}
            />
          ))}
        </div>
      )}
    </Card>
  )
}

interface ChannelRowProps {
  readonly channel: ProjectChannel
  readonly isActive: boolean
  readonly onSelect: () => void
  readonly onEdit: () => void
  readonly onDelete: () => void
  readonly onLeave: () => void
}

function ChannelRow({
  channel,
  isActive,
  onSelect,
  onEdit,
  onDelete,
  onLeave,
}: ChannelRowProps) {
  const t = useTranslations("chat.channels")
  const Icon = channel.isPrivate ? Lock : Hash
  const isGeneral = channel.slug === "general"
  const canEdit = channel.canManage
  const canDelete = channel.canDeleteChannel && !isGeneral
  const canLeave = !isGeneral && channel.isMember
  const hasMenu = canEdit || canLeave || canDelete

  return (
    <div className="group/channel relative">
      <button
        type="button"
        onClick={onSelect}
        className={cn(
          "flex w-full items-center gap-1.5 rounded-md px-2 py-1.5 text-left text-[12.5px] transition-colors",
          isActive
            ? "bg-accent/60 text-foreground"
            : "text-muted-foreground hover:bg-accent/30 hover:text-foreground",
        )}
      >
        <Icon
          className={cn(
            "size-3 shrink-0",
            isActive ? "text-foreground/80" : "text-muted-foreground/70",
          )}
          aria-hidden
        />
        <span className="flex-1 truncate">{channel.slug}</span>
        {channel.unreadCount > 0 && !isActive && (
          <span className="shrink-0 rounded-full bg-primary px-1.5 py-px text-[10px] font-semibold leading-none text-primary-foreground">
            {channel.unreadCount > 99 ? "99+" : channel.unreadCount}
          </span>
        )}
      </button>

      {hasMenu && (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              aria-label={t("settings")}
              className={cn(
                "absolute right-1 top-1/2 size-6 -translate-y-1/2 text-muted-foreground opacity-0 transition-opacity hover:text-foreground",
                "group-hover/channel:opacity-100 focus-visible:opacity-100 data-[state=open]:opacity-100",
              )}
              onClick={(e) => e.stopPropagation()}
            >
              <MoreHorizontal className="size-3.5" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="min-w-[180px]">
            {canEdit && (
              <DropdownMenuItem onClick={onEdit}>
                <Settings2 className="mr-2 size-4" />
                {t("editChannel")}
              </DropdownMenuItem>
            )}
            {canLeave && (
              <DropdownMenuItem onClick={onLeave}>
                <LogOut className="mr-2 size-4" />
                {t("leaveChannel")}
              </DropdownMenuItem>
            )}
            {canDelete && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onClick={onDelete}
                  className="text-destructive focus:text-destructive"
                >
                  <Trash2 className="mr-2 size-4" />
                  {t("deleteChannel")}
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      )}
    </div>
  )
}

// ---------------------------------------------------------------------------
// Right sidebar — members list card
// ---------------------------------------------------------------------------

interface MembersListCardProps {
  readonly members: ReadonlyArray<ChannelMember>
  readonly loading: boolean
  readonly onOpenAll?: () => void
  readonly canOpenAll: boolean
}

function MembersListCard({
  members,
  loading,
  onOpenAll,
  canOpenAll,
}: MembersListCardProps) {
  const tChannels = useTranslations("chat.channels")
  const visible = members.slice(0, 6)
  const hidden = members.length - visible.length

  return (
    <Card className="border-border/70 bg-card/80 p-3 shadow-sm">
      <div className="mb-2 flex items-center justify-between">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">
          {tChannels("sidebarMembers")} · {members.length}
        </span>
        {canOpenAll && onOpenAll && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onOpenAll}
            className="h-6 px-1.5 text-[11px] font-normal text-muted-foreground hover:text-foreground"
          >
            {tChannels("viewAllMembers")}
          </Button>
        )}
      </div>

      {loading && members.length === 0 ? (
        <p className="px-1 py-1 text-xs text-muted-foreground">{tChannels("loading")}</p>
      ) : members.length === 0 ? (
        <p className="px-1 py-1 text-xs text-muted-foreground">
          {tChannels("membersEmpty")}
        </p>
      ) : (
        <div className="flex flex-col gap-0.5">
          {visible.map((m) => (
            <MemberRow key={m.userId} member={m} />
          ))}
          {hidden > 0 && (
            <button
              type="button"
              onClick={onOpenAll}
              disabled={!canOpenAll || !onOpenAll}
              className={cn(
                "mt-1 px-1 py-1 text-left text-[11.5px] text-muted-foreground transition-colors",
                canOpenAll && onOpenAll
                  ? "hover:text-foreground"
                  : "cursor-default",
              )}
            >
              {tChannels("andMore", { count: hidden })}
            </button>
          )}
        </div>
      )}
    </Card>
  )
}

function MemberRow({ member }: { readonly member: ChannelMember }) {
  const tChannels = useTranslations("chat.channels")
  const displayName = member.fullName?.trim() || member.username || "—"
  const initials = displayName
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0]?.toUpperCase() ?? "")
    .join("") || "?"

  const badge = member.isProjectOwner
    ? tChannels("ownerBadge")
    : member.role === "admin"
      ? tChannels("roleAdmin")
      : null

  return (
    <div className="flex items-center gap-2 rounded-md px-1 py-1">
      <Avatar className="size-6">
        <AvatarImage src={member.avatarUrl ?? undefined} alt={displayName} />
        <AvatarFallback className="text-[10px]">{initials}</AvatarFallback>
      </Avatar>
      <span className="flex-1 truncate text-[12px] text-foreground/90">{displayName}</span>
      {badge && (
        <span className="shrink-0 rounded bg-muted px-1.5 py-0.5 text-[9.5px] font-medium uppercase tracking-wide text-muted-foreground">
          {badge}
        </span>
      )}
    </div>
  )
}
