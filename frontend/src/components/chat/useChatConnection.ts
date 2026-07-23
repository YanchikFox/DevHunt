"use client"

import { useEffect } from "react";
import { useChat } from "@/hooks/useChat"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { useChatMessages } from "./useChatMessages"
import type { DisplayMessage } from "./types"
import type { ConversationParticipant } from "@/lib/api/queries/chat"

// ── Helpers ──────────────────────────────────────────────────────

function extractHistoryErrorMessage(
  hasError: boolean,
  errorObj: Error | null,
  fallback: string,
): string | null {
  if (!hasError || !errorObj) return null
  const typed = errorObj as { userMessage?: string } & Error
  return typed.userMessage ?? typed.message ?? fallback
}

// ── Types ────────────────────────────────────────────────────────

interface UseChatConnectionOptions {
  readonly conversationId: string
  readonly isNewChat: boolean
  readonly participantList: ConversationParticipant[]
}

export interface ChatConnectionResult {
  readonly connected: boolean
  readonly sendMessage: (content: string) => Promise<void>
  readonly setTyping: (typing: boolean) => Promise<void> | void
  readonly typingUsers: Set<string>
  readonly allMessages: DisplayMessage[]
  readonly setAiMessages: React.Dispatch<React.SetStateAction<DisplayMessage[]>>
  readonly historyLoading: boolean
  readonly historyError: boolean
  readonly historyErrorObj: Error | null
  readonly historyErrorMessage: string | null
  readonly refetchHistory: () => void
}

// ── Hook ─────────────────────────────────────────────────────────

/**
 * Manages the real-time SignalR connection, message history, and
 * auto-mark-as-read + error-toast side-effects.
 */
export function useChatConnection({
  conversationId,
  isNewChat,
  participantList,
}: UseChatConnectionOptions): ChatConnectionResult {
  const { toast } = useToast()
  const t = useTranslations()

  const effectiveId = isNewChat ? null : conversationId

  const {
    connected,
    messages: liveMessages,
    typingUsers,
    sendMessage,
    markMessagesAsRead,
    setTyping,
  } = useChat({ conversationId: effectiveId, participants: participantList })

  const {
    allMessages,
    setAiMessages,
    historyLoading,
    historyError,
    historyErrorObj,
    refetchHistory,
  } = useChatMessages({ conversationId, isNewChat, liveMessages })

  // Side-effect: mark as read on connect
  useEffect(() => {
    if (connected && !isNewChat) markMessagesAsRead()
  }, [connected, isNewChat, markMessagesAsRead])

  // Side-effect: toast on history error
  useEffect(() => {
    if (!historyError || !historyErrorObj) return
    const typed = historyErrorObj as { userMessage?: string } & Error
    toast({
      title: t("chat.loadHistoryErrorTitle"),
      description: typed.userMessage ?? typed.message ?? t("chat.loadHistoryError"),
      variant: "destructive",
    })
  }, [historyError, historyErrorObj, toast, t])

  const historyErrorMessage = extractHistoryErrorMessage(
    historyError, historyErrorObj, t("chat.loadHistoryError"),
  )

  return {
    connected, sendMessage, setTyping, typingUsers,
    allMessages, setAiMessages,
    historyLoading, historyError, historyErrorObj, historyErrorMessage,
    refetchHistory,
  }
}
