"use client"

import { useMemo, useState, useEffect, useCallback } from "react"
import { useMessages, type MessageReactionSummary } from "@/lib/api/queries/chat";
import type { DisplayMessage } from "./types"
import type { AiMessageMetadata, AiToolCall, AiUsageSummary } from "@/lib/signalr"

interface RawMessage {
  id: string
  senderId?: string
  sender?: { id: string; fullName?: string; avatarUrl?: string | null } | null
  senderFullName?: string
  senderAvatarUrl?: string | null
  content: string
  createdAt: string
  replyToId?: string | null
  replyTo?: { id: string; content?: string; fullName?: string } | null
  isEdited?: boolean
  isAiGenerated?: boolean
  isPinned?: boolean
  pinnedAt?: string | null
  pinnedByUserId?: string | null
  reactions?: MessageReactionSummary[]
  aiUsage?: AiUsageSummary | null
  aiToolCalls?: AiToolCall[] | null
  aiMetadata?: AiMessageMetadata | null
}

interface UseChatMessagesOptions {
  readonly conversationId: string
  readonly isNewChat: boolean
  readonly liveMessages: RawMessage[]
}

interface UseChatMessagesResult {
  readonly allMessages: DisplayMessage[]
  readonly aiMessages: DisplayMessage[]
  readonly setAiMessages: React.Dispatch<React.SetStateAction<DisplayMessage[]>>
  readonly historyLoading: boolean
  readonly historyError: boolean
  readonly historyErrorObj: Error | null
  readonly refetchHistory: () => void
}

/** Maps raw message to DisplayMessage */
function mapToDisplayMessage(entry: RawMessage): DisplayMessage {
  const aiMetadata = entry.aiMetadata ?? null

  return {
    id: entry.id,
    senderId: entry.senderId ?? entry.sender?.id ?? "",
    senderFullName: entry.senderFullName ?? entry.sender?.fullName,
    senderAvatarUrl: entry.senderAvatarUrl ?? entry.sender?.avatarUrl ?? null,
    content: entry.content,
    createdAt: entry.createdAt,
    replyToId: entry.replyToId ?? entry.replyTo?.id ?? null,
    replyTo: entry.replyTo ?? null,
    isEdited: entry.isEdited ?? false,
    isPinned: entry.isPinned ?? false,
    pinnedAt: entry.pinnedAt ?? null,
    pinnedByUserId: entry.pinnedByUserId ?? null,
    reactions: entry.reactions ?? [],
    isAiResponse: entry.isAiGenerated ?? false,
    aiUsage: entry.aiUsage ?? buildUsageFromMetadata(aiMetadata),
    aiToolCalls: entry.aiToolCalls ?? buildToolCallsFromMetadata(aiMetadata),
    aiMetadata,
  }
}

function buildUsageFromMetadata(metadata: AiMessageMetadata | null): AiUsageSummary | null {
  if (!metadata) return null
  const tokens = metadata.tokens
  const hasTokens = Boolean(tokens?.promptTokens || tokens?.completionTokens || tokens?.totalTokens)
  const hasCost = typeof metadata.costUsd === "number"
  if (!hasTokens && !hasCost) return null

  return {
    promptTokens: tokens?.promptTokens ?? null,
    completionTokens: tokens?.completionTokens ?? null,
    totalTokens: tokens?.totalTokens ?? null,
    estimatedCostUsd: metadata.costUsd ?? null,
  }
}

function buildToolCallsFromMetadata(metadata: AiMessageMetadata | null): AiToolCall[] | null {
  const detailCalls = metadata?.details?.toolCalls
  if (detailCalls && detailCalls.length > 0) {
    return detailCalls.map((call, index) => ({
      id: call.id ?? `tool-${index}`,
      type: "function",
      function: {
        name: call.name,
        arguments: call.arguments ?? "{}",
        argumentsParsed: call.argumentsParsed ?? null,
      },
    }))
  }

  const summaryCalls = metadata?.toolCalls
  if (!summaryCalls || summaryCalls.length === 0) return null

  return summaryCalls.map((call, index) => ({
    id: call.id ?? `tool-${index}`,
    type: "function",
    function: {
      name: call.name,
      arguments: "{}",
      argumentsParsed: null,
    },
  }))
}

/** Merges and sorts messages from multiple sources */
function mergeMessages(...sources: DisplayMessage[][]): DisplayMessage[] {
  const normalized = new Map<string, DisplayMessage>()
  sources.forEach(source => source.forEach(msg => normalized.set(msg.id, msg)))
  return Array.from(normalized.values()).sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  )
}

/**
 * Hook for managing chat messages - merges history, live, and AI messages.
 */
export function useChatMessages({
  conversationId,
  isNewChat,
  liveMessages,
}: UseChatMessagesOptions): UseChatMessagesResult {
  const [aiMessages, setAiMessages] = useState<DisplayMessage[]>([])

  const {
    data: historyData,
    isLoading: historyLoading,
    isError: historyError,
    error: historyErrorObj,
    refetch: refetchHistory,
  } = useMessages(isNewChat ? "" : conversationId)

  // Reset AI messages when switching conversations
  useEffect(() => {
    setAiMessages([])
  }, [conversationId])

  const historyDataItems = historyData?.data
  const historyMessages = useMemo(
    () => historyDataItems?.map(mapToDisplayMessage) ?? [],
    [historyDataItems]
  )

  const liveDisplayMessages = useMemo(
    () => liveMessages.map(mapToDisplayMessage),
    [liveMessages]
  )

  const allMessages = useMemo(
    () => mergeMessages(historyMessages, liveDisplayMessages, aiMessages),
    [historyMessages, liveDisplayMessages, aiMessages]
  )

  const handleRefetch = useCallback(() => {
    refetchHistory()
  }, [refetchHistory])

  return {
    allMessages,
    aiMessages,
    setAiMessages,
    historyLoading,
    historyError,
    historyErrorObj: historyErrorObj ?? null,
    refetchHistory: handleRefetch,
  }
}
