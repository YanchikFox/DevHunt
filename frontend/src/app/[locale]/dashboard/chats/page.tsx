"use client"

import Image from "next/image"
import { useCallback, useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { Hash, Lock, MessageSquare, Settings2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { ChatWindow } from "@/components/chat/ChatWindow"
import { useConversations, type Conversation } from "@/lib/api/queries/chat"
import {
  type ProjectChannel,
} from "@/lib/api/queries/project-channels"
import { useChannelMembers } from "@/lib/api/queries/channel-members"
import { ChannelSettingsDialog } from "@/components/projects/project-chat/ChannelSettingsDialog"
import { ChatsSidebar, type SidebarSelection } from "./_components/ChatsSidebar"
import { cn } from "@/lib/utils"

export default function ChatsPage() {
  const tChat = useTranslations("chat")

  const { data: conversations } = useConversations()
  const [selection, setSelection] = useState<SidebarSelection>(null)
  const [channelSettingsOpen, setChannelSettingsOpen] = useState(false)

  const handleSelectConversation = useCallback((c: Conversation) => {
    setSelection({ kind: "conversation", id: c.id })
  }, [])

  const handleSelectChannel = useCallback(
    (projectId: string, channel: ProjectChannel) => {
      setSelection({ kind: "channel", id: channel.id, projectId, channel })
    },
    [],
  )

  const handleCloseChat = useCallback(() => {
    setSelection(null)
  }, [])

  const selectedConversation = useMemo<Conversation | undefined>(
    () =>
      selection?.kind === "conversation"
        ? conversations?.find((c) => c.id === selection.id)
        : undefined,
    [conversations, selection],
  )

  const selectedChannel = selection?.kind === "channel" ? selection.channel : null
  const selectedProjectId = selection?.kind === "channel" ? selection.projectId : null

  return (
    <div className="fade-in -mx-5 lg:-mx-7 -my-5 lg:-my-7 h-[calc(100vh-3.5rem)]">
      <div className="grid h-full grid-cols-1 md:grid-cols-[320px_1fr] xl:grid-cols-[320px_1fr_280px]">
        <aside
          className={cn(
            "flex h-full min-h-0 flex-col border-r border-border bg-bg-subtle",
            selection && "hidden md:flex",
          )}
        >
          <ChatsSidebar
            selection={selection}
            onSelectConversation={handleSelectConversation}
            onSelectChannel={handleSelectChannel}
          />
        </aside>

        <main
          className={cn(
            "flex h-full min-h-0 flex-col bg-background",
            !selection && "hidden md:flex",
          )}
        >
          {selection?.kind === "channel" ? (
            <>
              <ChannelPageHeader
                channel={selection.channel}
                projectId={selection.projectId}
                onOpenSettings={() => setChannelSettingsOpen(true)}
              />
              <div className="min-h-0 flex-1">
                <ChatWindow
                  key={selection.id}
                  conversationId={selection.id}
                  projectId={selection.projectId}
                  className="h-full rounded-none border-0 bg-transparent"
                  showHeader={false}
                />
              </div>
            </>
          ) : selection?.kind === "conversation" ? (
            <ChatWindow
              conversationId={selection.id}
              conversation={selectedConversation}
              onBack={handleCloseChat}
              className="h-full w-full rounded-none border-none bg-transparent shadow-none"
              showBackButton={true}
            />
          ) : (
            <EmptyState hint={tChat("selectConversationHint")} />
          )}
        </main>

        <ChatsPageRail
          conversation={selectedConversation}
          channel={selectedChannel}
          projectId={selectedProjectId}
          onOpenChannelSettings={() => setChannelSettingsOpen(true)}
        />

        {selectedChannel && selectedProjectId && (
          <ChannelSettingsDialog
            open={channelSettingsOpen}
            onOpenChange={setChannelSettingsOpen}
            projectId={selectedProjectId}
            channel={selectedChannel}
          />
        )}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// Header shown above a selected channel in the main pane
// ---------------------------------------------------------------------------

interface ChannelPageHeaderProps {
  readonly channel: ProjectChannel
  readonly projectId: string
  readonly onOpenSettings: () => void
}

function ChannelPageHeader({
  channel,
  projectId,
  onOpenSettings,
}: ChannelPageHeaderProps) {
  const tChannels = useTranslations("chat.channels")
  const tChat = useTranslations("chat")
  const { data: members } = useChannelMembers(projectId, channel.id)
  const Icon = channel.isPrivate ? Lock : Hash
  const memberCount = members?.length ?? 0
  const topic = channel.topic?.trim()

  return (
    <div className="flex shrink-0 items-center justify-between gap-3 border-b border-border/60 bg-bg-elevated px-5 py-3">
      <div className="flex min-w-0 flex-1 items-center gap-2.5">
        <Icon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        <div className="flex min-w-0 items-baseline gap-2">
          <span className="truncate text-sm font-semibold text-foreground">
            {channel.slug}
          </span>
          <span className="shrink-0 text-[11px] text-muted-foreground">
            · {tChat("memberCount", { count: memberCount })}
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
  )
}

// ---------------------------------------------------------------------------
// Right rail — members (either channel or conversation participants)
// ---------------------------------------------------------------------------

function EmptyState({ hint }: { readonly hint: string }) {
  return (
    <div className="flex h-full flex-col items-center justify-center gap-4 px-8 text-muted-foreground">
      <div className="flex h-14 w-14 items-center justify-center rounded-[12px] border border-dashed border-border bg-bg-subtle">
        <MessageSquare className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <div className="max-w-xs space-y-1.5 text-center">
        <h3 className="text-[14px] font-semibold text-foreground">{hint}</h3>
        <p className="text-[12px] leading-relaxed text-muted-foreground">
          Select an existing conversation or start a new one from the list.
        </p>
      </div>
    </div>
  )
}

interface ChatsPageRailProps {
  readonly conversation: Conversation | undefined
  readonly channel: ProjectChannel | null
  readonly projectId: string | null
  readonly onOpenChannelSettings: () => void
}

function ChatsPageRail({
  conversation,
  channel,
  projectId,
  onOpenChannelSettings,
}: ChatsPageRailProps) {
  const tChannels = useTranslations("chat.channels")
  const { data: members } = useChannelMembers(
    channel && projectId ? projectId : null,
    channel?.id ?? null,
  )

  if (!conversation && !channel) {
    return <div className="hidden xl:block border-l border-border bg-bg-subtle" />
  }

  if (channel) {
    const visible = members?.slice(0, 8) ?? []
    const hidden = (members?.length ?? 0) - visible.length

    return (
      <aside className="hidden xl:flex h-full min-h-0 flex-col gap-5 overflow-y-auto border-l border-border bg-bg-subtle p-5">
        <div>
          <div className="mb-3 flex items-center justify-between">
            <div className="caption">
              [{tChannels("sidebarMembers")} · {members?.length ?? 0}]
            </div>
            {channel.canManageMembers && (
              <button
                type="button"
                onClick={onOpenChannelSettings}
                className="text-[11px] text-muted-foreground hover:text-foreground"
              >
                {tChannels("viewAllMembers")}
              </button>
            )}
          </div>
          {visible.length === 0 ? (
            <div className="rounded-[10px] border border-border bg-bg-elevated p-3 text-[11px] text-muted-foreground">
              {tChannels("membersEmpty")}
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {visible.map((m) => {
                const display = m.fullName?.trim() || m.username || "—"
                const initial = display[0]?.toUpperCase() ?? "?"
                const badge = m.isProjectOwner
                  ? tChannels("ownerBadge")
                  : m.role === "admin"
                    ? tChannels("roleAdmin")
                    : null
                return (
                  <div key={m.userId} className="flex items-center gap-2.5">
                    <div className="relative shrink-0">
                      {m.avatarUrl ? (
                        <Image
                          src={m.avatarUrl}
                          alt={display}
                          width={26}
                          height={26}
                          className="h-[26px] w-[26px] rounded-full object-cover"
                        />
                      ) : (
                        <div className="flex h-[26px] w-[26px] items-center justify-center rounded-full bg-primary/10 font-mono text-[11px] font-semibold text-primary">
                          {initial}
                        </div>
                      )}
                    </div>
                    <span className="min-w-0 flex-1 truncate text-[12px] font-medium text-foreground">
                      {display}
                    </span>
                    {badge && (
                      <span className="shrink-0 rounded bg-muted px-1.5 py-0.5 text-[9.5px] font-medium uppercase tracking-wide text-muted-foreground">
                        {badge}
                      </span>
                    )}
                  </div>
                )
              })}
              {hidden > 0 && (
                <button
                  type="button"
                  onClick={onOpenChannelSettings}
                  disabled={!channel.canManageMembers}
                  className={cn(
                    "mt-1 text-left text-[11px] text-muted-foreground",
                    channel.canManageMembers
                      ? "hover:text-foreground"
                      : "cursor-default",
                  )}
                >
                  {tChannels("andMore", { count: hidden })}
                </button>
              )}
            </div>
          )}
        </div>

        {channel.topic && (
          <div>
            <div className="caption mb-3">[Topic]</div>
            <div className="rounded-[10px] border border-border bg-bg-elevated p-3 text-[11px] leading-relaxed text-muted-foreground">
              {channel.topic}
            </div>
          </div>
        )}
      </aside>
    )
  }

  // Direct / group conversation: show raw participants (no role metadata)
  const participants = conversation?.participants ?? []
  return (
    <aside className="hidden xl:flex h-full min-h-0 flex-col gap-5 overflow-y-auto border-l border-border bg-bg-subtle p-5">
      <div>
        <div className="caption mb-3">[Members]</div>
        <div className="flex flex-col gap-2">
          {participants.slice(0, 8).map((p) => (
            <div key={p.userId} className="flex items-center gap-2.5">
              <div className="relative shrink-0">
                {p.avatarUrl ? (
                  <Image
                    src={p.avatarUrl}
                    alt={p.fullName}
                    width={26}
                    height={26}
                    className="h-[26px] w-[26px] rounded-full object-cover"
                  />
                ) : (
                  <div className="flex h-[26px] w-[26px] items-center justify-center rounded-full bg-primary/10 font-mono text-[11px] font-semibold text-primary">
                    {p.fullName[0]?.toUpperCase() ?? "?"}
                  </div>
                )}
                <span className="absolute -bottom-[1px] -right-[1px] h-2 w-2 rounded-full border-2 border-bg-subtle bg-success" />
              </div>
              <span className="min-w-0 flex-1 truncate text-[12px] font-medium text-foreground">
                {p.fullName}
              </span>
            </div>
          ))}
        </div>
      </div>

      <div>
        <div className="caption mb-3">[Pinned]</div>
        <div className="rounded-[10px] border border-border bg-bg-elevated p-3 text-[11px] leading-relaxed text-muted-foreground">
          No pinned messages yet.
        </div>
      </div>
    </aside>
  )
}
