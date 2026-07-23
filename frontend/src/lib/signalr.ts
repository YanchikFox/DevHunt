import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel,
  type IHttpConnectionOptions,
  type ILogger,
} from "@microsoft/signalr"

let connection: HubConnection | null = null

// Custom Logger System
/**
 * Function signature for log handlers.
 */
type LogHandler = (level: string, message: string) => void
const logHandlers: Set<LogHandler> = new Set()

/**
 * Subscribes a handler to SignalR logs.
 * @param handler - The function to call when a log event occurs.
 * @returns A cleanup function to unsubscribe the handler.
 */
export const subscribeToSignalRLogs = (handler: LogHandler) => {
  logHandlers.add(handler)
  return () => {
    logHandlers.delete(handler)
  }
}

const logToHandlers = (level: string, message: string) => {
  logHandlers.forEach((h) => h(level, message))
}

class CustomLogger implements ILogger {
  log(logLevel: LogLevel, message: string) {
    const levelMap: Record<LogLevel, string> = {
      [LogLevel.Trace]: "trace",
      [LogLevel.Debug]: "debug",
      [LogLevel.Information]: "info",
      [LogLevel.Warning]: "warn",
      [LogLevel.Error]: "error",
      [LogLevel.Critical]: "critical",
      [LogLevel.None]: "none",
    }
    const level = levelMap[logLevel] || "info"

    // Filter out keep-alive ping messages to reduce noise if needed,
    // but user wants detailed logs, so let's keep them or maybe filter 'trace'
    if (logLevel >= LogLevel.Information) {
      logToHandlers(level, message)
    }
  }
}

import type {
  ChatMessage,
  MessageEditedEvent,
  MessageReactionsUpdatedEvent,
  MessagePinUpdatedEvent,
  AiStreamStartedEvent,
  AiStreamDeltaEvent,
  AiStreamCompletedEvent,
  AiStreamFailedEvent,
  AiStreamCancelledEvent,
  AiToolCallsProposedEvent,
  AiToolsExecutedEvent,
} from "./signalr-types"

export type {
  ChatMessage,
  MessageEditedEvent,
  MessageReactionsUpdatedEvent,
  MessagePinUpdatedEvent,
  AiStreamStartedEvent,
  AiStreamDeltaEvent,
  AiStreamCompletedEvent,
  AiStreamFailedEvent,
  AiStreamCancelledEvent,
  AiToolCallsProposedEvent,
  AiToolsExecutedEvent,
}

export type {
  MessageReactionSummary,
  AiUsageSummary,
  AiToolCallFunction,
  AiToolCall,
  AiMessageTokenBreakdown,
  AiMessageToolCallSummary,
  AiMessageToolCallDetail,
  AiMessageDetailsPayload,
  AiMessageMetadata,
  AiToolExecutionResultView,
  MessageReadEvent,
  UserTypingEvent,
  UserJoinedEvent,
} from "./signalr-types"

/** Gets or creates the singleton SignalR connection instance. */
export const getChatConnection = (accessToken?: string): HubConnection => {
  if (!connection) {
    // Use explicit WS URL if set, otherwise default to relative path (proxied by Next.js/Nginx)
    // If running in SSR (no window), fallback to localhost:7002 or similar if needed, but SignalR usually runs on client.
    const baseUrl =
      process.env.NEXT_PUBLIC_WS_URL ||
      (typeof window !== "undefined" ? "" : "http://localhost:7002")
    const url = `${baseUrl}/chatHub`

    logToHandlers("info", `🔌 Initializing SignalR connection to: ${url}`)
    logToHandlers(
      "info",
      `🔌 Access token provided: ${Boolean(accessToken)} (Length: ${accessToken?.length ?? 0})`
    )

    const connectionOptions: IHttpConnectionOptions = {
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      withCredentials: true,
    }

    if (accessToken) {
      connectionOptions.accessTokenFactory = () => {
        logToHandlers("debug", "🔌 Providing explicit access token to SignalR")
        return accessToken
      }
    }

    connection = new HubConnectionBuilder()
      .withUrl(url, connectionOptions)
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(new CustomLogger())
      .build()

    connection.onreconnecting((error) => {
      logToHandlers("warn", `🔌 SignalR reconnecting: ${error}`)
    })

    connection.onreconnected((connectionId) => {
      logToHandlers("info", `✅ SignalR reconnected. Connection ID: ${connectionId}`)
    })

    connection.onclose((error) => {
      logToHandlers("error", `❌ SignalR connection closed: ${error}`)
    })
  } else if (accessToken) {
    // If connection exists but we have a new token (e.g. refresh), we might need to warn or handle it.
    // SignalR doesn't support changing accessTokenFactory on the fly easily without restart.
    // For now, just log.
    logToHandlers("debug", "🔌 getChatConnection called with token but connection already exists")
  }

  return connection
}

