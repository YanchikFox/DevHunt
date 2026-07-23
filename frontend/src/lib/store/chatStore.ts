import { create } from "zustand"

/**
 * Represents a user who is the recipient of a chat message or conversation.
 */
export interface RecipientUser {
  /** Unique identifier of the user */
  id: string
  /** Display name of the user */
  name: string
  /** Optional URL to the user's avatar image */
  avatarUrl?: string | null
}

/**
 * Global state for the Chat Widget.
 * Manages visibility, active conversation, and temporary state for new chats.
 */
interface ChatState {
  /** Whether the chat widget (bottom right) is currently open */
  isOpen: boolean
  /** ID of the currently active conversation. Null if viewing the list or a new chat. */
  activeConversationId: string | null
  /**
   * Temporary user data when starting a new chat before the conversation is actually created on the backend.
   * Used to show the header with the user's name.
   */
  recipientUser: RecipientUser | null

  // Actions
  /** Opens the chat widget */
  openWidget: () => void
  /** Closes the chat widget */
  closeWidget: () => void
  /** Toggles the visibility of the chat widget */
  toggleWidget: () => void
  /**
   * Opens a specific existing conversation.
   * @param conversationId - The UUID of the conversation to open
   */
  openConversation: (conversationId: string) => void
  /**
   * Prepares the widget for a new direct message to a specific user.
   * @param user - The user to start chatting with
   */
  openNewChat: (user: RecipientUser) => void
  /** Resets the view to the conversation list */
  resetActiveChat: () => void
}

/**
 * Zustand store for managing the Chat UI state.
 *
 * @example
 * ```tsx
 * const { openConversation, isOpen } = useChatStore()
 *
 * // Open a chat
 * openConversation('123-abc')
 * ```
 */
export const useChatStore = create<ChatState>((set) => ({
  isOpen: false,
  activeConversationId: null,
  recipientUser: null,

  openWidget: () => set({ isOpen: true }),
  closeWidget: () => set({ isOpen: false }),
  toggleWidget: () => set((state) => ({ isOpen: !state.isOpen })),

  openConversation: (conversationId) =>
    set({
      isOpen: true,
      activeConversationId: conversationId,
      recipientUser: null,
    }),

  openNewChat: (user) =>
    set({
      isOpen: true,
      activeConversationId: null,
      recipientUser: user,
    }),

  resetActiveChat: () =>
    set({
      activeConversationId: null,
      recipientUser: null,
    }),
}))
