/**
 * Real-time notification client interface
 * Supports both fake WebSocket (for development) and SignalR (for production)
 */

import type { Notification } from "@/lib/api/schema"
import { USE_MOCKS, WS_URL } from "@/lib/feature-flags"
import { HubConnection, HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr"

/**
 * Interface for a notification client.
 * Defines methods for connecting, disconnecting, and listening for messages and errors.
 */
export interface NotificationClient {
  /**
   * Connects to the notification service.
   * @param userId - The ID of the user connecting.
   * @param token - The authentication token.
   */
  connect(userId: string, token: string): Promise<void>
  /**
   * Disconnects from the notification service.
   */
  disconnect(): void
  /**
   * Registers a callback to be called when a notification is received.
   * @param callback - The function to call with the received notification.
   */
  onMessage(callback: (notification: Notification) => void): void
  /**
   * Registers a callback to be called when an error occurs.
   * @param callback - The function to call with the error.
   */
  onError(callback: (error: Error) => void): void
}

/**
 * Fake WebSocket implementation for development.
 * Simulates incoming notifications periodically.
 */
export class FakeNotificationClient implements NotificationClient {
  private messageCallbacks: Array<(notification: Notification) => void> = []
  private errorCallbacks: Array<(error: Error) => void> = []
  private intervalId: NodeJS.Timeout | null = null
  private userId: string | null = null

  async connect(userId: string, _token: string): Promise<void> {
    this.userId = userId

    // Simulate incoming notifications every 30 seconds
    this.intervalId = setInterval(() => {
      const mockNotification: Notification = {
        id: `fake-${Date.now()}`,
        userId,
        type: "invitation",
        title: "New invitation",
        message: "You have been invited to join a project",
        read: false,
        createdAt: new Date().toISOString(),
      }

      this.messageCallbacks.forEach((callback) => callback(mockNotification))
    }, 30000)
  }

  disconnect(): void {
    if (this.intervalId) {
      clearInterval(this.intervalId)
      this.intervalId = null
    }
    this.userId = null
  }

  onMessage(callback: (notification: Notification) => void): void {
    this.messageCallbacks.push(callback)
  }

  onError(callback: (error: Error) => void): void {
    this.errorCallbacks.push(callback)
  }
}

/**
 * SignalR implementation for real-time notifications.
 * Connects to the backend /notificationHub.
 */
export class SignalRNotificationClient implements NotificationClient {
  private connection: HubConnection | null = null
  private messageCallbacks: Array<(notification: Notification) => void> = []
  private errorCallbacks: Array<(error: Error) => void> = []
  private hubUrl: string

  constructor(hubUrl: string) {
    this.hubUrl = hubUrl.replace(/^ws:\/\//, "http://").replace(/^wss:\/\//, "https://")
  }

  async connect(userId: string, token: string): Promise<void> {
    // Use hubUrl (from WS_URL feature flag) — empty = relative path through nginx
    const url = `${this.hubUrl}/notificationHub`

    if (process.env.NODE_ENV !== "production") {
      console.debug("[SignalR] connecting to", url)
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(url, {
        accessTokenFactory: () => token,
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s, then 30s intervals
          if (retryContext.previousRetryCount === 0) return 0
          if (retryContext.previousRetryCount === 1) return 2000
          if (retryContext.previousRetryCount === 2) return 10000
          return 30000
        },
      })
      .configureLogging(LogLevel.None)
      .build()

    this.connection.onclose((error) => {
      if (error) {
        this.errorCallbacks.forEach((callback) =>
          callback(new Error(`SignalR connection closed: ${error}`))
        )
      }
    })

    this.connection.onreconnecting(() => {
      // silent – hub is reconnecting after a drop
    })

    this.connection.onreconnected(() => {
      if (process.env.NODE_ENV === "development") console.debug("[SignalR] NotificationHub reconnected")
    })

    // Listen for notifications from server
    // Backend sends via: hubContext.Clients.Group($"user:{userId}").SendAsync("Notification", ...)
    this.connection.on("Notification", (notification: Notification) => {
      this.messageCallbacks.forEach((callback) => callback(notification))
    })

    try {
      await this.connection.start()

      // Note: Backend automatically adds user to group "user:{userId}" in OnConnectedAsync
      // No need to call JoinUserGroup manually
    } catch (error) {
      const err = error instanceof Error ? error : new Error("Failed to connect to SignalR")
      this.errorCallbacks.forEach((callback) => callback(err))
      throw err
    }
  }

  disconnect(): void {
    if (this.connection) {
      this.connection.stop()
      this.connection = null
    }
  }

  onMessage(callback: (notification: Notification) => void): void {
    this.messageCallbacks.push(callback)
  }

  onError(callback: (error: Error) => void): void {
    this.errorCallbacks.push(callback)
  }
}

/**
 * Factory function to create appropriate client based on feature flags.
 * @returns A NotificationClient instance (Fake or SignalR).
 */
export function createNotificationClient(): NotificationClient {
  if (USE_MOCKS) {
    return new FakeNotificationClient()
  }

  // Use SignalR for real API
  return new SignalRNotificationClient(WS_URL)
}