/** Starts the SignalR connection if it's not already connected. */
export const startChatConnection = async (accessToken?: string): Promise<HubConnection> => {
  const conn = getChatConnection(accessToken)

  logToHandlers("info", `Attempting to start connection. Current state: ${conn.state}`)

  if (conn.state === HubConnectionState.Disconnected) {
    try {
      await conn.start()
      logToHandlers("info", `✅ SignalR connected to ChatHub. ID: ${conn.connectionId}`)
    } catch (err) {
      logToHandlers("error", `❌ SignalR connection failed: ${err}`)
      if (err instanceof Error) {
        logToHandlers("error", `Stack: ${err.stack}`)
      }
      throw err
    }
  } else {
    logToHandlers("info", `SignalR already connected or connecting. State: ${conn.state}`)
  }

  return conn
}

/** Stops the SignalR connection. */
export const stopChatConnection = async (): Promise<void> => {
  if (connection && connection.state !== HubConnectionState.Disconnected) {
    await connection.stop()
    logToHandlers("info", "❌ SignalR disconnected from ChatHub")
  }
}

// Chat Hub methods
export const joinChat = async (conversationId: string): Promise<void> => {
  if (!connection) throw new Error("SignalR not connected")
  await connection.invoke("JoinConversation", conversationId)
}

export const leaveChat = async (conversationId: string): Promise<void> => {
  if (!connection) throw new Error("SignalR not connected")
  await connection.invoke("LeaveConversation", conversationId)
}

export const sendMessage = async (conversationId: string, content: string): Promise<void> => {
  if (!connection) throw new Error("SignalR not connected")
  await connection.invoke("SendMessage", conversationId, content)
}

export const markAsRead = async (conversationId: string): Promise<void> => {
  if (!connection) throw new Error("SignalR not connected")
  await connection.invoke("MarkAsRead", conversationId)
}

export const sendTyping = async (conversationId: string, isTyping: boolean): Promise<void> => {
  if (!connection) throw new Error("SignalR not connected")
  await connection.invoke("Typing", conversationId, isTyping)
}

// Event listeners
/**
 * Registers a callback for receiving messages.
 * @param callback - The function to call when a message is received.
 */
export const onReceiveMessage = (callback: (message: ChatMessage) => void): void => {
  // C-04: Unified event name — both Hub and Service now use "ReceiveMessage"
  connection?.on("ReceiveMessage", callback)
}

/**
 * Registers a callback for when a message is read.
 * @param callback - The function to call when a message is read.
 */
export const onMessageRead = (callback: (userId: string, readAt: string) => void): void => {
  connection?.on("MessageRead", callback)
}

/**
 * Registers a callback for when a user is typing.
 * @param callback - The function to call when a user is typing.
 */
export const onUserTyping = (callback: (userId: string, isTyping: boolean) => void): void => {
  connection?.on("UserTyping", callback)
}

/**
 * Registers a callback for when a user joins.
 * @param callback - The function to call when a user joins.
 */
export const onUserJoined = (callback: (userId: string, username?: string) => void): void => {
  connection?.on("UserJoined", callback)
}

/**
 * Registers a callback for when a user leaves.
 * @param callback - The function to call when a user leaves.
 */
export const onUserLeft = (callback: (userId: string) => void): void => {
  connection?.on("UserLeft", callback)
}

export const onMessageEdited = (callback: (event: MessageEditedEvent) => void): void => {
  connection?.on("MessageEdited", callback)
}

export const onMessageDeleted = (callback: (messageId: string) => void): void => {
  connection?.on("MessageDeleted", callback)
}

