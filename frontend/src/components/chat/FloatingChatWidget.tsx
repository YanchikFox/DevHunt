import { useState, useCallback, useMemo } from "react"
import { useParams } from "next/navigation"
import {
  ArrowLeft,
  Hash,
  MessageCircle,
  Search,
  Sparkles,
  X,
} from "lucide-react"
import { Button } from "@/components/ui/button"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { ChatWindow } from "./ChatWindow"
import { AiChatWindow } from "./AiChatWindow"
import {
  useConversations,
  type Conversation,
  type ConversationParticipant,
} from "@/lib/api/queries/chat"
import { useProjectChannels } from "@/lib/api/queries/project-channels"
import { useProjectsList } from "@/lib/api/queries/projects"
import { useChatStore } from "@/lib/store/chatStore"
import { useSession } from "next-auth/react"
import { useTranslations } from "next-intl"
import { RootView, ProjectsView, ChannelsView } from "./FloatingChatWidgetViews"

function useFloatingChatState() {
  const t = useTranslations("chat")
  const tChannels = useTranslations("chat.channels")
  const tCommon = useTranslations("common")
  const { isOpen, activeConversationId, recipientUser, openConversation, closeWidget, toggleWidget, resetActiveChat } = useChatStore()
  const { data: session } = useSession()
  const params = useParams()
  const routeProjectId = typeof params?.id === "string" ? params.id : null

  const [searchQuery, setSearchQuery] = useState("")
  const [isAiMode, setIsAiMode] = useState(false)
  const [view, setView] = useState<"root" | "projects" | "channels">("root")
  const [selectedProjectId, setSelectedProjectId] = useState<string | null>(null)
  const [selectedProjectTitle, setSelectedProjectTitle] = useState<string>("")

  const { data: conversations = [] } = useConversations()
  const { data: myProjects = [] } = useProjectsList({ myProjects: true })
  const { data: channels = [] } = useProjectChannels(view === "channels" ? selectedProjectId : null)

  const activeConversation = conversations.find((c) => c.id === activeConversationId)
  const isConversationOpen = Boolean(isAiMode || activeConversationId || recipientUser)

  const isDirectConversation = useCallback((conv: Conversation) => {
    const typeValue = String(conv?.type ?? "").toLowerCase()
    return typeValue === "direct" || typeValue === "0"
  }, [])

  const getConversationTitle = useCallback((conv: Conversation) => {
    if (conv.title) return conv.title
    if (isDirectConversation(conv)) {
      const otherUser = conv.participants.find((p: ConversationParticipant) => p.userId !== session?.user?.id)
      return otherUser?.fullName || t("unknownUser")
    }
    return t("groupChatFallback")
  }, [isDirectConversation, session?.user?.id, t])

  const getConversationAvatar = useCallback((conv: Conversation) => {
    if (isDirectConversation(conv)) {
      const otherUser = conv.participants.find((p: ConversationParticipant) => p.userId !== session?.user?.id)
      return otherUser?.avatarUrl
    }
    return null
  }, [isDirectConversation, session?.user?.id])

  const filteredConversations = useMemo(() => conversations.filter((conv) => getConversationTitle(conv).toLowerCase().includes(searchQuery.toLowerCase())), [conversations, getConversationTitle, searchQuery])
  const filteredProjects = useMemo(() => { const q = searchQuery.toLowerCase(); if (!q) return myProjects; return myProjects.filter((p) => (p.title || "").toLowerCase().includes(q)) }, [myProjects, searchQuery])
  const filteredChannels = useMemo(() => { const q = searchQuery.toLowerCase(); if (!q) return channels; return channels.filter((c) => c.slug.toLowerCase().includes(q) || (c.title ?? "").toLowerCase().includes(q)) }, [channels, searchQuery])

  const enterProjectsView = useCallback(() => { setView("projects"); setSearchQuery("") }, [])
  const enterChannelsView = useCallback((projectId: string, title: string) => { setSelectedProjectId(projectId); setSelectedProjectTitle(title); setView("channels"); setSearchQuery("") }, [])
  const goRoot = useCallback(() => { setView("root"); setSearchQuery("") }, [])
  const goProjectsFromChannels = useCallback(() => setView("projects"), [])
  const handleConversationClick = useCallback((id: string) => { setIsAiMode(false); openConversation(id) }, [openConversation])
  const handleAiModeToggle = useCallback(() => { setIsAiMode(true); resetActiveChat() }, [resetActiveChat])
  const handleBackFromChat = useCallback(() => { setIsAiMode(false); resetActiveChat() }, [resetActiveChat])

  return {
    t, tChannels, tCommon, isOpen, activeConversationId, recipientUser,
    closeWidget, toggleWidget, resetActiveChat, routeProjectId,
    searchQuery, setSearchQuery, isAiMode, view, selectedProjectId, selectedProjectTitle,
    conversations, myProjects, channels, activeConversation, isConversationOpen,
    isDirectConversation, getConversationTitle, getConversationAvatar,
    filteredConversations, filteredProjects, filteredChannels,
    enterProjectsView, enterChannelsView, goRoot, goProjectsFromChannels,
    handleConversationClick, handleAiModeToggle, handleBackFromChat,
  }
}

