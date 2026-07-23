"use client"

import { useEffect } from "react"
import { useQueryClient } from "@tanstack/react-query"
import {
  onReceiveMessage, onMessageRead, onUserTyping, onUserJoined, onUserLeft,
  onMessageEdited, onMessageDeleted, onMessageReactionsUpdated, onMessagePinUpdated,
  onAiStreamCompleted, onAiToolCallsProposed,
  offReceiveMessage, offMessageRead, offUserTyping, offUserJoined, offUserLeft,
  offMessageEdited, offMessageDeleted, offMessageReactionsUpdated, offMessagePinUpdated,
  offAiStreamCompleted, offAiToolCallsProposed,
} from "@/lib/signalr"
import type {
  ChatMessage, MessageEditedEvent, MessageReactionsUpdatedEvent,
  MessagePinUpdatedEvent, AiStreamCompletedEvent, AiToolCallsProposedEvent,
} from "@/lib/signalr"

export function useChatSignalRHandlers(
  connected: boolean,
  conversationId: string | null,
  enrichMessage: (msg: ChatMessage) => ChatMessage,
  setMessages: React.Dispatch<React.SetStateAction<ChatMessage[]>>,
  setTypingUsers: React.Dispatch<React.SetStateAction<Set<string>>>,
) {
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!connected) return undefined

    const handleReceiveMessage = (message: ChatMessage) => {
      const normalized = enrichMessage(message)
      const targetId = normalized.conversationId ?? conversationId
      queryClient.invalidateQueries({ queryKey: ["chat", "conversations"] })
      if (!conversationId || targetId !== conversationId) return
      if (normalized.replyToId) queryClient.invalidateQueries({ queryKey: ["chat", "messages", targetId] })
      setMessages((prev) => prev.some((m) => m.id === normalized.id) ? prev : [...prev, normalized])
    }

    const handleUserTyping = (userId: string, isTyping: boolean) => {
      setTypingUsers((prev) => {
        const next = new Set(prev)
        if (isTyping) next.add(userId)
        else next.delete(userId)
        return next
      })
    }

    const handleMessageEdited = (event: MessageEditedEvent) => {
      setMessages((prev) => prev.map((m) => m.id === event.id ? { ...m, content: event.content, isEdited: event.isEdited } : m))
      queryClient.invalidateQueries({ queryKey: ["chat", "messages", conversationId] })
      queryClient.invalidateQueries({ queryKey: ["chat", "conversations"] })
    }

    const handleMessageDeleted = (messageId: string) => {
      setMessages((prev) => prev.filter((m) => m.id !== messageId))
      queryClient.invalidateQueries({ queryKey: ["chat", "messages", conversationId] })
      queryClient.invalidateQueries({ queryKey: ["chat", "conversations"] })
    }

    const handleMessageReactionsUpdated = (event: MessageReactionsUpdatedEvent) => {
      setMessages((prev) => prev.map((m) => m.id === event.messageId ? { ...m, reactions: event.reactions } : m))
      queryClient.invalidateQueries({ queryKey: ["chat", "messages", conversationId] })
    }

    const handleMessagePinUpdated = (event: MessagePinUpdatedEvent) => {
      setMessages((prev) => prev.map((m) => m.id === event.messageId
        ? { ...m, isPinned: event.isPinned, pinnedAt: event.pinnedAt ?? null, pinnedByUserId: event.pinnedByUserId ?? null }
        : m))
      queryClient.invalidateQueries({ queryKey: ["chat", "messages", conversationId] })
    }

    const handleAiStreamCompleted = (event: AiStreamCompletedEvent) => {
      setMessages((prev) => prev.map((m) => m.id === event.messageId
        ? { ...m, aiUsage: event.usage ?? null, aiToolCalls: event.toolCalls ?? null }
        : m))
    }

    const handleAiToolCallsProposed = (event: AiToolCallsProposedEvent) => {
      setMessages((prev) => prev.map((m) => m.id === event.messageId ? { ...m, aiToolCalls: event.toolCalls } : m))
    }

    const noop = () => {}

    onReceiveMessage(handleReceiveMessage)
    onMessageRead(noop)
    onUserTyping(handleUserTyping)
    onUserJoined(noop)
    onUserLeft(noop)
    onMessageEdited(handleMessageEdited)
    onMessageDeleted(handleMessageDeleted)
    onMessageReactionsUpdated(handleMessageReactionsUpdated)
    onMessagePinUpdated(handleMessagePinUpdated)
    onAiStreamCompleted(handleAiStreamCompleted)
    onAiToolCallsProposed(handleAiToolCallsProposed)

    return () => {
      offReceiveMessage(handleReceiveMessage)
      offMessageRead(noop)
      offUserTyping(handleUserTyping)
      offUserJoined(noop)
      offUserLeft(noop)
      offMessageEdited(handleMessageEdited)
      offMessageDeleted(handleMessageDeleted)
      offMessageReactionsUpdated(handleMessageReactionsUpdated)
      offMessagePinUpdated(handleMessagePinUpdated)
      offAiStreamCompleted(handleAiStreamCompleted)
      offAiToolCallsProposed(handleAiToolCallsProposed)
    }
  }, [connected, conversationId, enrichMessage, queryClient, setMessages, setTypingUsers])
}
