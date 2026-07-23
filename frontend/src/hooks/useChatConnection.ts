"use client"

import { useCallback, useEffect, useState } from "react"
import { useSession } from "next-auth/react"
import { startChatConnection, joinChat, leaveChat } from "@/lib/signalr"
import { HubConnectionState } from "@microsoft/signalr"

export function useChatConnection(conversationId: string | null) {
  const { data: session, status } = useSession()
  const [connected, setConnected] = useState(false)
  const [connectionError, setConnectionError] = useState<string | null>(null)

  useEffect(() => {
    let isActive = true

    const connect = async () => {
      if (status === "loading" || !session?.user?.id) return
      setConnectionError(null)
      try {
        const { fetchRealtimeAccessToken } = await import("@/lib/auth/realtime-token")
        const accessToken = await fetchRealtimeAccessToken()
        if (!accessToken || !isActive) return

        const conn = await startChatConnection(accessToken)
        if (!isActive) return

        setConnected(conn.state === HubConnectionState.Connected)
        setConnectionError(null)

        conn.onreconnected(() => {
          if (!isActive) return
          setConnected(true)
          setConnectionError(null)
        })
        conn.onclose((error) => {
          if (!isActive) return
          setConnected(false)
          setConnectionError(error instanceof Error ? error.message : error ? String(error) : "SignalR connection closed")
        })
      } catch (error) {
        if (!isActive) return
        setConnected(false)
        setConnectionError(error instanceof Error ? error.message : "SignalR connection failed")
      }
    }

    connect()
    return () => { isActive = false }
  }, [session?.user?.id, status])

  const joinLeave = useCallback(() => {
    if (!connected || !conversationId) return undefined
    const join = async () => {
      try { await joinChat(conversationId) }
      catch (error) { console.error("Failed to join chat:", error) }
    }
    join()
    return () => {
      if (conversationId) leaveChat(conversationId).catch(() => {})
    }
  }, [connected, conversationId])

  useEffect(() => joinLeave() ?? undefined, [joinLeave])

  return { connected, connectionError }
}