export function FloatingChatWidget() {
  const {
    t, tChannels, tCommon, isOpen, activeConversationId, recipientUser,
    closeWidget, toggleWidget, routeProjectId,
    searchQuery, setSearchQuery, isAiMode, view, selectedProjectId, selectedProjectTitle,
    myProjects, channels, activeConversation, isConversationOpen,
    getConversationTitle, getConversationAvatar, filteredConversations, filteredProjects, filteredChannels,
    enterProjectsView, enterChannelsView, goRoot, goProjectsFromChannels,
    handleConversationClick, handleAiModeToggle, handleBackFromChat,
    resetActiveChat, conversations,
  } = useFloatingChatState()

  if (!isOpen) {
    return (
      <Button
        onClick={toggleWidget}
        className="fixed bottom-5 right-5 z-50 h-[52px] w-[52px] rounded-full bg-primary text-primary-foreground shadow-lg hover:bg-primary/90"
        size="icon"
        aria-label="Open chat"
      >
        <MessageCircle className="h-5 w-5" />
      </Button>
    )
  }

  return (
    <div className="fixed bottom-[84px] right-5 z-50 flex h-[520px] max-h-[calc(100vh-120px)] w-[360px] max-w-[calc(100vw-40px)] overflow-hidden rounded-[14px] border border-border bg-bg-elevated shadow-lg fade-in">
      <div className="flex min-w-0 flex-1 flex-col bg-bg-elevated">
        {isConversationOpen ? (
          <ConversationSurface
            isAiMode={isAiMode}
            activeConversation={activeConversation}
            activeConversationId={activeConversationId}
            recipientUser={recipientUser}
            routeProjectId={routeProjectId}
            tCommon={tCommon}
            t={t}
            getConversationTitle={getConversationTitle}
            getConversationAvatar={getConversationAvatar}
            onBack={handleBackFromChat}
            onClose={closeWidget}
            onResetActiveChat={resetActiveChat}
          />
        ) : (
          <>
            <WidgetHeader
              view={view}
              title={
                view === "projects"
                  ? tChannels("projectsSectionTitle")
                  : view === "channels"
                    ? selectedProjectTitle || tChannels("sectionTitle")
                    : t("title")
              }
              countBadge={
                view === "root"
                  ? conversations.length
                  : view === "projects"
                    ? myProjects.length
                    : channels.length
              }
              onBack={
                view === "channels"
                  ? goProjectsFromChannels
                  : view === "projects"
                    ? goRoot
                    : undefined
              }
              onClose={closeWidget}
            />
            <div className="px-2 pt-2">
              <SearchBar
                value={searchQuery}
                onChange={setSearchQuery}
                placeholder={
                  view === "projects"
                    ? t("searchPlaceholder")
                    : view === "channels"
                      ? tChannels("searchMembers")
                      : t("searchPlaceholder")
                }
              />
            </div>
            <div className="flex-1 overflow-y-auto p-1">
              {view === "root" && (
                <RootView
                  isAiMode={isAiMode}
                  onAiModeToggle={handleAiModeToggle}
                  onProjectsClick={enterProjectsView}
                  projectsCount={myProjects.length}
                  conversations={filteredConversations}
                  activeConversationId={activeConversationId}
                  onConversationClick={handleConversationClick}
                  getConversationTitle={getConversationTitle}
                  getConversationAvatar={getConversationAvatar}
                  tCommon={tCommon}
                  tChannels={tChannels}
                  t={t}
                />
              )}

              {view === "projects" && (
                <ProjectsView
                  projects={filteredProjects}
                  onSelectProject={enterChannelsView}
                  emptyText={tChannels("projectsEmpty")}
                />
              )}

              {view === "channels" && selectedProjectId && (
                <ChannelsView
                  channels={filteredChannels}
                  emptyText={tChannels("noChannels")}
                  onSelectChannel={(channelId) => handleConversationClick(channelId)}
                  unreadBadgeTemplate={(n) => tChannels("unreadBadge", { count: n })}
                />
              )}
            </div>
          </>
        )}
      </div>
    </div>
  )
}

