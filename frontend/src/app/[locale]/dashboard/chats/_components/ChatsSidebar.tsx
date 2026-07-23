"use client"

import { useCallback, useMemo, useState } from "react"
import { useSession } from "next-auth/react"
import { useLocale, useTranslations } from "next-intl"
import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import {
  ArrowLeft,
  ChevronRight,
  Folder,
  Hash,
  Loader2,
  Lock,
  Search,
} from "lucide-react"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import {
  useConversations,
  type Conversation as ChatConversation,
} from "@/lib/api/queries/chat"
import { useProjectsList } from "@/lib/api/queries/projects"
import {
  useProjectChannels,
  type ProjectChannel,
} from "@/lib/api/queries/project-channels"
import { cn } from "@/lib/utils"

export type SidebarSelection =
  | { readonly kind: "conversation"; readonly id: string }
  | {
      readonly kind: "channel"
      readonly id: string
      readonly projectId: string
      readonly channel: ProjectChannel
    }
  | null

interface ChatsSidebarProps {
  readonly selection: SidebarSelection
  readonly onSelectConversation: (conversation: ChatConversation) => void
  readonly onSelectChannel: (projectId: string, channel: ProjectChannel) => void
  readonly className?: string
}

/**
 * Discord-style drill-down sidebar for the full-page `/dashboard/chats`:
 *   root → [Projects ▸] + direct/group conversations
 *   root → click "Projects" → project list
 *   projects → click a project → channel list
 *   channels → click a channel → open it in the main pane
 *
 * Selection state (controlled) lives in the page so it can drive both the
 * main chat pane and the right member rail.
 */
