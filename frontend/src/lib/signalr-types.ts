/**
 * Represents a chat message received from the server.
 */
export interface ChatMessage {
  /** Unique ID of the message */
  id: string
  /** ID of the sender */
  senderId?: string
  /** Sender details */
  sender?: {
    id: string
    fullName?: string
    avatarUrl?: string | null
  } | null
  /** Message text content */
  content: string
  /** ISO timestamp of creation */
  createdAt: string
  /** ID of the conversation this message belongs to */
  conversationId?: string
  /** Whether the message has been edited */
  isEdited?: boolean
  /** ID of the message this is a reply to */
  replyToId?: string | null
  /** Details of the message being replied to */
  replyTo?: {
    id: string
    content?: string
    fullName?: string
  } | null
  isPinned?: boolean
  isAiGenerated?: boolean
  pinnedAt?: string | null
  pinnedByUserId?: string | null
  reactions?: MessageReactionSummary[]
  aiUsage?: AiUsageSummary | null
  aiToolCalls?: AiToolCall[] | null
  aiMetadata?: AiMessageMetadata | null
  // Fields from REST API (history):
  senderFullName?: string
  senderAvatarUrl?: string | null
}

export interface MessageReactionSummary {
  emoji: string
  count: number
  userIds: string[]
}

export interface MessageEditedEvent {
  id: string
  content: string
  isEdited: boolean
  editedAt?: string
}

export interface MessageReactionsUpdatedEvent {
  messageId: string
  reactions: MessageReactionSummary[]
}

export interface MessagePinUpdatedEvent {
  messageId: string
  isPinned: boolean
  pinnedAt?: string | null
  pinnedByUserId?: string | null
}

export interface AiUsageSummary {
  promptTokens?: number | null
  completionTokens?: number | null
  totalTokens?: number | null
  estimatedCostUsd?: number | null
}

export interface AiToolCallFunction {
  name: string
  arguments: string
  argumentsParsed?: Record<string, unknown> | null
}

export interface AiToolCall {
  id: string
  type: "function"
  function: AiToolCallFunction
}

export interface AiMessageTokenBreakdown {
  promptTokens?: number | null
  completionTokens?: number | null
  totalTokens?: number | null
}

export interface AiMessageToolCallSummary {
  id?: string | null
  name: string
  success?: boolean | null
}

export interface AiMessageToolCallDetail {
  id?: string | null
  name: string
  arguments?: string | null
  argumentsParsed?: Record<string, unknown> | null
  success?: boolean | null
  summary?: string | null
  error?: string | null
  resultJson?: string | null
}

export interface AiMessageDetailsPayload {
  redacted?: boolean | null
  toolCalls?: AiMessageToolCallDetail[] | null
  providerResponse?: {
    finishReason?: string | null
  } | null
  createdAt?: string | null
  retainedUntil?: string | null
}

export interface AiMessageMetadata {
  provider?: string | null
  modelId?: string | null
  tokens?: AiMessageTokenBreakdown | null
  costUsd?: number | null
  latencyMs?: number | null
  finishReason?: string | null
  toolCalls?: AiMessageToolCallSummary[] | null
  detailsAvailable?: boolean | null
  detailsRetentionDays?: number | null
  detailsRetainedUntil?: string | null
  details?: AiMessageDetailsPayload | null
}

export interface AiStreamStartedEvent {
  requestId: string
  userId?: string
  conversationId: string
  provider: string
  modelId: string
  /** Skill the assistant ran with — General / Summarize for now. */
  skill?: string
  /**
   * When true, this stream is the natural-language follow-up emitted after
   * the user confirmed a tool batch. The host UI can suppress a second
   * "thinking" placeholder and append the deltas to the original turn.
   */
  followUp?: boolean
  /** Round-trip: the original user-facing request id this follow-up belongs to. */
  originalRequestId?: string
}

export interface AiStreamDeltaEvent {
  requestId: string
  userId?: string
  delta: string
  followUp?: boolean
  originalRequestId?: string
}

export interface AiStreamCompletedEvent {
  requestId: string
  userId?: string
  messageId: string
  usage?: AiUsageSummary | null
  finishReason?: string
  hasToolCalls?: boolean
  toolCalls?: AiToolCall[] | null
  followUp?: boolean
  originalRequestId?: string
}

export interface AiStreamFailedEvent {
  requestId: string
  userId?: string
  error: string
  followUp?: boolean
  originalRequestId?: string
}

/**
 * Fired when a user-initiated /cancel terminates a stream mid-flight.
 * <see cref="partialText"/> is whatever the assistant managed to emit before
 * the stop, so the UI can keep the partial reply visible.
 */
export interface AiStreamCancelledEvent {
  requestId: string
  userId?: string
  partialText?: string
  followUp?: boolean
  originalRequestId?: string
}

export interface AiToolCallsProposedEvent {
  requestId: string
  userId?: string
  messageId: string
  toolCalls: AiToolCall[]
}

/**
 * Fired after the user confirms tool calls and the dispatcher finishes
 * executing them. The host UI uses this to refresh the kanban board / task
 * list without waiting on a manual refetch.
 */
export interface AiToolsExecutedEvent {
  conversationId: string
  projectId?: string | null
  messageId: string
  results: AiToolExecutionResultView[]
}

export interface AiToolExecutionResultView {
  id: string
  name: string
  success: boolean
  summary?: string | null
  error?: string | null
  resultJson: string
}

/**
 * Event payload for when a message is read.
 */
export interface MessageReadEvent {
  /** ID of the user who read the message */
  userId: string
  /** Timestamp when the message was read */
  readAt: string
}

/**
 * Event payload for when a user is typing.
 */
export interface UserTypingEvent {
  /** ID of the user who is typing */
  userId: string
  /** Whether the user is currently typing */
  isTyping: boolean
}

/**
 * Event payload for when a user joins a conversation.
 */
export interface UserJoinedEvent {
  /** ID of the user who joined */
  userId: string
  /** Username of the user who joined */
  username?: string
}
