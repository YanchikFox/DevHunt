"use client"

import { useMemo } from "react"
import { useParticipants, type ConversationParticipant, type Conversation } from "@/lib/api/queries/chat"
import { useTranslations } from "next-intl"
import type { MergedParticipant, RecipientUser } from "./types"

const ONLINE_THRESHOLD_MINUTES = 10
const MINUTES_PER_DAY = 1440
const MS_PER_DAY = 1000 * 60 * 60 * 24

interface UseChatParticipantsOptions {
  readonly conversationId: string | null
  readonly conversation?: Conversation | null
  readonly participants?: ConversationParticipant[]
  readonly currentUserId?: string
  readonly isNewChat: boolean
  readonly recipientUser?: RecipientUser | null
}

interface UseChatParticipantsResult {
  readonly mergedParticipants: MergedParticipant[]
  readonly otherParticipant?: MergedParticipant
  readonly isGroupChat: boolean
  readonly groupOnlineCount: number
  readonly otherOnline: boolean
  readonly statusText: string
  readonly conversationTitle: string
}

type TranslateFn = ReturnType<typeof useTranslations>

function mergeParticipantDetails(
  participantList: ConversationParticipant[],
  details: ConversationParticipant[]
): MergedParticipant[] {
  if (!details.length) return participantList as MergedParticipant[]
  const map = new Map(details.map(p => [p.userId, p]))
  return participantList.map(p => ({ ...p, ...map.get(p.userId) })) as MergedParticipant[]
}

function isParticipantOnline(participant: MergedParticipant): boolean {
  const last = participant.lastReadAt ?? participant.lastLogin
  if (!last) return false
  const diffMins = (Date.now() - new Date(last).getTime()) / 60000
  return diffMins >= 0 && diffMins <= ONLINE_THRESHOLD_MINUTES
}

function countOnlineParticipants(participants: MergedParticipant[]): number {
  return participants.filter(isParticipantOnline).length
}

function findOtherParticipant(
  participants: MergedParticipant[],
  currentUserId?: string
): MergedParticipant | undefined {
  return participants.find(p => p.userId !== currentUserId)
}

function getLastSeenTimestamp(
  otherParticipant: MergedParticipant | undefined,
  conversation: Conversation | null | undefined
): string | null {
  return otherParticipant?.lastLogin
    ?? otherParticipant?.lastReadAt
    ?? conversation?.lastMessageAt
    ?? null
}

function getLastSeenTranslationKey(daysSinceLastSeen: number): string {
  if (daysSinceLastSeen < 2) return "chat.lastSeenYesterday"
  if (daysSinceLastSeen <= 7) return "chat.lastSeenWeekAgo"
  if (daysSinceLastSeen <= 30) return "chat.lastSeenMonthAgo"
  return "chat.lastSeenLongAgo"
}

function formatLastSeen(value: string | null, t: TranslateFn): string {
  if (!value) return t("chat.statusUnavailable")

  const lastDate = new Date(value)
  if (Number.isNaN(lastDate.getTime())) return t("chat.statusUnavailable")

  const diffMs = Date.now() - lastDate.getTime()
  const minutes = diffMs / 60000
  const days = diffMs / MS_PER_DAY

  const timeOptions: Intl.DateTimeFormatOptions = { hour: "2-digit", minute: "2-digit" }
  const timeStr = lastDate.toLocaleTimeString([], timeOptions)

  if (minutes < MINUTES_PER_DAY) {
    return t("chat.lastSeenAt", { time: timeStr })
  }

  if (days < 2) {
    return t("chat.lastSeenYesterday", { time: timeStr })
  }

  return t(getLastSeenTranslationKey(days))
}

function getConversationTitle(
  isNewChat: boolean,
  recipientUser: RecipientUser | null | undefined,
  conversation: Conversation | null | undefined,
  mergedParticipants: MergedParticipant[],
  currentUserId: string | undefined,
  t: TranslateFn
): string {
  if (isNewChat && recipientUser) return recipientUser.name
  if (conversation?.title) return conversation.title
  const other = findOtherParticipant(mergedParticipants, currentUserId)
  return other?.fullName || t("chat.title")
}

function getStatusText(
  isGroupChat: boolean,
  lastSeenText: string,
  participantCount: number,
  groupOnlineCount: number,
  t: TranslateFn
): string {
  if (!isGroupChat) return lastSeenText
  if (!participantCount) return t("chat.groupChatFallback")

  const onlineText = t("chat.onlineCount", { count: groupOnlineCount })
  const totalText = t("chat.memberCount", { count: participantCount })
  return `${totalText}, ${onlineText}`
}

function isGroupConversation(
  conversationType: string | undefined,
  participantCount: number
): boolean {
  const type = (conversationType ?? "").toLowerCase()
  return type === "group" || participantCount > 2
}

function useParticipantData(
  participantList: ConversationParticipant[],
  conversationId: string | null,
  isNewChat: boolean,
  currentUserId?: string
) {
  const { data: participantsDetails = [] } = useParticipants(isNewChat ? null : conversationId)

  const mergedParticipants = useMemo(
    () => mergeParticipantDetails(participantList, participantsDetails),
    [participantList, participantsDetails]
  )

  const otherParticipant = useMemo(
    () => findOtherParticipant(mergedParticipants, currentUserId),
    [mergedParticipants, currentUserId]
  )

  const groupOnlineCount = useMemo(
    () => countOnlineParticipants(mergedParticipants),
    [mergedParticipants]
  )

  const otherOnline = useMemo(
    () => otherParticipant ? isParticipantOnline(otherParticipant) : false,
    [otherParticipant]
  )

  return { mergedParticipants, otherParticipant, groupOnlineCount, otherOnline }
}

/**
 * Hook for managing chat participants and their online status.
 */
export function useChatParticipants({
  conversationId,
  conversation,
  participants,
  currentUserId,
  isNewChat,
  recipientUser,
}: UseChatParticipantsOptions): UseChatParticipantsResult {
  const t = useTranslations()
  const participantList = conversation?.participants ?? participants ?? []

  const { mergedParticipants, otherParticipant, groupOnlineCount, otherOnline } = useParticipantData(
    participantList,
    conversationId,
    isNewChat,
    currentUserId
  )

  const isGroupChat = useMemo(
    () => isGroupConversation(conversation?.type, mergedParticipants.length),
    [conversation?.type, mergedParticipants.length]
  )

  const lastSeenText = useMemo(() => {
    if (isNewChat) return t("chat.newDialogue")
    return formatLastSeen(getLastSeenTimestamp(otherParticipant, conversation), t)
  }, [isNewChat, otherParticipant, conversation, t])

  const statusText = useMemo(
    () => getStatusText(isGroupChat, lastSeenText, mergedParticipants.length, groupOnlineCount, t),
    [isGroupChat, lastSeenText, mergedParticipants.length, groupOnlineCount, t]
  )

  const conversationTitle = useMemo(
    () => getConversationTitle(isNewChat, recipientUser, conversation, mergedParticipants, currentUserId, t),
    [isNewChat, recipientUser, conversation, mergedParticipants, currentUserId, t]
  )

  return {
    mergedParticipants,
    otherParticipant,
    isGroupChat,
    groupOnlineCount,
    otherOnline,
    statusText,
    conversationTitle,
  }
}
