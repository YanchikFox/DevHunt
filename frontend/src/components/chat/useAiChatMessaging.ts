import { useState, useCallback, useRef, useEffect } from "react"
import { getToolIcon, getToolLabel, type AgentChatResponse, type ToolCall } from "@/lib/api/agent-types";
import type { ToolData } from "@/lib/api/queries/project-context"
import { useTranslations } from "next-intl"

export interface ChatMessage {
  id: string
  role: "user" | "assistant"
  content: string
  executedTools?: ExecutedTool[]
}

// Generate unique message ID
function generateMessageId(): string {
  return `msg-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`
}

interface ExecutedTool {
  name: string
  success: boolean
}

export interface ToolExecutionResult {
  toolName: string
  success: boolean
}

interface UseAiChatMessagingParams {
  agentEnabled: boolean
  projectId: string | null | undefined
  contextString: string
  toolData?: ToolData
  locale: string
}

interface UseAiChatMessagingReturn {
  messages: ChatMessage[]
  isLoading: boolean
  error: string | null
  pendingToolCalls: ToolCall[] | null
  pendingAiMessage: string | null
  showToolConfirm: boolean
  sendMessage: (input: string) => Promise<void>
  handleToolsConfirmed: (results: ToolExecutionResult[]) => void
  handleToolsCancelled: () => void
}

const API_ENDPOINTS = {
  agent: "/api/proxy-ml/ai/chat-agent",
  basic: "/api/proxy-ml/ai/chat",
} as const

interface ChatRequestParams {
  message: string
  history: ChatMessage[]
  context: string
  toolData?: ToolData
  projectId?: string
  enableTools: boolean
  language: string
}

async function fetchChatResponse(
  endpoint: string,
  params: ChatRequestParams,
  signal?: AbortSignal // P1-03: Support request cancellation
): Promise<AgentChatResponse> {
  const isAgent = endpoint === API_ENDPOINTS.agent
  const body: Record<string, unknown> = {
    message: params.message,
    history: params.history.map(({ role, content }) => ({ role, content })),
    context: params.context,
    language: params.language,
  }

  if (isAgent) {
    body.toolData = params.toolData
    body.projectId = params.projectId
    body.enableTools = params.enableTools
  }

  const response = await fetch(endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
    signal, // P1-03: Pass AbortController signal
  })

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}))
    throw new Error(errorData.detail || `HTTP ${response.status}`)
  }

  return response.json()
}

function formatToolLine(tool: ExecutedTool, locale: string): string {
  const icon = tool.success ? "✓" : "✗"
  return `${icon} ${getToolIcon(tool.name)} ${getToolLabel(tool.name, locale)}`
}

function buildToolResultContent(
  pendingMessage: string | null,
  results: ToolExecutionResult[],
  locale: string,
  t: (key: string, v?: Record<string, string | number>) => string // P3-03: no `any`
): string {
  const successCount = results.filter((r) => r.success).length
  const failCount = results.length - successCount

  const statusParts: string[] = []

  if (successCount > 0) {
    statusParts.push(`✅ ${t("status.executed")} ${successCount}`)
  }
  if (failCount > 0) {
    statusParts.push(`⚠️ ${failCount} ${t("status.failed")}`)
  }

  const toolLines = results.map((r) =>
    formatToolLine({ name: r.toolName, success: r.success }, locale)
  )

  return [pendingMessage, statusParts.join(", "), ...toolLines]
    .filter(Boolean)
    .join("\n\n")
}

function extractErrorMessage(err: unknown): string {
  return err instanceof Error ? err.message : "Failed to send message"
}

interface ToolConfirmationState {
  pendingToolCalls: ToolCall[] | null
  pendingAiMessage: string | null
  showToolConfirm: boolean
  setPendingState: (toolCalls: ToolCall[], message: string | null) => void
  clearPendingState: () => void
}

function useToolConfirmation(): ToolConfirmationState {
  const [pendingToolCalls, setPendingToolCalls] = useState<ToolCall[] | null>(null)
  const [pendingAiMessage, setPendingAiMessage] = useState<string | null>(null)
  const [showToolConfirm, setShowToolConfirm] = useState(false)

  const setPendingState = useCallback((toolCalls: ToolCall[], message: string | null) => {
    setPendingToolCalls(toolCalls)
    setPendingAiMessage(message)
    setShowToolConfirm(true)
  }, [])

  const clearPendingState = useCallback(() => {
    setPendingToolCalls(null)
    setPendingAiMessage(null)
    setShowToolConfirm(false)
  }, [])

  return {
    pendingToolCalls,
    pendingAiMessage,
    showToolConfirm,
    setPendingState,
    clearPendingState,
  }
}

interface ChatMessagesState {
  messages: ChatMessage[]
  addUserMessage: (content: string) => void
  addAssistantMessage: (content: string, executedTools?: ExecutedTool[]) => void
  removeLastMessage: () => void
}

