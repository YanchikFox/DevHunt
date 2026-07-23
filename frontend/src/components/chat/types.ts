import type { Conversation, ConversationParticipant, MessageReactionSummary } from "@/lib/api/queries/chat"
import type { AiMessageMetadata, AiToolCall, AiUsageSummary } from "@/lib/signalr"

/**
 * Internal representation of a message for display purposes.
 */
export interface DisplayMessage {
  readonly id: string
  readonly senderId: string
  readonly senderFullName?: string
  readonly senderAvatarUrl?: string | null
  readonly content: string
  readonly createdAt: string
  readonly replyToId?: string | null
  readonly replyTo?: {
    readonly id: string
    readonly content?: string
    readonly fullName?: string
  } | null
  readonly isEdited: boolean
  readonly isPinned?: boolean
  readonly pinnedAt?: string | null
  readonly pinnedByUserId?: string | null
  readonly reactions?: MessageReactionSummary[]
  readonly isAiResponse?: boolean
  readonly aiUsage?: AiUsageSummary | null
  readonly aiToolCalls?: AiToolCall[] | null
  readonly aiMetadata?: AiMessageMetadata | null
  /** True while the assistant is still emitting deltas — drives the cursor + cancel button. */
  readonly aiStreaming?: boolean
  /** Backend request id, set on AiStreamStarted; needed to invoke /cancel. */
  readonly aiRequestId?: string
  /** True when the user pressed cancel and the partial reply was preserved. */
  readonly aiCancelled?: boolean
  /** True when the stream errored — content holds the error message. */
  readonly aiFailed?: boolean
}

/**
 * Props for the ChatWindow component.
 */
export interface ChatWindowProps {
  /** ID of the conversation to display. Use "new" for creating a new conversation. */
  readonly conversationId: string
  /** Initial conversation data if available */
  readonly conversation?: Conversation | null
  /** List of participants in the conversation */
  readonly participants?: ConversationParticipant[]
  /** User to start a new conversation with (required if conversationId is "new") */
  readonly recipientUser?: RecipientUser | null
  /** Callback when the close button is clicked */
  readonly onClose?: () => void
  /** Callback when the back button is clicked (mobile view) */
  readonly onBack?: () => void
  /** Additional CSS classes */
  readonly className?: string
  /** Whether to show the back button */
  readonly showBackButton?: boolean
  /** Whether to show the header */
  readonly showHeader?: boolean
  /** Project ID for AI context (enables /ai command in project chats) */
  readonly projectId?: string | null
  /** Whether the current user can use AI tools (requires canManageTasks permission) */
  readonly canUseAiTools?: boolean
  readonly canPinMessages?: boolean
  readonly canDeleteMessages?: boolean
}

export interface RecipientUser {
  readonly id: string
  readonly name: string
  readonly avatarUrl?: string | null
}

/**
 * Props for ChatMessage component.
 */
export interface ChatMessageProps {
  readonly msg: DisplayMessage
  readonly isOwn: boolean
  readonly currentUserId?: string
  readonly onReply?: (message: DisplayMessage) => void
  readonly onEdit?: (message: DisplayMessage) => void
  readonly onDelete?: (message: DisplayMessage) => void
  readonly onToggleReaction?: (message: DisplayMessage, emoji: string) => void
  readonly onTogglePin?: (message: DisplayMessage) => void
  readonly canDeleteMessage?: boolean
  readonly canPinMessage?: boolean
  /** Stop the in-flight AI stream. Provided when the message is the user's own /ai turn. */
  readonly onCancelAi?: (message: DisplayMessage) => void
  /** Re-run the latest /ai command. Shown on completed assistant messages. */
  readonly onRegenerateAi?: (message: DisplayMessage) => void
}

/**
 * Extended participant with merged details.
 */
export type MergedParticipant = ConversationParticipant & {
  readonly lastLogin?: string | null
  readonly lastReadAt?: string | null
}
