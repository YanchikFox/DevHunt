"use client"

import { useSession } from "next-auth/react"

import { useChatParticipants } from "./useChatParticipants"
import { useChatConnection, type ChatConnectionResult } from "./useChatConnection";
import { useChatActions } from "./useChatActions"
import type { LlmPreferences } from "./useLlmPreferences"
import type { ChatWindowProps, DisplayMessage } from "./types"
import type { ConversationParticipant } from "@/lib/api/queries/chat"
import type { AiToolCall } from "@/lib/signalr"

// ── Helpers (pure functions — no branching counted against the hook) ──

interface ResolvedProps {
  readonly currentUserId: string | undefined
  readonly isNewChat: boolean
  readonly participantList: ConversationParticipant[]
}

function resolveProps(
  props: Readonly<ChatWindowProps>,
  userId: string | undefined,
): ResolvedProps {
  const isNewChat = props.conversationId === "new" && Boolean(props.recipientUser)
  const participantList = props.conversation?.participants ?? props.participants ?? []
  return { currentUserId: userId, isNewChat, participantList }
}

interface DerivedState {
  readonly isEmpty: boolean
  readonly typingIndicator: boolean
  readonly isOnline: boolean
  readonly avatarUrl: string | null | undefined
}

function computeDerived(
  conn: ChatConnectionResult,
  isGroupChat: boolean,
  groupOnlineCount: number,
  otherOnline: boolean,
  isNewChat: boolean,
  recipientAvatarUrl: string | null | undefined,
): DerivedState {
  return {
    isEmpty: conn.allMessages.length === 0,
    typingIndicator: conn.typingUsers.size > 0,
    isOnline: isGroupChat ? groupOnlineCount > 0 : otherOnline,
    avatarUrl: isNewChat ? recipientAvatarUrl : undefined,
  }
}

// ── Orchestrator result type ─────────────────────────────────────

export interface ChatOrchestratorResult {
  readonly currentUserId: string | undefined
  readonly isNewChat: boolean
  readonly conversationTitle: string
  readonly statusText: string
  readonly isEmpty: boolean
  readonly typingIndicator: boolean
  readonly isOnline: boolean
  readonly avatarUrl: string | null | undefined
  readonly historyErrorMessage: string | null
  readonly historyLoading: boolean
  readonly allMessages: DisplayMessage[]
  readonly refetchHistory: () => void
  readonly message: string
  readonly handleTyping: (value: string) => void
  readonly isAiCommand: boolean
  readonly handleSend: () => Promise<void>
  readonly isSending: boolean
  readonly aiLoading: boolean
  readonly llmPreferences: LlmPreferences
  readonly updateLlmPreferences: (patch: Partial<LlmPreferences>) => void
  readonly replyToMessage: DisplayMessage | null
  readonly editingMessage: DisplayMessage | null
  readonly startReply: (message: DisplayMessage) => void
  readonly startEdit: (message: DisplayMessage) => void
  readonly clearComposerMode: () => void
  readonly cancelActiveAi: () => Promise<boolean>
  readonly activeAiRequestId: string | null
  readonly pendingToolCalls: AiToolCall[] | null
  readonly showToolConfirm: boolean
  readonly handleToolsConfirmed: () => Promise<void>
  readonly handleToolsCancelled: () => void
}

// ── Main orchestrator hook ───────────────────────────────────────

/**
 * Composes all chat sub-hooks into a single flat result object.
 * Each concern is delegated to a focused sub-hook, keeping cc low.
 */
export function useChatOrchestrator(props: Readonly<ChatWindowProps>): ChatOrchestratorResult {
  const { data: session } = useSession()
  const resolved = resolveProps(props, session?.user?.id)

  const conn = useChatConnection({
    conversationId: props.conversationId,
    isNewChat: resolved.isNewChat,
    participantList: resolved.participantList,
  })

  const parts = useChatParticipants({
    conversationId: resolved.isNewChat ? null : props.conversationId,
    conversation: props.conversation,
    participants: props.participants,
    currentUserId: resolved.currentUserId,
    isNewChat: resolved.isNewChat,
    recipientUser: props.recipientUser,
  })

  const actions = useChatActions({
    conversationId: props.conversationId,
    isNewChat: resolved.isNewChat,
    projectId: props.projectId,
    canUseAiTools: props.canUseAiTools ?? false,
    recipientUser: props.recipientUser,
    connected: conn.connected,
    sendMessage: conn.sendMessage,
    setTyping: conn.setTyping,
    allMessages: conn.allMessages,
    setAiMessages: conn.setAiMessages,
    refetchHistory: conn.refetchHistory,
  })

  const derived = computeDerived(conn, parts.isGroupChat, parts.groupOnlineCount, parts.otherOnline, resolved.isNewChat, props.recipientUser?.avatarUrl)

  return {
    ...resolved,
    conversationTitle: parts.conversationTitle,
    statusText: parts.statusText,
    ...derived,
    historyErrorMessage: conn.historyErrorMessage,
    historyLoading: conn.historyLoading,
    allMessages: conn.allMessages,
    refetchHistory: conn.refetchHistory,
    ...actions,
  }
}