function useChatMessagesState(): ChatMessagesState {
  const [messages, setMessages] = useState<ChatMessage[]>([])

  const addUserMessage = useCallback((content: string) => {
    setMessages((prev) => [...prev, { id: generateMessageId(), role: "user", content }])
  }, [])

  const addAssistantMessage = useCallback((content: string, executedTools?: ExecutedTool[]) => {
    setMessages((prev) => [...prev, { id: generateMessageId(), role: "assistant", content, executedTools }])
  }, [])

  const removeLastMessage = useCallback(() => {
    setMessages((prev) => prev.slice(0, -1))
  }, [])

  return { messages, addUserMessage, addAssistantMessage, removeLastMessage }
}

interface UseSendMessageParams {
  endpoint: string
  chatState: ChatMessagesState
  toolState: ToolConfirmationState
  contextString: string
  toolData?: ToolData
  projectId: string | null | undefined
  agentEnabled: boolean
  locale: string
}

interface UseSendMessageReturn {
  isLoading: boolean
  error: string | null
  sendMessage: (input: string) => Promise<void>
}

function useSendMessage({
  endpoint,
  chatState,
  toolState,
  contextString,
  toolData,
  projectId,
  agentEnabled,
  locale,
}: UseSendMessageParams): UseSendMessageReturn {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const abortControllerRef = useRef<AbortController | null>(null) // P1-03: Track in-flight request

  // P1-03: Cleanup on unmount
  useEffect(() => {
    return () => abortControllerRef.current?.abort()
  }, [])

  const sendMessage = useCallback(async (input: string) => {
    const trimmedInput = input.trim()
    if (!trimmedInput || isLoading) return

    setError(null)
    setIsLoading(true)
    chatState.addUserMessage(trimmedInput)

    try {
      // P1-03: Abort previous in-flight request
      abortControllerRef.current?.abort()
      const controller = new AbortController()
      abortControllerRef.current = controller

      const processResponse = (data: AgentChatResponse): void => { // P0-02: typed instead of `any`
        if (data.toolCalls?.length) {
          toolState.setPendingState(data.toolCalls, data.message ?? null)
        } else {
          chatState.addAssistantMessage(data.message ?? "")
        }
      }

      const data = await fetchChatResponse(endpoint, {
        message: trimmedInput,
        // P2-02: Limit history to last 10 messages (ML truncates to 10 anyway)
        history: chatState.messages.slice(-10),
        context: contextString,
        toolData,
        projectId: projectId || undefined,
        enableTools: agentEnabled,
        language: locale,
      }, controller.signal) // P1-03: pass signal

      processResponse(data)
    } catch (err) {
      console.error("AI Chat error:", err)
      setError(extractErrorMessage(err))
      chatState.removeLastMessage()
    } finally {
      setIsLoading(false)
    }
  }, [isLoading, chatState, toolState, endpoint, contextString, toolData, projectId, agentEnabled, locale])

  return { isLoading, error, sendMessage }
}

interface UseToolHandlersParams {
  chatState: ChatMessagesState
  toolState: ToolConfirmationState
  locale: string
  t: (key: string) => string
}

interface UseToolHandlersReturn {
  handleToolsConfirmed: (results: ToolExecutionResult[]) => void
  handleToolsCancelled: () => void
}

function useToolHandlers({
  chatState,
  toolState,
  locale,
  t,
}: UseToolHandlersParams): UseToolHandlersReturn {

  const handleToolsConfirmed = useCallback((results: ToolExecutionResult[]) => {
    const content = buildToolResultContent(toolState.pendingAiMessage, results, locale, t)
    const executedTools = results.map((r) => ({ name: r.toolName, success: r.success }))
    chatState.addAssistantMessage(content, executedTools)
    toolState.clearPendingState()
  }, [chatState, toolState, locale, t])

  const handleToolsCancelled = useCallback(() => {
    if (toolState.pendingAiMessage) {
      const cancelText = t("status.cancelled")
      const content = `${toolState.pendingAiMessage}\n\n_${cancelText}_`
      chatState.addAssistantMessage(content)
    }
    toolState.clearPendingState()
  }, [chatState, toolState, t])

  return { handleToolsConfirmed, handleToolsCancelled }
}

/**
 * Hook to manage AI chat messaging state and operations.
 * Composes smaller focused hooks for maintainability.
 */
export function useAiChatMessaging({
  agentEnabled,
  projectId,
  contextString,
  toolData,
  locale,
}: UseAiChatMessagingParams): UseAiChatMessagingReturn {
  const chatState = useChatMessagesState()
  const toolState = useToolConfirmation()
  const t = useTranslations("ai")

  const endpoint = agentEnabled && projectId ? API_ENDPOINTS.agent : API_ENDPOINTS.basic

  const { isLoading, error, sendMessage } = useSendMessage({
    endpoint,
    chatState,
    toolState,
    contextString,
    toolData,
    projectId,
    agentEnabled,
    locale,
  })

  const { handleToolsConfirmed, handleToolsCancelled } = useToolHandlers({
    chatState,
    toolState,
    locale,
    t,
  })

  return {
    messages: chatState.messages,
    isLoading,
    error,
    pendingToolCalls: toolState.pendingToolCalls,
    pendingAiMessage: toolState.pendingAiMessage,
    showToolConfirm: toolState.showToolConfirm,
    sendMessage,
    handleToolsConfirmed,
    handleToolsCancelled,
  }
}
