"use client"

import { useRef, useEffect } from "react"
import { MessageCircle, Loader2, Sparkles } from "lucide-react"
import { Button } from "@/components/ui/button"
import { ScrollArea } from "@/components/ui/scroll-area"
import { useTranslations } from "next-intl"
import { ChatMessage } from "./ChatMessage"
import type { DisplayMessage } from "./types"

interface MessageHandlerProps {
  readonly onReply?: (message: DisplayMessage) => void
  readonly onEdit?: (message: DisplayMessage) => void
  readonly onDelete?: (message: DisplayMessage) => void
  readonly onToggleReaction?: (message: DisplayMessage, emoji: string) => void
  readonly onTogglePin?: (message: DisplayMessage) => void
  readonly canPinMessages?: boolean
  readonly canDeleteMessages?: boolean
  readonly onCancelAi?: (message: DisplayMessage) => void
  readonly onRegenerateAi?: (message: DisplayMessage) => void
}

interface ChatMessageListProps extends MessageHandlerProps {
  readonly messages: DisplayMessage[]
  readonly currentUserId?: string
  readonly isLoading: boolean
  readonly isEmpty: boolean
  readonly errorMessage: string | null
  readonly onRefresh: () => void
  readonly typingIndicator: boolean
  readonly aiLoading: boolean
}

/**
 * Chat message list with loading, empty, and error states.
 */
export function ChatMessageList({
  messages,
  currentUserId,
  isLoading,
  isEmpty,
  errorMessage,
  onRefresh,
  typingIndicator,
  aiLoading,
  onReply,
  onEdit,
  onDelete,
  onToggleReaction,
  onTogglePin,
  canPinMessages,
  canDeleteMessages,
  onCancelAi,
  onRegenerateAi,
}: ChatMessageListProps) {
  const t = useTranslations()
  const scrollRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    scrollRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages.length])

  return (
    <ScrollArea className="min-h-0 flex-1 bg-background px-[18px] py-5">
      {errorMessage && (
        <ErrorBanner message={errorMessage} onRefresh={onRefresh} refreshLabel={t("chat.refresh")} />
      )}

      <MessageListContent
        messages={messages}
        currentUserId={currentUserId}
        isLoading={isLoading}
        isEmpty={isEmpty}
        noMessagesLabel={t("chat.noMessages")}
        scrollRef={scrollRef}
        onReply={onReply}
        onEdit={onEdit}
        onDelete={onDelete}
        onToggleReaction={onToggleReaction}
        onTogglePin={onTogglePin}
        canPinMessages={canPinMessages}
        canDeleteMessages={canDeleteMessages}
        onCancelAi={onCancelAi}
        onRegenerateAi={onRegenerateAi}
      />

      {typingIndicator && <TypingIndicator text={t("chat.typing")} />}
      {aiLoading && <AiLoadingIndicator t={t} />}
    </ScrollArea>
  )
}

// --- Sub-components ---

interface MessageListContentProps extends MessageHandlerProps {
  readonly messages: DisplayMessage[]
  readonly currentUserId?: string
  readonly isLoading: boolean
  readonly isEmpty: boolean
  readonly noMessagesLabel: string
  readonly scrollRef: React.RefObject<HTMLDivElement | null>
}

function MessageListContent({
  messages,
  currentUserId,
  isLoading,
  isEmpty,
  noMessagesLabel,
  scrollRef,
  onReply,
  onEdit,
  onDelete,
  onToggleReaction,
  onTogglePin,
  canPinMessages,
  canDeleteMessages,
  onCancelAi,
  onRegenerateAi,
}: MessageListContentProps) {
  if (isLoading && isEmpty) return <LoadingPlaceholder />
  if (isEmpty) return <EmptyPlaceholder message={noMessagesLabel} />

  return (
    <div className="flex flex-col gap-3">
      {messages.map((msg) => (
        <ChatMessage
          key={msg.id}
          msg={msg}
          isOwn={msg.senderId === currentUserId}
          currentUserId={currentUserId}
          onReply={onReply}
          onEdit={onEdit}
          onDelete={onDelete}
          onToggleReaction={onToggleReaction}
          onTogglePin={onTogglePin}
          canDeleteMessage={canDeleteMessages || msg.senderId === currentUserId}
          canPinMessage={canPinMessages}
          onCancelAi={onCancelAi}
          onRegenerateAi={onRegenerateAi}
        />
      ))}
      <div ref={scrollRef} />
    </div>
  )
}

interface ErrorBannerProps {
  readonly message: string
  readonly onRefresh: () => void
  readonly refreshLabel: string
}

function ErrorBanner({ message, onRefresh, refreshLabel }: ErrorBannerProps) {
  return (
    <div className="mb-3 rounded-xl border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive flex items-center justify-between gap-2">
      <span className="truncate">{message}</span>
      <Button variant="outline" size="sm" className="h-7 text-xs" onClick={onRefresh}>
        {refreshLabel}
      </Button>
    </div>
  )
}

function LoadingPlaceholder() {
  return (
    <div className="flex h-full items-center justify-center">
      <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
    </div>
  )
}

interface EmptyPlaceholderProps {
  readonly message: string
}

function EmptyPlaceholder({ message }: EmptyPlaceholderProps) {
  return (
    <div className="flex h-full flex-col items-center justify-center text-muted-foreground gap-2">
      <div className="h-12 w-12 rounded-full bg-muted flex items-center justify-center">
        <MessageCircle className="h-6 w-6 opacity-50" />
      </div>
      <p>{message}</p>
    </div>
  )
}

interface TypingIndicatorProps {
  readonly text: string
}

function TypingIndicator({ text }: TypingIndicatorProps) {
  return (
    <div className="mt-3 flex items-center gap-[10px]">
      <div className="h-6 w-6 shrink-0 rounded-full bg-bg-subtle" aria-hidden="true" />
      <div className="flex items-center gap-[3px] rounded-[12px] bg-bg-subtle px-[11px] py-[7px]">
        <span
          className="pulse-dot h-[5px] w-[5px] rounded-full bg-muted-foreground"
          style={{ animationDelay: "0s" }}
        />
        <span
          className="pulse-dot h-[5px] w-[5px] rounded-full bg-muted-foreground"
          style={{ animationDelay: "0.2s" }}
        />
        <span
          className="pulse-dot h-[5px] w-[5px] rounded-full bg-muted-foreground"
          style={{ animationDelay: "0.4s" }}
        />
      </div>
      <span className="sr-only">{text}</span>
    </div>
  )
}

interface AiLoadingIndicatorProps {
  readonly t: (key: string) => string
}

function AiLoadingIndicator({ t }: AiLoadingIndicatorProps) {
  return (
    <div className="mt-3 flex items-center gap-2 pl-10 text-[12px] text-primary">
      <Sparkles className="h-3.5 w-3.5 animate-pulse" />
      <span className="font-mono text-[10px]">{t("ai.thinking")}</span>
    </div>
  )
}
