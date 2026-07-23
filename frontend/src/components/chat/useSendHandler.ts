"use client"

import { useCallback } from "react"
import { useCreateDirectConversation, useEditMessage, useSendMessage } from "@/lib/api/queries/chat"
import { apiClient } from "@/lib/api/client"
import { useChatStore } from "@/lib/store/chatStore"
import type { RecipientUser } from "./types"

// ── Pure helpers ─────────────────────────────────────────────────

/** Extract an AI query from the message, or return null for normal messages. */
function parseAiCommand(trimmed: string): string | null {
  if (!trimmed.toLowerCase().startsWith("/ai ")) return null
  const query = trimmed.slice(4).trim()
  return query || null
}

// ── Types ────────────────────────────────────────────────────────

interface UseSendHandlerOptions {
  readonly message: string
  readonly setMessage: (value: string) => void
  readonly editingMessageId?: string | null
  readonly replyToId?: string | null
  readonly onSent?: () => void
  readonly aiLoading: boolean
  readonly isNewChat: boolean
  readonly recipientUser?: RecipientUser | null
  readonly conversationId: string
  readonly connected: boolean
  readonly sendMessage: (content: string) => Promise<void>
  readonly setTyping: (typing: boolean) => void
  readonly handleAiCommand: (query: string) => Promise<void>
  readonly onError: (title: string, description: string) => void
  readonly errorTitle: string
  readonly fallbackErrorDescription: string
}

interface MessageDispatcherOptions {
  readonly isNewChat: boolean
  readonly recipientUser?: RecipientUser | null
  readonly conversationId: string
  readonly connected: boolean
  readonly sendMessage: (content: string) => Promise<void>
  readonly replyToId?: string | null
}

interface UseSendHandlerResult {
  readonly handleSend: () => Promise<void>
  readonly isSending: boolean
}

type ApiErrorShape = {
  readonly response?: {
    readonly data?: {
      readonly message?: string
      readonly error?: string
    }
  }
  readonly message?: string
}

function isApiErrorShape(value: unknown): value is ApiErrorShape {
  if (!value || typeof value !== "object") return false

  const message = Reflect.get(value, "message")
  if (message !== undefined && typeof message !== "string") return false

  const response = Reflect.get(value, "response")
  if (response === undefined) return true
  if (!response || typeof response !== "object") return false

  const data = Reflect.get(response, "data")
  if (data === undefined) return true
  if (!data || typeof data !== "object") return false

  const dataMessage = Reflect.get(data, "message")
  const dataError = Reflect.get(data, "error")
  return (
    (dataMessage === undefined || typeof dataMessage === "string") &&
    (dataError === undefined || typeof dataError === "string")
  )
}

// ── Message dispatcher (split to isolate routing cc) ─────────────

/** Hook that returns a stable callback to route a message to the correct transport. */
function useMessageDispatcher({
  isNewChat,
  recipientUser,
  conversationId,
  connected,
  sendMessage,
  replyToId,
}: MessageDispatcherOptions) {
  const createDirectConversation = useCreateDirectConversation()
  const sendMessageMutation = useSendMessage()
  const editMessageMutation = useEditMessage()
  const { openConversation } = useChatStore()

  const dispatch = useCallback(async (content: string, editingMessageId?: string | null): Promise<void> => {
    if (editingMessageId) {
      await editMessageMutation.mutateAsync({ messageId: editingMessageId, content })
      return
    }

    if (isNewChat && recipientUser) {
      const { conversationId: newId } = await createDirectConversation.mutateAsync(recipientUser.id)
      openConversation(newId)
      await apiClient.post(`/chat/conversations/${newId}/messages`, { content, replyToId: replyToId ?? null })
      return
    }

    if (!conversationId || conversationId === "new") {
      throw new Error("Conversation is not ready")
    }

    if (connected && !replyToId) {
      await sendMessage(content)
    } else {
      await sendMessageMutation.mutateAsync({ conversationId, content, replyToId: replyToId ?? null })
    }
  }, [isNewChat, recipientUser, conversationId, connected, sendMessage, createDirectConversation, openConversation, sendMessageMutation, editMessageMutation, replyToId])

  const isSending = createDirectConversation.isPending || sendMessageMutation.isPending || editMessageMutation.isPending

  return { dispatch, isSending } as const
}

// ── Main hook ────────────────────────────────────────────────────

/**
 * Hook encapsulating the send-message logic (new chat, AI command, live/fallback).
 * Extracted from ChatWindow to reduce cyclomatic complexity.
 */
export function useSendHandler({
  message,
  setMessage,
  editingMessageId,
  replyToId,
  onSent,
  aiLoading,
  isNewChat,
  recipientUser,
  conversationId,
  connected,
  sendMessage,
  setTyping,
  handleAiCommand,
  onError,
  errorTitle,
  fallbackErrorDescription,
}: UseSendHandlerOptions): UseSendHandlerResult {
  const { dispatch, isSending } = useMessageDispatcher({
    isNewChat, recipientUser, conversationId, connected, sendMessage, replyToId,
  })

  const handleSend = useCallback(async () => {
    const trimmed = message.trim()
    if (!trimmed || aiLoading) return

    const aiQuery = parseAiCommand(trimmed)
    if (aiQuery !== null) {
      setMessage("")
      await handleAiCommand(aiQuery)
      return
    }

    try {
      await dispatch(trimmed, editingMessageId)
      setMessage("")
      onSent?.()
    } catch (err: unknown) {
      let description = fallbackErrorDescription
      if (isApiErrorShape(err)) {
        if (err.response?.data?.message) {
          description = err.response.data.message
        } else if (err.response?.data?.error) {
          description = err.response.data.error
        } else if (err.message) {
          description = err.message
        }
      }
      onError(errorTitle, description)
    } finally {
      setTyping(false)
    }
  }, [message, aiLoading, dispatch, editingMessageId, setMessage, onSent, handleAiCommand, onError, errorTitle, fallbackErrorDescription, setTyping])

  return { handleSend, isSending }
}