export function ChatsSidebar({
  selection,
  onSelectConversation,
  onSelectChannel,
  className,
}: ChatsSidebarProps) {
  const t = useTranslations()
  const tChat = useTranslations("chat")
  const tChannels = useTranslations("chat.channels")
  const { data: session } = useSession()
  const currentUserId = session?.user?.id

  const [view, setView] = useState<"root" | "projects" | "channels">("root")
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
  const [selectedProjectTitle, setSelectedProjectTitle] = useState<string>("")
  const [searchQuery, setSearchQuery] = useState("")

  const { data: conversations = [], isLoading: convLoading } = useConversations()
  const { data: myProjects = [], isLoading: projLoading } = useProjectsList({
    myProjects: true,
  })
  const { data: channels = [], isLoading: chLoading } = useProjectChannels(
    view === "channels" ? selectedProjectId : null,
  )

  const isDirect = useCallback((conv: ChatConversation) => {
    const typeValue = String(conv.type).toLowerCase()
    return typeValue === "direct" || typeValue === "0"
  }, [])

  const isChannel = useCallback((conv: ChatConversation) => {
    const typeValue = String(conv.type).toLowerCase()
    // Backend enum: ProjectChannel = 2. Hide channels from the flat list so
    // users navigate to them explicitly via Projects → Channels drill-down.
    return typeValue === "projectchannel" || typeValue === "2"
  }, [])

  const getTitle = useCallback(
    (conv: ChatConversation) => {
      if (conv.title) return conv.title
      if (isDirect(conv)) {
        const other = conv.participants.find((p) => p.userId !== currentUserId)
        return other?.fullName || tChat("unknownUser")
      }
      return tChat("groupChatFallback")
    },
    [isDirect, currentUserId, tChat],
  )

  const getAvatar = useCallback(
    (conv: ChatConversation) => {
      if (isDirect(conv)) {
        const other = conv.participants.find((p) => p.userId !== currentUserId)
        return other?.avatarUrl ?? null
      }
      return null
    },
    [isDirect, currentUserId],
  )

  const filteredConversations = useMemo(() => {
    // Hide project channels from the flat list; they live under Projects.
    const base = conversations.filter((c) => !isChannel(c))
    const q = searchQuery.trim().toLowerCase()
    if (!q) return base
    return base.filter((c) => getTitle(c).toLowerCase().includes(q))
  }, [conversations, isChannel, getTitle, searchQuery])

  const filteredProjects = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()
    if (!q) return myProjects
    return myProjects.filter((p) => (p.title || "").toLowerCase().includes(q))
  }, [myProjects, searchQuery])

  const filteredChannels = useMemo(() => {
    const q = searchQuery.trim().toLowerCase()
    if (!q) return channels
    return channels.filter(
      (c) =>
        c.slug.toLowerCase().includes(q) ||
        (c.title ?? "").toLowerCase().includes(q),
    )
  }, [channels, searchQuery])

  const enterProjects = useCallback(() => {
    setView("projects")
    setSearchQuery("")
  }, [])

  const enterChannels = useCallback((projectId: string, title: string) => {
    setSelectedProjectId(projectId)
    setSelectedProjectTitle(title)
    setView("channels")
    setSearchQuery("")
  }, [])

  const goBack = useCallback(() => {
    if (view === "channels") {
      setView("projects")
      setSearchQuery("")
    } else if (view === "projects") {
      setView("root")
      setSearchQuery("")
    }
  }, [view])

  const selectedId = selection?.id ?? null

  const headerLabel = (() => {
    if (view === "projects") return tChannels("projectsSectionTitle")
    if (view === "channels") return selectedProjectTitle || tChannels("sectionTitle")
    return null // root has the generic "[Messages]" caption
  })()

  const searchPlaceholder = (() => {
    if (view === "projects") return t("projects.searchPlaceholder")
    if (view === "channels") return tChannels("searchInChannel")
    return tChat("searchPlaceholder")
  })()

  return (
    <div className={cn("flex h-full min-h-0 flex-col", className)}>
      <div className="shrink-0 border-b border-border px-4 py-[14px]">
        <div className="mb-2 flex items-center gap-2">
          {view !== "root" ? (
            <button
              type="button"
              onClick={goBack}
              className="flex h-5 w-5 shrink-0 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
              aria-label={t("common.back")}
            >
              <ArrowLeft className="h-3.5 w-3.5" />
            </button>
          ) : null}
          <div className="caption truncate">
            {view === "root" ? "[Messages]" : `[${headerLabel}]`}
          </div>
        </div>
        <div className="relative">
          <Search className="pointer-events-none absolute left-[10px] top-1/2 h-[13px] w-[13px] -translate-y-1/2 text-muted-foreground" />
          <input
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={searchPlaceholder}
            className="h-8 w-full rounded-[8px] border border-border bg-bg-elevated pl-[30px] pr-3 text-[12px] text-foreground outline-none placeholder:text-muted-foreground focus:border-primary/40"
          />
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {view === "root" && (
          <RootView
            projectsCount={myProjects.length}
            conversations={filteredConversations}
            selectedId={selectedId}
            onProjectsClick={enterProjects}
            onSelectConversation={onSelectConversation}
            getTitle={getTitle}
            getAvatar={getAvatar}
            isDirect={isDirect}
            currentUserId={currentUserId}
            loading={convLoading}
            projectsLabel={tChannels("projectsSectionTitle")}
            viewChannelsLabel={tChannels("viewChannels")}
            emptyLabel={tChat("noConversationsFound")}
          />
        )}

        {view === "projects" && (
          <ProjectsPicker
            projects={filteredProjects}
            loading={projLoading}
            onSelect={enterChannels}
            emptyLabel={tChannels("projectsEmpty")}
          />
        )}

        {view === "channels" && selectedProjectId && (
          <ChannelsPicker
            channels={filteredChannels}
            loading={chLoading}
            selectedId={selectedId}
            onSelect={(channel) => onSelectChannel(selectedProjectId, channel)}
            emptyLabel={tChannels("noChannels")}
          />
        )}
      </div>
    </div>
  )
}

// ---------------------------------------------------------------------------
// Root — AI-free: "Projects" entry + flat conversation list
// ---------------------------------------------------------------------------

interface RootViewProps {
  readonly projectsCount: number
  readonly conversations: ChatConversation[]
  readonly selectedId: string | null
  readonly onProjectsClick: () => void
  readonly onSelectConversation: (c: ChatConversation) => void
  readonly getTitle: (c: ChatConversation) => string
  readonly getAvatar: (c: ChatConversation) => string | null
  readonly isDirect: (c: ChatConversation) => boolean
  readonly currentUserId: string | undefined
  readonly loading: boolean
  readonly projectsLabel: string
  readonly viewChannelsLabel: string
  readonly emptyLabel: string
}