export const onMessageReactionsUpdated = (
  callback: (event: MessageReactionsUpdatedEvent) => void
): void => {
  connection?.on("MessageReactionsUpdated", callback)
}

export const onMessagePinUpdated = (callback: (event: MessagePinUpdatedEvent) => void): void => {
  connection?.on("MessagePinUpdated", callback)
}

export const onAiStreamStarted = (callback: (event: AiStreamStartedEvent) => void): void => {
  connection?.on("AiStreamStarted", callback)
}

export const onAiStreamDelta = (callback: (event: AiStreamDeltaEvent) => void): void => {
  connection?.on("AiStreamDelta", callback)
}

export const onAiStreamCompleted = (callback: (event: AiStreamCompletedEvent) => void): void => {
  connection?.on("AiStreamCompleted", callback)
}

export const onAiStreamFailed = (callback: (event: AiStreamFailedEvent) => void): void => {
  connection?.on("AiStreamFailed", callback)
}

export const onAiToolCallsProposed = (callback: (event: AiToolCallsProposedEvent) => void): void => {
  connection?.on("AiToolCallsProposed", callback)
}

export const onAiStreamCancelled = (callback: (event: AiStreamCancelledEvent) => void): void => {
  connection?.on("AiStreamCancelled", callback)
}

export const onAiToolsExecuted = (callback: (event: AiToolsExecutedEvent) => void): void => {
  connection?.on("AiToolsExecuted", callback)
}

// C-13: All off* functions accept a specific callback to avoid removing ALL handlers
/**
 * Unregisters a specific receive message callback.
 */
export const offReceiveMessage = (callback: (message: ChatMessage) => void): void => {
  connection?.off("ReceiveMessage", callback)
}

/**
 * Unregisters a specific message read callback.
 */
export const offMessageRead = (callback: (userId: string, readAt: string) => void): void => {
  connection?.off("MessageRead", callback)
}

/**
 * Unregisters a specific user typing callback.
 */
export const offUserTyping = (callback: (userId: string, isTyping: boolean) => void): void => {
  connection?.off("UserTyping", callback)
}

/**
 * Unregisters a specific user joined callback.
 */
export const offUserJoined = (callback: (userId: string, username?: string) => void): void => {
  connection?.off("UserJoined", callback)
}

/**
 * Unregisters a specific user left callback.
 */
export const offUserLeft = (callback: (userId: string) => void): void => {
  connection?.off("UserLeft", callback)
}

export const offMessageEdited = (callback: (event: MessageEditedEvent) => void): void => {
  connection?.off("MessageEdited", callback)
}

export const offMessageDeleted = (callback: (messageId: string) => void): void => {
  connection?.off("MessageDeleted", callback)
}

export const offMessageReactionsUpdated = (
  callback: (event: MessageReactionsUpdatedEvent) => void
): void => {
  connection?.off("MessageReactionsUpdated", callback)
}

export const offMessagePinUpdated = (callback: (event: MessagePinUpdatedEvent) => void): void => {
  connection?.off("MessagePinUpdated", callback)
}

export const offAiStreamStarted = (callback: (event: AiStreamStartedEvent) => void): void => {
  connection?.off("AiStreamStarted", callback)
}

export const offAiStreamDelta = (callback: (event: AiStreamDeltaEvent) => void): void => {
  connection?.off("AiStreamDelta", callback)
}

export const offAiStreamCompleted = (callback: (event: AiStreamCompletedEvent) => void): void => {
  connection?.off("AiStreamCompleted", callback)
}

export const offAiStreamFailed = (callback: (event: AiStreamFailedEvent) => void): void => {
  connection?.off("AiStreamFailed", callback)
}

export const offAiToolCallsProposed = (callback: (event: AiToolCallsProposedEvent) => void): void => {
  connection?.off("AiToolCallsProposed", callback)
}

export const offAiStreamCancelled = (callback: (event: AiStreamCancelledEvent) => void): void => {
  connection?.off("AiStreamCancelled", callback)
}

export const offAiToolsExecuted = (callback: (event: AiToolsExecutedEvent) => void): void => {
  connection?.off("AiToolsExecuted", callback)
}
