import Image from "next/image"
import { ChevronRight, Folder, Hash, Lock, Sparkles } from "lucide-react"
import { type Conversation } from "@/lib/api/queries/chat"
import { cn } from "@/lib/utils"

export function RootView({
  isAiMode,
  onAiModeToggle,
  onProjectsClick,
  projectsCount,
  conversations,
  activeConversationId,
  onConversationClick,
  getConversationTitle,
  getConversationAvatar,
  tCommon,
  tChannels,
  t,
}: {
  readonly isAiMode: boolean
  readonly onAiModeToggle: () => void
  readonly onProjectsClick: () => void
  readonly projectsCount: number
  readonly conversations: Conversation[]
  readonly activeConversationId: string | null
  readonly onConversationClick: (id: string) => void
  readonly getConversationTitle: (c: Conversation) => string
  readonly getConversationAvatar: (c: Conversation) => string | null | undefined
  readonly tCommon: (k: string) => string
  readonly tChannels: (k: string) => string
  readonly t: (k: string) => string
}) {
  return (
    <div className="space-y-1">
      <button
        className={cn(
          "flex w-full items-center gap-3 rounded-[10px] p-2 text-left transition-colors",
          isAiMode
            ? "bg-primary/10 text-foreground"
            : "text-muted-foreground hover:bg-bg-hover hover:text-foreground",
        )}
        onClick={onAiModeToggle}
        title={tCommon("aiArchitectAssistant")}
      >
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] border border-primary/20 bg-primary/10">
          <Sparkles className="h-5 w-5 text-primary" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-medium">{t("aiAssistantLabel")}</div>
          <div className="truncate text-xs text-muted-foreground">
            {t("architectHelper")}
          </div>
        </div>
      </button>

      <button
        className="flex w-full items-center gap-3 rounded-[10px] p-2 text-left text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
        onClick={onProjectsClick}
      >
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] border border-border bg-bg-subtle">
          <Folder className="h-5 w-5" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-medium">
            {tChannels("projectsSectionTitle")}
          </div>
          <div className="truncate text-xs text-muted-foreground">
            {tChannels("viewChannels")}
          </div>
        </div>
        <span className="shrink-0 rounded-full bg-bg-subtle px-1.5 py-0.5 font-mono text-[10px] text-muted-foreground">
          {projectsCount}
        </span>
        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
      </button>

      <div className="my-2 border-t border-border" />

      {conversations.map((conv) => (
        <ConversationRow
          key={conv.id}
          conv={conv}
          isActive={activeConversationId === conv.id}
          title={getConversationTitle(conv)}
          avatar={getConversationAvatar(conv)}
          onClick={onConversationClick}
          t={t}
        />
      ))}
    </div>
  )
}

function ConversationRow({
  conv,
  isActive,
  title,
  avatar,
  onClick,
  t,
}: {
  readonly conv: Conversation
  readonly isActive: boolean
  readonly title: string
  readonly avatar: string | null | undefined
  readonly onClick: (id: string) => void
  readonly t: (k: string) => string
}) {
  return (
    <button
      className={cn(
        "flex w-full items-center gap-3 rounded-[10px] p-2 text-left transition-colors",
        isActive
          ? "bg-bg-subtle text-foreground"
          : "text-muted-foreground hover:bg-bg-hover hover:text-foreground",
      )}
      onClick={() => onClick(conv.id)}
      title={title}
    >
      <div className="relative flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-[10px] bg-primary/10 font-mono font-semibold text-primary">
        {avatar ? (
          <Image
            src={avatar}
            alt={title}
            width={40}
            height={40}
            className="h-full w-full object-cover"
          />
        ) : (
          title[0]
        )}
      </div>
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium">{title}</div>
        <div className="truncate text-xs text-muted-foreground">
          {conv.lastMessageAt ? t("clickToOpen") : t("noMessages")}
        </div>
      </div>
    </button>
  )
}

type ProjectListItem = {
  id: string
  title?: string | null
}

export function ProjectsView({
  projects,
  onSelectProject,
  emptyText,
}: {
  readonly projects: ProjectListItem[]
  readonly onSelectProject: (id: string, title: string) => void
  readonly emptyText: string
}) {
  if (projects.length === 0) {
    return (
      <div className="flex h-40 items-center justify-center px-4 text-center text-sm text-muted-foreground">
        {emptyText}
      </div>
    )
  }

  return (
    <div className="space-y-1">
      {projects.map((p) => {
        const title = p.title || "Untitled"
        return (
          <button
            key={p.id}
            className="flex w-full items-center gap-3 rounded-[10px] p-2 text-left text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
            onClick={() => onSelectProject(p.id, title)}
          >
            <div className="relative flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-[10px] bg-primary/10 font-mono font-semibold text-primary">
              {title[0]?.toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-medium">{title}</div>
            </div>
            <ChevronRight className="h-4 w-4 shrink-0" />
          </button>
        )
      })}
    </div>
  )
}

type ChannelListItem = {
  id: string
  slug: string
  title: string | null
  topic: string | null
  isPrivate: boolean
  unreadCount: number
}

export function ChannelsView({
  channels,
  onSelectChannel,
  emptyText,
  unreadBadgeTemplate,
}: {
  readonly channels: ChannelListItem[]
  readonly onSelectChannel: (id: string) => void
  readonly emptyText: string
  readonly unreadBadgeTemplate: (n: number) => string
}) {
  if (channels.length === 0) {
    return (
      <div className="flex h-40 items-center justify-center px-4 text-center text-sm text-muted-foreground">
        {emptyText}
      </div>
    )
  }

  return (
    <div className="space-y-1">
      {channels.map((ch) => {
        const Icon = ch.isPrivate ? Lock : Hash
        return (
          <button
            key={ch.id}
            className="flex w-full items-center gap-3 rounded-[10px] p-2 text-left text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
            onClick={() => onSelectChannel(ch.id)}
          >
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] border border-border bg-bg-subtle">
              <Icon className="h-4 w-4" />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-1 truncate text-sm font-medium text-foreground">
                <span className="truncate">#{ch.slug}</span>
                {ch.title && (
                  <span className="truncate text-xs font-normal text-muted-foreground">
                    · {ch.title}
                  </span>
                )}
              </div>
              {ch.topic && (
                <div className="truncate text-xs text-muted-foreground">
                  {ch.topic}
                </div>
              )}
            </div>
            {ch.unreadCount > 0 && (
              <span className="shrink-0 rounded-full bg-primary px-1.5 py-0.5 text-[10px] font-medium text-primary-foreground">
                {unreadBadgeTemplate(ch.unreadCount)}
              </span>
            )}
          </button>
        )
      })}
    </div>
  )
}