function RootView({
  projectsCount,
  conversations,
  selectedId,
  onProjectsClick,
  onSelectConversation,
  getTitle,
  getAvatar,
  isDirect,
  currentUserId,
  loading,
  projectsLabel,
  viewChannelsLabel,
  emptyLabel,
}: RootViewProps) {
  return (
    <>
      <button
        type="button"
        onClick={onProjectsClick}
        className="group flex w-full items-center gap-[10px] border-l-[3px] border-transparent px-4 py-3 text-left transition-colors hover:bg-bg-hover"
      >
        <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-[8px] border border-border bg-bg-subtle text-muted-foreground">
          <Folder className="h-4 w-4" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center justify-between gap-1.5">
            <span className="truncate text-[13px] font-medium text-foreground">
              {projectsLabel}
            </span>
            <span className="shrink-0 font-mono text-[10px] text-muted-foreground">
              {projectsCount}
            </span>
          </div>
          <div className="mt-[2px] truncate text-[11px] text-muted-foreground">
            {viewChannelsLabel}
          </div>
        </div>
        <ChevronRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
      </button>

      <div className="mx-4 border-b border-border/60" />

      {loading && conversations.length === 0 ? (
        <div className="flex h-40 items-center justify-center">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      ) : conversations.length === 0 ? (
        <div className="px-4 py-10 text-center text-[12px] text-muted-foreground">
          {emptyLabel}
        </div>
      ) : (
        conversations.map((conv) => (
          <ConversationRow
            key={conv.id}
            conv={conv}
            isSelected={selectedId === conv.id}
            onSelect={onSelectConversation}
            title={getTitle(conv)}
            avatar={getAvatar(conv) ?? undefined}
            isDirect={isDirect(conv)}
            currentUserId={currentUserId}
          />
        ))
      )}
    </>
  )
}

interface ConversationRowProps {
  readonly conv: ChatConversation
  readonly isSelected: boolean
  readonly onSelect: (c: ChatConversation) => void
  readonly title: string
  readonly avatar?: string
  readonly isDirect: boolean
  readonly currentUserId: string | undefined
}

function ConversationRow({
  conv,
  isSelected,
  onSelect,
  title,
  avatar,
  isDirect,
  currentUserId,
}: ConversationRowProps) {
  const locale = useLocale()
  const dateLocale = getDateFnsLocale(locale)
  const handleClick = useCallback(() => onSelect(conv), [onSelect, conv])

  const time = conv.lastMessageAt
    ? formatDistanceToNow(new Date(conv.lastMessageAt), {
        locale: dateLocale,
        addSuffix: false,
      })
    : null

  const hue = ((title.charCodeAt(0) || 65) * 7) % 360
  const groupColor = `oklch(0.72 0.12 ${hue})`

  const memberCount = conv.participants?.length ?? 0
  const rawPreview = conv.lastMessagePreview?.trim() ?? ""
  const senderPrefix = buildSenderPrefix(conv, currentUserId, isDirect)
  const hasPreview = rawPreview.length > 0

  const previewText = hasPreview
    ? senderPrefix
      ? `${senderPrefix}: ${rawPreview}`
      : rawPreview
    : isDirect
      ? "No messages yet"
      : `${memberCount} ${memberCount === 1 ? "member" : "members"}`

  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        "group flex w-full items-center gap-[10px] border-l-[3px] px-4 py-3 text-left transition-colors",
        isSelected
          ? "border-primary bg-bg-subtle"
          : "border-transparent hover:bg-bg-hover",
      )}
    >
      {isDirect ? (
        <Avatar className="h-[34px] w-[34px] shrink-0 rounded-full">
          {avatar && <AvatarImage src={avatar} />}
          <AvatarFallback className="rounded-full bg-primary/10 font-mono text-[12px] font-semibold text-primary">
            {title[0]?.toUpperCase() ?? "?"}
          </AvatarFallback>
        </Avatar>
      ) : (
        <div
          className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-[8px] font-mono text-[13px] font-semibold text-white"
          style={{ background: groupColor }}
        >
          #
        </div>
      )}

      <div className="min-w-0 flex-1">
        <div className="flex items-center justify-between gap-1.5">
          <span className="truncate text-[13px] font-medium text-foreground">
            {title}
          </span>
          {time && (
            <span className="shrink-0 font-mono text-[10px] text-muted-foreground">
              {time}
            </span>
          )}
        </div>
        <div className="mt-[2px] flex items-center justify-between gap-1.5">
          <span className="truncate text-[11px] text-muted-foreground">
            {previewText}
          </span>
          {conv.unreadCount > 0 && (
            <span className="shrink-0 rounded-full bg-primary px-1.5 py-[1px] text-[10px] font-medium leading-[1.4] text-primary-foreground">
              {conv.unreadCount}
            </span>
          )}
        </div>
      </div>
    </button>
  )
}

