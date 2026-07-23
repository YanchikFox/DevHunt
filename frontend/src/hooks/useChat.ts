"use client"

import { useCallback, useEffect, useMemo, useState } from "react"
import {
  sendMessage as sendMessageHub,
  markAsRead,
  sendTyping,
  ChatMessage,
} from "@/lib/signalr"
import type { ConversationParticipant } from "@/lib/api/queries/chat"
import { useChatConnection } from "./useChatConnection"
import { useChatSignalRHandlers } from "./useChatSignalRHandlers"

type ParticipantInfo = {
  fullName?: string
  avatarUrl?: string | null
}

type ParticipantsLookup = Record<string, ParticipantInfo>

/**
 * Props for the useChat hook.
 */
export interface UseChatProps {
  /** ID of the active conversation. If null, chat is not active. */
  conversationId: string | null
  /** List of participants to enrich message data with sender details. */
  participants?: ConversationParticipant[]
}

/**
 * Hook to manage real-time chat functionality using SignalR.
 * Handles connection, message sending/receiving, typing indicators, and read receipts.
 *
 * @example
 * ```tsx
 * const { messages, sendMessage } = useChat({
 *   conversationId: "123",
 *   participants: [...]
 * })
 * ```
 */
export const useChat = ({ conversationId, participants }: UseChatProps) => {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [typingUsers, setTypingUsers] = useState<Set<string>>(new Set())

  const { connected, connectionError } = useChatConnection(conversationId)

  const participantLookup = useMemo<ParticipantsLookup | undefined>(() => {
    if (!participants) return undefined
    return participants.reduce<ParticipantsLookup>((acc, p) => {
      acc[p.userId] = { fullName: p.fullName, avatarUrl: p.avatarUrl }
      return acc
    }, {})
  }, [participants])

  const enrichMessage = useCallback(
    (message: ChatMessage): ChatMessage => {
      const senderId = message.senderId ?? message.sender?.id ?? ""
      const participant = senderId ? participantLookup?.[senderId] : undefined
      return {
        ...message,
        senderId,
        senderFullName: message.senderFullName ?? message.sender?.fullName ?? participant?.fullName,
        senderAvatarUrl: message.senderAvatarUrl ?? message.sender?.avatarUrl ?? participant?.avatarUrl ?? null,
        replyToId: message.replyToId ?? message.replyTo?.id ?? null,
      }
    },
    [participantLookup]
  )

  useChatSignalRHandlers(connected, conversationId, enrichMessage, setMessages, setTypingUsers)

  useEffect(() => {
    setMessages([])
    setTypingUsers(new Set())
  }, [conversationId])

  const sendMessage = useCallback(
    async (content: string) => {
      if (!conversationId || !connected) return
      await sendMessageHub(conversationId, content)
    },
    [conversationId, connected]
  )

  const markMessagesAsRead = useCallback(async () => {
    if (!conversationId || !connected) return
    try { await markAsRead(conversationId) }
    catch (error) { console.error("Failed to mark messages as read:", error) }
  }, [conversationId, connected])

  const setTyping = useCallback(
    async (isTypingIndicator: boolean) => {
      if (!conversationId || !connected) return
      try { await sendTyping(conversationId, isTypingIndicator) }
      catch (error) { console.error("Failed to send typing indicator:", error) }
    },
    [conversationId, connected]
  )

  return { connected, connectionError, messages, typingUsers, sendMessage, markMessagesAsRead, setTyping }
}
