"use client"

import { useCallback } from "react"
import { MessageCircle } from "lucide-react"
import { Button } from "@/components/ui/button"
import { useProfile } from "@/lib/api/queries/profile"
import { useChatStore } from "@/lib/store/chatStore"
import { useConversations } from "@/lib/api/queries/chat"
import { useTranslations } from "next-intl"
import { useToast } from "@/hooks/use-toast"

/**
 * Props for the SendMessageButton component.
 */
export interface SendMessageButtonProps {
  /** ID of the user to send a message to */
  recipientId: string
  /** Name of the recipient */
  recipientName: string
  /** Avatar URL of the recipient */
  recipientAvatar?: string | null
  /** Button variant style */
  variant?: "default" | "outline" | "ghost"
  /** Button size */
  size?: "default" | "sm" | "lg" | "icon"
}

/**
 * A button that initiates a direct message conversation with a user.
 * If a conversation already exists, it opens it. Otherwise, it starts a new one.
 *
 * @example
 * ```tsx
 * <SendMessageButton
 *   recipientId="user-123"
 *   recipientName="John Doe"
 * />
 * ```
 */
export function SendMessageButton({
  recipientId,
  recipientName,
  recipientAvatar,
  variant = "default",
  size = "default",
}: SendMessageButtonProps) {
  const { data: profile, isLoading: profileLoading } = useProfile()
  const { openNewChat, openConversation } = useChatStore()
  const { data: conversations } = useConversations()
  const t = useTranslations("chat")
  const { toast } = useToast()

  const handleClick = useCallback(() => {
    if (!profile?.id) {
      toast({
        title: t("loginRequired"),
        variant: "destructive"
      })
      return
    }

    const isDirectConversation = (type: string | number | null | undefined) => {
      const typeValue = String(type ?? "").toLowerCase()
      return typeValue === "direct" || typeValue === "0"
    }

    // Check if conversation already exists
    const existingConv = conversations?.find(c =>
      isDirectConversation(c.type) && c.participants.some(p => p.userId === recipientId)
    )

    if (existingConv) {
      openConversation(existingConv.id)
    } else {
      openNewChat({
        id: recipientId,
        name: recipientName,
        avatarUrl: recipientAvatar
      })
    }
  }, [profile?.id, conversations, recipientId, recipientName, recipientAvatar, openConversation, openNewChat, t, toast])

  return (
    <Button
      variant={variant}
      size={size}
      disabled={profileLoading}
      onClick={handleClick}
    >
      <MessageCircle className="mr-2 h-4 w-4" />
      {t("sendMessage")}
    </Button>
  )
}
