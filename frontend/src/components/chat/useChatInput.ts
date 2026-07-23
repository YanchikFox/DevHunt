"use client"

import { useState, useCallback, useRef, useEffect } from "react"

interface UseChatInputOptions {
  readonly setTyping: (typing: boolean) => void
}

interface UseChatInputResult {
  readonly message: string
  readonly setMessage: (value: string) => void
  readonly handleTyping: (value: string) => void
  readonly isAiCommand: boolean
}

/**
 * Hook for managing chat input state and typing indicator.
 */
export function useChatInput({ setTyping }: UseChatInputOptions): UseChatInputResult {
  const [message, setMessage] = useState("")
  const typingTimeoutRef = useRef<NodeJS.Timeout | null>(null)

  useEffect(() => {
    return () => {
      if (typingTimeoutRef.current) {
        clearTimeout(typingTimeoutRef.current)
      }
    }
  }, [])

  const handleTyping = useCallback((value: string) => {
    setMessage(value)

    if (typingTimeoutRef.current) {
      clearTimeout(typingTimeoutRef.current)
    }

    if (value.length > 0) {
      setTyping(true)
      typingTimeoutRef.current = setTimeout(() => {
        setTyping(false)
      }, 3000)
    } else {
      setTyping(false)
    }
  }, [setTyping])

  const isAiCommand = message.toLowerCase().startsWith("/ai ")

  return {
    message,
    setMessage,
    handleTyping,
    isAiCommand,
  }
}
