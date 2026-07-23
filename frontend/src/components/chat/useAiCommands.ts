"use client"

import { useCallback, useRef, useState, type Dispatch, type SetStateAction } from "react"
import { useTranslations } from "next-intl"
import { useQueryClient } from "@tanstack/react-query"
import { apiClient } from "@/lib/api/client"
import type {
  AiStreamCancelledEvent,
  AiStreamCompletedEvent,
  AiStreamDeltaEvent,
  AiStreamFailedEvent,
  AiStreamStartedEvent,
  AiToolCall,
  AiToolCallsProposedEvent,
  AiToolsExecutedEvent,
} from "@/lib/signalr"
import type { DisplayMessage } from "./types"
import {
  appendMessageContent,
  createErrorMessage,
  createOptimisticUserMessage,
  createStreamingAssistantMessage,
  finalizeStreamingMessage,
  isActiveAiEvent,
  isEventForCurrentUser,
  markMessageCancelled,
  markMessageFailed,
  markMessageStreaming,
  registerAiStreamHandlers,
  sendAiRequest,
  unregisterAiStreamHandlers,
} from "./ai-commands-helpers"

interface UseAiCommandsOptions {
  readonly conversationId: string
  readonly isNewChat: boolean
  readonly projectId?: string | null
  /** P1-01: Only enable AI tools when user has canManageTasks permission. */
  readonly canUseTools?: boolean
  readonly provider?: string | null
  readonly modelId?: string | null
  readonly enableToolsPreference?: boolean
  readonly userId?: string
  readonly userName?: string
  readonly allMessages: DisplayMessage[]
  readonly setAiMessages: Dispatch<SetStateAction<DisplayMessage[]>>
  readonly refetchHistory: () => void
  readonly showError: (title: string, description: string) => void
}

interface UseAiCommandsResult {
  readonly aiLoading: boolean
  readonly handleAiCommand: (query: string) => Promise<void>
  /**
   * Cancels the currently-streaming /ai turn, if any. Returns true if a cancel
   * was issued; false when there's no live request the caller could stop.
   */
  readonly cancelActiveAi: () => Promise<boolean>
  readonly activeAiRequestId: string | null
  readonly pendingToolCalls: AiToolCall[] | null
  readonly showToolConfirm: boolean
  readonly handleToolsConfirmed: () => Promise<void>
  readonly handleToolsCancelled: () => void
}

/** Checks if AI commands are available. */
function isAiCommandAvailable(conversationId: string, isNewChat: boolean): boolean {
  if (isNewChat) return false
  if (!conversationId) return false
  if (conversationId === "new") return false
  return true
}

/** Gets localized error for unavailable AI. */
function getAiUnavailableError(t: (key: string) => string) {
  return {
    title: t("errorTitle"),
    description: t("commandsUnavailable"),
  }
}

/**
 * Hook for handling AI /ai commands in chat.
 *
 * The backend now owns persistence, context selection, BYOK resolution and
 * provider streaming. Frontend only creates temporary UI placeholders while
 * SignalR deltas arrive, then refreshes persisted history.
 */
