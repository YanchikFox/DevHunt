import { apiClient } from "@/lib/api/client"
import {
  offAiStreamCancelled,
  offAiStreamCompleted,
  offAiStreamDelta,
  offAiStreamFailed,
  offAiStreamStarted,
  offAiToolCallsProposed,
  offAiToolsExecuted,
  onAiStreamCancelled,
  onAiStreamCompleted,
  onAiStreamDelta,
  onAiStreamFailed,
  onAiStreamStarted,
  onAiToolCallsProposed,
  onAiToolsExecuted,
  type AiStreamCancelledEvent,
  type AiStreamCompletedEvent,
  type AiStreamDeltaEvent,
  type AiStreamFailedEvent,
  type AiStreamStartedEvent,
  type AiToolCall,
  type AiToolCallsProposedEvent,
  type AiToolsExecutedEvent,
  type AiUsageSummary,
} from "@/lib/signalr"
import type { DisplayMessage } from "./types"

export interface AiResponse {
  readonly requestId: string
  readonly userMessageId: string
  readonly assistantMessageId: string
  readonly provider: string
  readonly modelId: string
  readonly message: string
  readonly usage?: AiUsageSummary | null
  readonly hasToolCalls: boolean
  readonly toolCalls: AiToolCall[]
}

export interface OptimisticMessageParams {
  readonly userId?: string
  readonly userName?: string
  readonly query: string
}

export interface AiRequestParams {
  readonly conversationId: string
  readonly query: string
  readonly projectId?: string | null
  /** P1-01: Only enable tools when user has canManageTasks permission. */
  readonly canUseTools?: boolean
  readonly provider?: string | null
  readonly modelId?: string | null
  readonly enableToolsPreference?: boolean
}

export function createOptimisticUserMessage({ userId, userName, query }: OptimisticMessageParams): DisplayMessage {
  return {
    id: `ai-user-${Date.now()}`,
    senderId: userId || "",
    senderFullName: userName || "You",
    content: `/ai ${query}`,
    createdAt: new Date().toISOString(),
    isEdited: false,
  }
}

export function createStreamingAssistantMessage(id: string, t: (key: string) => string): DisplayMessage {
  return {
    id,
    senderId: "ai-assistant",
    senderFullName: "AI Assistant",
    content: t("thinking"),
    createdAt: new Date().toISOString(),
    isEdited: false,
    isAiResponse: true,
  }
}

export function createErrorMessage(t: (key: string) => string): DisplayMessage {
  return {
    id: `ai-error-${Date.now()}`,
    senderId: "ai-assistant",
    senderFullName: "AI Assistant",
    content: t("processingError"),
    createdAt: new Date().toISOString(),
    isEdited: false,
    isAiResponse: true,
  }
}

export async function sendAiRequest({
  conversationId,
  query,
  projectId,
  canUseTools,
  provider,
  modelId,
  enableToolsPreference,
}: AiRequestParams): Promise<AiResponse> {
  const response = await apiClient.post<AiResponse>(`/ai/chat/conversations/${conversationId}`, {
    message: query,
    provider: provider || null,
    modelId: modelId || null,
    enableTools: enableToolsPreference === true && canUseTools === true && Boolean(projectId),
    useHistory: null,
  })

  return response.data
}

export function markMessageStreaming(
  messages: DisplayMessage[],
  messageId: string,
  requestId: string,
  initialContent: string
): DisplayMessage[] {
  return messages.map((message) =>
    message.id === messageId
      ? {
          ...message,
          content: initialContent,
          aiStreaming: true,
          aiRequestId: requestId,
          aiCancelled: false,
          aiFailed: false,
        }
      : message
  )
}

export function appendMessageContent(messages: DisplayMessage[], messageId: string, delta: string): DisplayMessage[] {
  return messages.map((message) =>
    message.id === messageId
      ? {
          ...message,
          content: message.content === "" ? delta : `${message.content}${delta}`,
        }
      : message
  )
}

export function finalizeStreamingMessage(
  messages: DisplayMessage[],
  messageId: string,
  usage: AiUsageSummary | null,
  toolCalls: AiToolCall[] | null
): DisplayMessage[] {
  return messages.map((message) =>
    message.id === messageId
      ? {
          ...message,
          aiUsage: usage,
          aiToolCalls: toolCalls,
          aiStreaming: false,
        }
      : message
  )
}

export function markMessageCancelled(
  messages: DisplayMessage[],
  messageId: string,
  partialText: string
): DisplayMessage[] {
  return messages.map((message) =>
    message.id === messageId
      ? {
          ...message,
          // Keep whatever streamed before the stop; if nothing came through,
          // fall back to the partial text the server kept for us.
          content: message.content && message.content.length > 0 ? message.content : partialText,
          aiStreaming: false,
          aiCancelled: true,
        }
      : message
  )
}

export function markMessageFailed(
  messages: DisplayMessage[],
  messageId: string,
  error: string
): DisplayMessage[] {
  return messages.map((message) =>
    message.id === messageId
      ? {
          ...message,
          content: error,
          aiStreaming: false,
          aiFailed: true,
        }
      : message
  )
}

export function isEventForCurrentUser(eventUserId: string | undefined, currentUserId: string | undefined): boolean {
  if (!eventUserId || !currentUserId) return true
  return eventUserId === currentUserId
}

export function isActiveAiEvent(
  event: { readonly requestId: string; readonly userId?: string },
  activeRequestId: string | null,
  currentUserId: string | undefined
): boolean {
  if (!isEventForCurrentUser(event.userId, currentUserId)) return false
  return activeRequestId === null || activeRequestId === event.requestId
}

export interface StreamHandlerBundle {
  readonly handleStarted: (event: AiStreamStartedEvent) => void
  readonly handleDelta: (event: AiStreamDeltaEvent) => void
  readonly handleCompleted: (event: AiStreamCompletedEvent) => void
  readonly handleFailed: (event: AiStreamFailedEvent) => void
  readonly handleCancelled: (event: AiStreamCancelledEvent) => void
  readonly handleToolsExecuted: (event: AiToolsExecutedEvent) => void
  readonly handleToolCallsProposed: (event: AiToolCallsProposedEvent) => void
}

export function registerAiStreamHandlers(handlers: StreamHandlerBundle): void {
  onAiStreamStarted(handlers.handleStarted)
  onAiStreamDelta(handlers.handleDelta)
  onAiStreamCompleted(handlers.handleCompleted)
  onAiStreamFailed(handlers.handleFailed)
  onAiStreamCancelled(handlers.handleCancelled)
  onAiToolsExecuted(handlers.handleToolsExecuted)
  onAiToolCallsProposed(handlers.handleToolCallsProposed)
}

export function unregisterAiStreamHandlers(handlers: StreamHandlerBundle): void {
  offAiStreamStarted(handlers.handleStarted)
  offAiStreamDelta(handlers.handleDelta)
  offAiStreamCompleted(handlers.handleCompleted)
  offAiStreamFailed(handlers.handleFailed)
  offAiStreamCancelled(handlers.handleCancelled)
  offAiToolsExecuted(handlers.handleToolsExecuted)
  offAiToolCallsProposed(handlers.handleToolCallsProposed)
}