function buildSenderPrefix(
  conv: ChatConversation,
  currentUserId: string | undefined,
  isDirect: boolean,
): string | null {
  const senderId = conv.lastMessageSenderId
  if (!senderId) return null
  if (conv.lastMessageIsAi) return "AI"
  if (senderId === currentUserId) return "You"
  if (isDirect) return null
  const name = conv.lastMessageSenderName
  if (!name) return null
  return name.split(/\s+/)[0] ?? name
}

// ---------------------------------------------------------------------------
// Projects picker
// ---------------------------------------------------------------------------

type ProjectListItem = {
  id: string
  title?: string | null
}

interface ProjectsPickerProps {
  readonly projects: ReadonlyArray<ProjectListItem>
  readonly loading: boolean
  readonly onSelect: (projectId: string, title: string) => void
  readonly emptyLabel: string
}

function ProjectsPicker({
  projects,
  loading,
  onSelect,
  emptyLabel,
}: ProjectsPickerProps) {
  if (loading && projects.length === 0) {
    return (
      <div className="flex h-40 items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    )
  }
  if (projects.length === 0) {
    return (
      <div className="px-4 py-10 text-center text-[12px] text-muted-foreground">
        {emptyLabel}
      </div>
    )
  }

  return (
    <>
      {projects.map((p) => {
        const title = p.title || "Untitled"
        const hue = ((title.charCodeAt(0) || 65) * 7) % 360
        const projectColor = `oklch(0.72 0.12 ${hue})`
        return (
          <button
            key={p.id}
            type="button"
            onClick={() => onSelect(p.id, title)}
            className="group flex w-full items-center gap-[10px] border-l-[3px] border-transparent px-4 py-3 text-left transition-colors hover:bg-bg-hover"
          >
            <div
              className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-[8px] font-mono text-[13px] font-semibold text-white"
              style={{ background: projectColor }}
            >
              {title[0]?.toUpperCase() ?? "?"}
            </div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-[13px] font-medium text-foreground">
                {title}
              </div>
            </div>
            <ChevronRight className="h-3.5 w-3.5 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5" />
          </button>
        )
      })}
    </>
  )
}

// ---------------------------------------------------------------------------
// Channels picker
// ---------------------------------------------------------------------------

interface ChannelsPickerProps {
  readonly channels: ReadonlyArray<ProjectChannel>
  readonly loading: boolean
  readonly selectedId: string | null
  readonly onSelect: (channel: ProjectChannel) => void
  readonly emptyLabel: string
}

function ChannelsPicker({
  channels,
  loading,
  selectedId,
  onSelect,
  emptyLabel,
}: ChannelsPickerProps) {
  if (loading && channels.length === 0) {
    return (
      <div className="flex h-40 items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    )
  }
  if (channels.length === 0) {
    return (
      <div className="px-4 py-10 text-center text-[12px] text-muted-foreground">
        {emptyLabel}
      </div>
    )
  }

  return (
    <>
      {channels.map((ch) => {
        const Icon = ch.isPrivate ? Lock : Hash
        const isSelected = ch.id === selectedId
        return (
          <button
            key={ch.id}
            type="button"
            onClick={() => onSelect(ch)}
            className={cn(
              "flex w-full items-center gap-[10px] border-l-[3px] px-4 py-3 text-left transition-colors",
              isSelected
                ? "border-primary bg-bg-subtle"
                : "border-transparent hover:bg-bg-hover",
            )}
          >
            <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-[8px] border border-border bg-bg-subtle text-muted-foreground">
              <Icon className="h-4 w-4" />
            </div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-[13px] font-medium text-foreground">
                #{ch.slug}
              </div>
              {ch.topic ? (
                <div className="truncate text-[11px] text-muted-foreground">
                  {ch.topic}
                </div>
              ) : ch.isPrivate ? (
                <div className="truncate text-[11px] text-muted-foreground">
                  Private
                </div>
              ) : null}
            </div>
            {ch.unreadCount > 0 && !isSelected && (
              <span className="shrink-0 rounded-full bg-primary px-1.5 py-[1px] text-[10px] font-medium leading-[1.4] text-primary-foreground">
                {ch.unreadCount > 99 ? "99+" : ch.unreadCount}
              </span>
            )}
          </button>
        )
      })}
    </>
  )
}