export function useAiCommands({
  conversationId,
  isNewChat,
  projectId,
  canUseTools = false,
  provider,
  modelId,
  enableToolsPreference = true,
  userId,
  userName,
  setAiMessages,
  refetchHistory,
  showError,
}: UseAiCommandsOptions): UseAiCommandsResult {
  const [aiLoading, setAiLoading] = useState(false)
  const [activeAiRequestId, setActiveAiRequestId] = useState<string | null>(null)
  // The latest request id is also held in a ref so the cancel callback (which
  // captures it once on render) can read the freshest value without depending
  // on activeAiRequestId in its closure.
  const activeRequestIdRef = useRef<string | null>(null)
  const [pendingToolCalls, setPendingToolCalls] = useState<AiToolCall[] | null>(null)
  const [pendingRequestId, setPendingRequestId] = useState<string | null>(null)
  const [showToolConfirm, setShowToolConfirm] = useState(false)
  const t = useTranslations("ai")
  const queryClient = useQueryClient()

  const handleToolsConfirmed = useCallback(async () => {
    if (!pendingToolCalls || !conversationId || conversationId === "new") return
    try {
      await apiClient.post(`/ai/chat/conversations/${conversationId}/tools/execute`, {
        originalRequestId: pendingRequestId,
        toolCalls: pendingToolCalls.map(tc => ({
          id: tc.id,
          name: tc.function.name,
          arguments: tc.function.arguments,
        })),
      })
    } catch (err) {
      console.error("Tool execution failed:", err)
    } finally {
      setPendingToolCalls(null)
      setPendingRequestId(null)
      setShowToolConfirm(false)
    }
  }, [pendingToolCalls, pendingRequestId, conversationId])

  const handleToolsCancelled = useCallback(() => {
    setPendingToolCalls(null)
    setPendingRequestId(null)
    setShowToolConfirm(false)
  }, [])

  const cancelActiveAi = useCallback(async (): Promise<boolean> => {
    const reqId = activeRequestIdRef.current
    if (!reqId || !conversationId || conversationId === "new") return false
    try {
      await apiClient.post(`/ai/chat/conversations/${conversationId}/cancel/${reqId}`)
      return true
    } catch (error) {
      console.error("Failed to cancel AI stream:", error)
      return false
    }
  }, [conversationId])

  const handleAiCommand = useCallback(async (query: string) => {
    if (!isAiCommandAvailable(conversationId, isNewChat)) {
      const { title, description } = getAiUnavailableError(t)
      showError(title, description)
      return
    }

    const assistantPlaceholderId = `ai-stream-${Date.now()}`
    const userMsg = createOptimisticUserMessage({ userId, userName, query })
    const assistantMsg = createStreamingAssistantMessage(assistantPlaceholderId, t)

    setAiMessages((prev) => [...prev, userMsg, assistantMsg])
    setAiLoading(true)

    let localActiveRequestId: string | null = null
    const setActive = (id: string | null) => {
      localActiveRequestId = id
      activeRequestIdRef.current = id
      setActiveAiRequestId(id)
    }

    const handleStarted = (event: AiStreamStartedEvent) => {
      if (!isEventForCurrentUser(event.userId, userId)) return
      // Only the initial turn allocates the placeholder. Round-trip follow-up
      // turns share the same conversation but their deltas are appended onto
      // a separate persisted message — let those flow through the normal
      // ReceiveMessage path instead of trying to merge into our placeholder.
      if (event.followUp) return
      setActive(event.requestId)
      setAiMessages((prev) =>
        markMessageStreaming(prev, assistantPlaceholderId, event.requestId, "")
      )
    }

    const handleDelta = (event: AiStreamDeltaEvent) => {
      if (event.followUp) return
      if (!isActiveAiEvent(event, localActiveRequestId, userId)) return
      setActive(event.requestId)
      setAiMessages((prev) => appendMessageContent(prev, assistantPlaceholderId, event.delta))
    }

    const handleCompleted = (event: AiStreamCompletedEvent) => {
      if (event.followUp) return
      if (!isActiveAiEvent(event, localActiveRequestId, userId)) return
      setAiMessages((prev) =>
        finalizeStreamingMessage(prev, assistantPlaceholderId, event.usage ?? null, event.toolCalls ?? null)
      )
    }

    const handleFailed = (event: AiStreamFailedEvent) => {
      if (event.followUp) return
      if (!isActiveAiEvent(event, localActiveRequestId, userId)) return
      setAiMessages((prev) =>
        markMessageFailed(prev, assistantPlaceholderId, event.error || t("processingError"))
      )
    }

    const handleCancelled = (event: AiStreamCancelledEvent) => {
      if (event.followUp) return
      if (!isActiveAiEvent(event, localActiveRequestId, userId)) return
      setAiMessages((prev) =>
        markMessageCancelled(prev, assistantPlaceholderId, event.partialText ?? "")
      )
    }

    const handleToolsExecuted = (event: AiToolsExecutedEvent) => {
      // Tool executions mutate the kanban state — invalidate the relevant
      // task queries so the board UI repaints without a manual refresh.
      // Project ID is the source of truth; we don't filter by user because
      // any participant in the project channel may benefit.
      if (event.projectId) {
        queryClient.invalidateQueries({ queryKey: ["tasks", event.projectId] })
        queryClient.invalidateQueries({ queryKey: ["projects", event.projectId] })
      }
    }

    const handleToolCallsProposed = (event: AiToolCallsProposedEvent) => {
      if (!isEventForCurrentUser(event.userId, userId)) return
      setPendingRequestId(event.requestId)
      setPendingToolCalls(event.toolCalls)
      setShowToolConfirm(true)
    }

    registerAiStreamHandlers({
      handleStarted,
      handleDelta,
      handleCompleted,
      handleFailed,
      handleCancelled,
      handleToolsExecuted,
      handleToolCallsProposed,
    })

    try {
      await sendAiRequest({
        conversationId,
        query,
        projectId,
        canUseTools,
        provider,
        modelId,
        enableToolsPreference,
      })
      setAiMessages([])
      refetchHistory()
    } catch (error) {
      console.error("AI command error:", error)
      setAiMessages([createErrorMessage(t)])
    } finally {
      unregisterAiStreamHandlers({
        handleStarted,
        handleDelta,
        handleCompleted,
        handleFailed,
        handleCancelled,
        handleToolsExecuted,
        handleToolCallsProposed,
      })
      setActive(null)
      setAiLoading(false)
    }
  }, [conversationId, isNewChat, projectId, canUseTools, provider, modelId, enableToolsPreference, userId, userName, setAiMessages, refetchHistory, showError, t, queryClient])

  return { aiLoading, handleAiCommand, cancelActiveAi, activeAiRequestId, pendingToolCalls, showToolConfirm, handleToolsConfirmed, handleToolsCancelled }
}
