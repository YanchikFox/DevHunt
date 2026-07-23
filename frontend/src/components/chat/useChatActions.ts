"use client"

import { useCallback, useState } from "react"
import { useSession } from "next-auth/react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

import { useAiCommands } from "./useAiCommands"
import type { AiToolCall } from "@/lib/signalr"
import { useChatInput } from "./useChatInput"
import { useLlmPreferences, type LlmPreferences } from "./useLlmPreferences"
import { useSendHandler } from "./useSendHandler"
import type { DisplayMessage, RecipientUser } from "./types"

// ── Types ────────────────────────────────────────────────────────

interface UseChatActionsOptions {
  readonly conversationId: string
  readonly isNewChat: boolean
  readonly projectId?: string | null
  readonly canUseAiTools: boolean
  readonly recipientUser?: RecipientUser | null
  readonly connected: boolean
  readonly sendMessage: (content: string) => Promise<void>
  readonly setTyping: (typing: boolean) => void
  readonly allMessages: DisplayMessage[]
  readonly setAiMessages: React.Dispatch<React.SetStateAction<DisplayMessage[]>>
  readonly refetchHistory: () => void
}

export interface ChatActionsResult {
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

// ── Hook ─────────────────────────────────────────────────────────

/**
 * Composes input-state, AI-command handling, and send logic
 * into a single cohesive result.
 */
export function useChatActions(opts: UseChatActionsOptions): ChatActionsResult {
  const { data: session } = useSession()
  const { toast } = useToast()
  const t = useTranslations()
  const [replyToMessage, setReplyToMessage] = useState<DisplayMessage | null>(null)
  const [editingMessage, setEditingMessage] = useState<DisplayMessage | null>(null)
  const { preferences: llmPreferences, updatePreferences: updateLlmPreferences } = useLlmPreferences()
  const { message, setMessage, handleTyping, isAiCommand } = useChatInput({ setTyping: opts.setTyping })

  const clearComposerMode = useCallback(() => { setReplyToMessage(null); setEditingMessage(null) }, [])
  const startReply = useCallback((s: DisplayMessage) => { setEditingMessage(null); setReplyToMessage(s) }, [])
  const startEdit = useCallback((s: DisplayMessage) => { setReplyToMessage(null); setEditingMessage(s); setMessage(s.content) }, [setMessage])
  const showError = useCallback((title: string, description: string) => { toast({ title, description, variant: "destructive" }) }, [toast])

  const { aiLoading, handleAiCommand, cancelActiveAi, activeAiRequestId, pendingToolCalls, showToolConfirm, handleToolsConfirmed, handleToolsCancelled } = useAiCommands({
    conversationId: opts.conversationId, isNewChat: opts.isNewChat, projectId: opts.projectId,
    canUseTools: opts.canUseAiTools, provider: llmPreferences.provider,
    modelId: llmPreferences.modelId, enableToolsPreference: llmPreferences.enableTools,
    userId: session?.user?.id, userName: session?.user?.name ?? undefined,
    allMessages: opts.allMessages, setAiMessages: opts.setAiMessages,
    refetchHistory: opts.refetchHistory, showError,
  })

  const { handleSend, isSending } = useSendHandler({
    message, setMessage, editingMessageId: editingMessage?.id ?? null,
    replyToId: replyToMessage?.id ?? null, onSent: clearComposerMode,
    aiLoading, isNewChat: opts.isNewChat, recipientUser: opts.recipientUser,
    conversationId: opts.conversationId, connected: opts.connected,
    sendMessage: opts.sendMessage, setTyping: opts.setTyping, handleAiCommand,
    onError: showError, errorTitle: t("common.error"),
    fallbackErrorDescription: t("chat.sendMessageError"),
  })

  return {
    message, handleTyping, isAiCommand, handleSend, isSending, aiLoading,
    llmPreferences, updateLlmPreferences, replyToMessage, editingMessage,
    startReply, startEdit, clearComposerMode, cancelActiveAi, activeAiRequestId,
    pendingToolCalls, showToolConfirm, handleToolsConfirmed, handleToolsCancelled,
  }
}
