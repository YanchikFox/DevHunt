"use client"

import { useCallback } from "react"
import { cn } from "@/lib/utils"
import {
  useDeleteMessage,
  useSetMessagePin,
  useToggleMessageReaction,
} from "@/lib/api/queries/chat"
import { useRegenerateAiMessage } from "@/lib/api/queries/llm"
import { useToast } from "@/hooks/use-toast"

import { ChatHeader } from "./ChatHeader"
import { ChatInput } from "./ChatInput"
import { ChatMessageList } from "./ChatMessageList"
import { ToolConfirmDialog } from "./ToolConfirmDialog"
import { useChatOrchestrator } from "./useChatOrchestrator"
import type { ChatWindowProps, DisplayMessage } from "./types"
import type { ToolCall } from "@/lib/api/agent-types"

// Re-export types for external use
export type { ChatWindowProps, DisplayMessage } from "./types"

/**
 * Main chat window component.
 * Delegates all hook composition to useChatOrchestrator,
 * keeping this file a thin render-only function.
 */
export function ChatWindow(props: Readonly<ChatWindowProps>) {
  const {
    showBackButton = true,
    showHeader = true,
    onBack,
    onClose,
    className,
    canPinMessages = true,
    canDeleteMessages = false,
  } = props

  const state = useChatOrchestrator(props)
  const { toast } = useToast()
  const deleteMessage = useDeleteMessage()
  const toggleReaction = useToggleMessageReaction()
  const setMessagePin = useSetMessagePin()
  const regenerateAi = useRegenerateAiMessage()

  const showActionError = useCallback((description: string) => {
    toast({ title: "Chat action failed", description, variant: "destructive" })
  }, [toast])

  const handleDelete = useCallback(async (message: DisplayMessage) => {
    try {
      await deleteMessage.mutateAsync(message.id)
    } catch (error) {
      showActionError(error instanceof Error ? error.message : "Could not delete message")
    }
  }, [deleteMessage, showActionError])

  const handleToggleReaction = useCallback(async (message: DisplayMessage, emoji: string) => {
    try {
      await toggleReaction.mutateAsync({ messageId: message.id, emoji })
    } catch (error) {
      showActionError(error instanceof Error ? error.message : "Could not update reaction")
    }
  }, [toggleReaction, showActionError])

  const handleTogglePin = useCallback(async (message: DisplayMessage) => {
    try {
      await setMessagePin.mutateAsync({ messageId: message.id, isPinned: !message.isPinned })
    } catch (error) {
      showActionError(error instanceof Error ? error.message : "Could not update pin")
    }
  }, [setMessagePin, showActionError])

  const { cancelActiveAi } = state
  const handleCancelAi = useCallback(async (_message: DisplayMessage) => {
    const ok = await cancelActiveAi()
    if (!ok) showActionError("No active AI request to cancel")
  }, [cancelActiveAi, showActionError])

  const conversationId = props.conversationId
  const llmPrefs = state.llmPreferences
  const refetchHistory = state.refetchHistory
  const canUseAiTools = props.canUseAiTools ?? false
  const projectId = props.projectId
  const handleRegenerateAi = useCallback(async (_message: DisplayMessage) => {
    if (!conversationId || conversationId === "new") return
    try {
      await regenerateAi.mutateAsync({
        conversationId,
        provider: llmPrefs.provider,
        modelId: llmPrefs.modelId,
        enableTools: llmPrefs.enableTools && canUseAiTools && Boolean(projectId),
      })
      refetchHistory()
    } catch (error) {
      showActionError(error instanceof Error ? error.message : "Could not regenerate response")
    }
  }, [regenerateAi, conversationId, llmPrefs.provider, llmPrefs.modelId, llmPrefs.enableTools, canUseAiTools, projectId, refetchHistory, showActionError])

  return (
    <div className={cn("flex h-full flex-col bg-background", className)}>
      {showHeader && (
        <ChatHeader
          title={state.conversationTitle}
          statusText={state.statusText}
          avatarUrl={state.avatarUrl}
          isOnline={state.isOnline}
          isNewChat={state.isNewChat}
          showBackButton={showBackButton}
          onBack={onBack}
          onClose={onClose}
        />
      )}

      <ChatMessageList
        messages={state.allMessages}
        currentUserId={state.currentUserId}
        isLoading={state.historyLoading}
        isEmpty={state.isEmpty}
        errorMessage={state.historyErrorMessage}
        onRefresh={state.refetchHistory}
        typingIndicator={state.typingIndicator}
        aiLoading={state.aiLoading}
        onReply={state.startReply}
        onEdit={state.startEdit}
        onDelete={handleDelete}
        onToggleReaction={handleToggleReaction}
        onTogglePin={handleTogglePin}
        canPinMessages={canPinMessages}
        canDeleteMessages={canDeleteMessages}
        onCancelAi={handleCancelAi}
        onRegenerateAi={handleRegenerateAi}
      />

      <ChatInput
        value={state.message}
        onChange={state.handleTyping}
        onSend={state.handleSend}
        mode={state.editingMessage ? "edit" : state.replyToMessage ? "reply" : "default"}
        contextMessage={state.editingMessage ?? state.replyToMessage}
        onCancelContext={state.clearComposerMode}
        disabled={state.isSending}
        isLoading={state.isSending || state.aiLoading}
        isAiCommand={state.isAiCommand}
        conversationId={props.conversationId}
        projectId={props.projectId}
        canUseAiTools={props.canUseAiTools ?? false}
        llmPreferences={state.llmPreferences}
        onLlmPreferencesChange={state.updateLlmPreferences}
      />

      <ToolConfirmDialog
        open={state.showToolConfirm}
        toolCalls={(state.pendingToolCalls ?? []) as ToolCall[]}
        onConfirm={state.handleToolsConfirmed}
        onCancel={state.handleToolsCancelled}
      />
    </div>
  )
}