function WidgetHeader({
  view,
  title,
  countBadge,
  onBack,
  onClose,
}: {
  readonly view: "root" | "projects" | "channels"
  readonly title: string
  readonly countBadge: number
  readonly onBack?: () => void
  readonly onClose: () => void
}) {
  return (
    <div className="flex h-14 shrink-0 items-center gap-2 border-b border-border px-3">
      {onBack ? (
        <button
          type="button"
          onClick={onBack}
          className="flex h-8 w-8 items-center justify-center rounded-[8px] text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
          aria-label="Back"
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
      ) : view === "root" ? (
        <MessageCircle className="h-4 w-4 text-muted-foreground" />
      ) : null}
      <span className="flex-1 truncate text-[13px] font-semibold">{title}</span>
      {countBadge > 0 && <span className="chip text-[10px]">{countBadge}</span>}
      <button
        type="button"
        onClick={onClose}
        className="flex h-8 w-8 items-center justify-center rounded-[8px] text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
        aria-label="Close"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  )
}

function SearchBar({
  value,
  onChange,
  placeholder,
}: {
  readonly value: string
  readonly onChange: (v: string) => void
  readonly placeholder: string
}) {
  return (
    <div className="relative">
      <input
        type="text"
        className="h-8 w-full rounded-[8px] border border-border bg-bg-subtle py-1.5 pl-8 pr-3 text-[12px] focus:outline-none focus:ring-1 focus:ring-primary"
        placeholder={placeholder}
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
    </div>
  )
}

function ConversationSurface({
  isAiMode,
  activeConversation,
  activeConversationId,
  recipientUser,
  routeProjectId,
  tCommon,
  t,
  getConversationTitle,
  getConversationAvatar,
  onBack,
  onClose,
  onResetActiveChat,
}: {
  readonly isAiMode: boolean
  readonly activeConversation: Conversation | undefined
  readonly activeConversationId: string | null
  readonly recipientUser: { readonly id: string; readonly name: string; readonly avatarUrl?: string | null } | null
  readonly routeProjectId: string | null
  readonly tCommon: (k: string) => string
  readonly t: (k: string) => string
  readonly getConversationTitle: (conv: Conversation) => string
  readonly getConversationAvatar: (conv: Conversation) => string | null | undefined
  readonly onBack: () => void
  readonly onClose: () => void
  readonly onResetActiveChat: () => void
}) {
  return (
    <>
      <div className="flex h-14 shrink-0 items-center gap-2 border-b border-border bg-bg-elevated px-3">
        <button
          type="button"
          onClick={onBack}
          className="flex h-8 w-8 items-center justify-center rounded-[8px] text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
          aria-label={tCommon("back")}
        >
          <ArrowLeft className="h-4 w-4" />
        </button>
        {isAiMode ? (
          <div className="flex min-w-0 flex-1 items-center gap-2">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[10px] border border-primary/20 bg-primary/10">
              <Sparkles className="h-4 w-4 text-primary" />
            </div>
            <div className="min-w-0">
              <div className="truncate text-[13px] font-semibold">
                {t("aiAssistantLabel")}
              </div>
              <div className="truncate font-mono text-[10px] text-muted-foreground">
                {t("architectHelper")}
              </div>
            </div>
          </div>
        ) : (
          <DockConversationHeader
            activeConversation={activeConversation}
            recipientUser={recipientUser}
            getConversationTitle={getConversationTitle}
            getConversationAvatar={getConversationAvatar}
            t={t}
          />
        )}
        <button
          type="button"
          onClick={onClose}
          className="flex h-8 w-8 items-center justify-center rounded-[8px] text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
          aria-label={tCommon("close")}
        >
          <X className="h-4 w-4" />
        </button>
      </div>
      {isAiMode ? (
        <AiChatWindow
          projectId={routeProjectId}
          className="h-full border-0 rounded-none shadow-none"
        />
      ) : (
        <ChatWindow
          conversationId={activeConversationId ?? "new"}
          conversation={activeConversation}
          recipientUser={recipientUser}
          onBack={onResetActiveChat}
          onClose={onClose}
          className="h-full border-0 rounded-none shadow-none"
          showBackButton={false}
          showHeader={false}
        />
      )}
    </>
  )
}

function DockConversationHeader({
  activeConversation,
  recipientUser,
  getConversationTitle,
  getConversationAvatar,
  t,
}: {
  readonly activeConversation: Conversation | undefined
  readonly recipientUser: { readonly id: string; readonly name: string; readonly avatarUrl?: string | null } | null
  readonly getConversationTitle: (conv: Conversation) => string
  readonly getConversationAvatar: (conv: Conversation) => string | null | undefined
  readonly t: (key: string) => string
}) {
  // Project channels show "#slug · Title" so the header matches the
  // full-page chat. For direct / group we fall back to the usual name.
  const isChannel = (() => {
    if (!activeConversation) return false
    const typeVal = String(activeConversation.type ?? "").toLowerCase()
    return typeVal === "projectchannel" || typeVal === "2"
  })()

  let title: string
  if (isChannel && activeConversation) {
    const slug = (activeConversation as Record<string, unknown>).slug as string | undefined
    const readable = activeConversation.title
    title = slug ? `#${slug}${readable ? ` · ${readable}` : ""}` : readable ?? t("groupChatFallback")
  } else {
    title = activeConversation
      ? getConversationTitle(activeConversation)
      : recipientUser?.name || t("newDialogue")
  }

  const avatar = activeConversation && !isChannel
    ? getConversationAvatar(activeConversation)
    : recipientUser?.avatarUrl
  const initial = title[0]?.toUpperCase() ?? "?"

  return (
    <div className="flex min-w-0 flex-1 items-center gap-2">
      <Avatar className="h-8 w-8 rounded-[10px] border border-border">
        <AvatarImage src={avatar ?? undefined} alt={title} />
        <AvatarFallback className="rounded-[10px] bg-primary/10 font-mono text-[11px] font-semibold text-primary">
          {isChannel ? <Hash className="size-4" /> : initial}
        </AvatarFallback>
      </Avatar>
      <div className="min-w-0">
        <div className="truncate text-[13px] font-semibold">{title}</div>
        <div className="font-mono text-[10px] text-success">online</div>
      </div>
    </div>
  )
}
