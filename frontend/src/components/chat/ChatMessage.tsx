"use client"

import { useCallback, useState } from "react"
import {
  ChevronDown,
  MoreHorizontal,
  Pin,
  PinOff,
  Pencil,
  Reply,
  RotateCcw,
  Sparkles,
  Square,
  Trash2,
} from "lucide-react"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import { useLocale, useTranslations } from "next-intl"
import { Link } from "@/i18n/routing"
import { cn } from "@/lib/utils"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import type { ChatMessageProps } from "./types"

const QUICK_REACTIONS = ["👍", "❤️", "🔥", "🎉", "👀"]

export function ChatMessage({
  msg,
  isOwn,
  currentUserId,
  onReply,
  onEdit,
  onDelete,
  onToggleReaction,
  onTogglePin,
  canDeleteMessage = isOwn,
  canPinMessage = true,
  onCancelAi,
  onRegenerateAi,
}: Readonly<ChatMessageProps>) {
  const isInvitation = Boolean(msg.replyToId)
  const isAi = msg.isAiResponse
  const senderInitial = msg.senderFullName?.[0]?.toUpperCase() || "?"
  const t = useTranslations()
  const locale = useLocale()
  const dateLocale = getDateFnsLocale(locale)

  const time = formatDistanceToNow(new Date(msg.createdAt), {
    addSuffix: false,
    locale: dateLocale,
  })

  const handleReply = useCallback(() => onReply?.(msg), [msg, onReply])
  const handleEdit = useCallback(() => onEdit?.(msg), [msg, onEdit])
  const handleDelete = useCallback(() => onDelete?.(msg), [msg, onDelete])
  const handleTogglePin = useCallback(() => onTogglePin?.(msg), [msg, onTogglePin])
  const handleToggleReaction = useCallback(
    (emoji: string) => onToggleReaction?.(msg, emoji),
    [msg, onToggleReaction]
  )

  if (isAi) {
    return <AiMessage msg={msg} locale={locale} onCancelAi={onCancelAi} onRegenerateAi={onRegenerateAi} />
  }

  return (
    <div className={cn("flex items-start gap-[10px]", isOwn ? "flex-row-reverse" : "flex-row")}>
      <Avatar className="h-7 w-7 shrink-0 rounded-full">
        {msg.senderAvatarUrl && <AvatarImage src={msg.senderAvatarUrl} />}
        <AvatarFallback className="rounded-full bg-primary/10 font-mono text-[10px] font-semibold text-primary">
          {senderInitial}
        </AvatarFallback>
      </Avatar>

      <div className={cn("flex max-w-[70%] flex-col", isOwn ? "items-end" : "items-start")}>
        {!isOwn && (
          <Link
            href={`/dashboard/profile/${msg.senderId}`}
            className="ml-1 mb-[3px] font-mono text-[10px] text-muted-foreground hover:text-primary"
          >
            {msg.senderFullName || t("chat.defaultUser")} · {time}
          </Link>
        )}

        <div className="group/msg flex items-end gap-1">
          <div
            className={cn(
              "rounded-[14px] px-[13px] py-[9px] text-[13px] leading-[1.5]",
              msg.isPinned && "ring-1 ring-primary/35",
              isInvitation
                ? "border border-primary/40 bg-primary/5 text-foreground"
                : isOwn
                  ? "bg-primary text-primary-foreground"
                  : "bg-bg-subtle text-foreground"
              )}
          >
            {msg.isPinned && (
              <div className={cn("mb-1 flex items-center gap-1 text-[10px]", isOwn ? "text-primary-foreground/75" : "text-primary")}>
                <Pin className="h-3 w-3" />
                <span>Pinned</span>
              </div>
            )}
            {msg.replyTo && (
              <div
                className={cn(
                  "mb-2 max-w-[260px] rounded-[9px] px-2 py-1 text-[11px]",
                  isOwn ? "bg-primary-foreground/12 text-primary-foreground/75" : "bg-background/70 text-muted-foreground"
                )}
              >
                <div className="font-medium">{msg.replyTo.fullName ?? "Reply"}</div>
                <div className="truncate">{msg.replyTo.content}</div>
              </div>
            )}
            <p className="font-emoji whitespace-pre-wrap">{msg.content}</p>
            {isInvitation && <InvitationHint label={t("chat.invitationReplyHint")} />}
            {msg.isEdited && (
              <span className={cn("ml-2 text-[10px]", isOwn ? "text-primary-foreground/70" : "text-muted-foreground")}>
                {t("chat.edited")}
              </span>
            )}
          </div>
          <MessageActions
            isOwn={isOwn}
            isPinned={msg.isPinned === true}
            onReply={handleReply}
            onEdit={handleEdit}
            onDelete={canDeleteMessage ? handleDelete : undefined}
            onTogglePin={canPinMessage ? handleTogglePin : undefined}
            onToggleReaction={onToggleReaction ? handleToggleReaction : undefined}
          />
          {!isOwn && (
            <div className="opacity-0 transition-opacity group-hover/msg:opacity-100">
              <ReportDialog targetType={REPORT_TARGET_TYPES.MESSAGE} targetId={msg.id} />
            </div>
          )}
        </div>
        <ReactionRow
          reactions={msg.reactions ?? []}
          currentUserId={currentUserId}
          onToggleReaction={onToggleReaction ? handleToggleReaction : undefined}
        />
      </div>
    </div>
  )
}

interface MessageActionsProps {
  readonly isOwn: boolean
  readonly isPinned: boolean
  readonly onReply: () => void
  readonly onEdit: () => void
  readonly onDelete?: () => void
  readonly onTogglePin?: () => void
  readonly onToggleReaction?: (emoji: string) => void
}

function MessageActions({
  isOwn,
  isPinned,
  onReply,
  onEdit,
  onDelete,
  onTogglePin,
  onToggleReaction,
}: MessageActionsProps) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-7 w-7 rounded-[7px] opacity-0 transition-opacity group-hover/msg:opacity-100 data-[state=open]:opacity-100"
          aria-label="Message actions"
        >
          <MoreHorizontal className="h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align={isOwn ? "end" : "start"} className="w-44 rounded-[10px]">
        <DropdownMenuItem onClick={onReply} className="gap-2 text-[12px]">
          <Reply className="h-3.5 w-3.5" />
          Reply
        </DropdownMenuItem>
        {onTogglePin && (
          <DropdownMenuItem onClick={onTogglePin} className="gap-2 text-[12px]">
            {isPinned ? <PinOff className="h-3.5 w-3.5" /> : <Pin className="h-3.5 w-3.5" />}
            {isPinned ? "Unpin" : "Pin"}
          </DropdownMenuItem>
        )}
        <DropdownMenuSeparator />
        <div className="grid grid-cols-5 gap-1 px-2 py-1">
          {QUICK_REACTIONS.map((emoji) => (
            <ReactionMenuButton key={emoji} emoji={emoji} onToggleReaction={onToggleReaction} />
          ))}
        </div>
        {(isOwn || onDelete) && (
          <>
            <DropdownMenuSeparator />
            {isOwn && (
              <DropdownMenuItem onClick={onEdit} className="gap-2 text-[12px]">
                <Pencil className="h-3.5 w-3.5" />
                Edit
              </DropdownMenuItem>
            )}
            {onDelete && (
              <DropdownMenuItem onClick={onDelete} className="gap-2 text-[12px] text-destructive focus:text-destructive">
                <Trash2 className="h-3.5 w-3.5" />
                Delete
              </DropdownMenuItem>
            )}
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function ReactionMenuButton({
  emoji,
  onToggleReaction,
}: {
  readonly emoji: string
  readonly onToggleReaction?: (emoji: string) => void
}) {
  const handleClick = useCallback(() => onToggleReaction?.(emoji), [emoji, onToggleReaction])
  return (
    <button
      type="button"
      onClick={handleClick}
      className="font-emoji flex h-7 items-center justify-center rounded-[7px] text-[14px] hover:bg-bg-subtle"
      aria-label={`React ${emoji}`}
    >
      {emoji}
    </button>
  )
}

function ReactionRow({
  reactions,
  currentUserId,
  onToggleReaction,
}: {
  readonly reactions: NonNullable<ChatMessageProps["msg"]["reactions"]>
  readonly currentUserId?: string
  readonly onToggleReaction?: (emoji: string) => void
}) {
  if (reactions.length === 0) return null

  return (
    <div className="mt-1 flex flex-wrap gap-1">
      {reactions.map((reaction) => (
        <ReactionPill
          key={reaction.emoji}
          reaction={reaction}
          currentUserId={currentUserId}
          onToggleReaction={onToggleReaction}
        />
      ))}
    </div>
  )
}

function ReactionPill({
  reaction,
  currentUserId,
  onToggleReaction,
}: {
  readonly reaction: NonNullable<ChatMessageProps["msg"]["reactions"]>[number]
  readonly currentUserId?: string
  readonly onToggleReaction?: (emoji: string) => void
}) {
  const active = Boolean(currentUserId && reaction.userIds.includes(currentUserId))
  const handleClick = useCallback(() => onToggleReaction?.(reaction.emoji), [reaction.emoji, onToggleReaction])

  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        "inline-flex h-6 items-center gap-1 rounded-full border px-2 text-[11px]",
        active
          ? "border-primary/40 bg-primary/10 text-primary"
          : "border-border bg-bg-subtle text-muted-foreground hover:text-foreground"
      )}
    >
      <span className="font-emoji">{reaction.emoji}</span>
      <span>{reaction.count}</span>
    </button>
  )
}

function AiMessage({
  msg,
  locale,
  onCancelAi,
  onRegenerateAi,
}: {
  readonly msg: ChatMessageProps["msg"]
  readonly locale: string
  readonly onCancelAi?: (message: ChatMessageProps["msg"]) => void
  readonly onRegenerateAi?: (message: ChatMessageProps["msg"]) => void
}) {
  const dateLocale = getDateFnsLocale(locale)
  const t = useTranslations("ai")
  const isStreaming = Boolean(msg.aiStreaming)
  const isCancelled = Boolean(msg.aiCancelled)
  const isFailed = Boolean(msg.aiFailed)
  const handleCancel = useCallback(() => onCancelAi?.(msg), [msg, onCancelAi])
  const handleRegenerate = useCallback(() => onRegenerateAi?.(msg), [msg, onRegenerateAi])

  // Once the stream completes, the regenerate affordance only makes sense
  // when the host wired one — gives surfaces (e.g. the floating widget)
  // a way to opt out without dead UI.
  const showRegenerate = !isStreaming && !isCancelled && !isFailed && Boolean(onRegenerateAi)
  const showCancel = isStreaming && Boolean(onCancelAi)

  return (
    <div className="flex items-start gap-[10px]">
      <div
        className={cn(
          "flex h-7 w-7 shrink-0 items-center justify-center rounded-full border border-primary/20 bg-primary/10",
          isStreaming && "animate-pulse",
        )}
      >
        <Sparkles className="h-3.5 w-3.5 text-primary" />
      </div>
      <div className="flex max-w-[85%] flex-col">
        <div className="mx-1 mb-[3px] flex items-center gap-1.5 font-mono text-[10px] text-primary">
          <span>AI Assistant</span>
          {isStreaming && (
            <span className="inline-flex items-center gap-1 text-primary/80">
              <span className="pulse-dot inline-block h-1 w-1 rounded-full bg-primary" />
              {t("streaming")}
            </span>
          )}
          {isCancelled && (
            <span className="rounded-full bg-amber-500/15 px-1.5 py-0 text-[10px] font-normal text-amber-700 dark:text-amber-300">
              {t("cancelled")}
            </span>
          )}
          {isFailed && (
            <span className="rounded-full bg-destructive/15 px-1.5 py-0 text-[10px] font-normal text-destructive">
              {t("failed")}
            </span>
          )}
        </div>
        <div className="rounded-[14px] border border-primary/15 bg-primary/5 px-[13px] py-[9px] text-[13px] leading-relaxed">
          <div className="prose prose-sm dark:prose-invert font-emoji max-w-none prose-p:my-1 prose-ul:my-1 prose-li:my-0.5">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>
              {msg.content || (isStreaming ? "" : msg.content)}
            </ReactMarkdown>
            {isStreaming && (
              // Animated cursor at the trailing edge of the content. Inline
              // span so it sits flush with the last token rather than
              // wrapping to a new line.
              <span
                aria-hidden
                className="ml-[1px] inline-block h-[1em] w-[2px] -translate-y-[1px] animate-pulse bg-primary align-middle"
              />
            )}
          </div>
          <AiMessageMeta msg={msg} t={t} />
          <AiMessageDetails metadata={msg.aiMetadata ?? null} t={t} />
          <div className="mt-1 flex items-center justify-between gap-2 font-mono text-[10px] text-primary/70">
            <div className="flex items-center gap-1">
              {showCancel && (
                <button
                  type="button"
                  onClick={handleCancel}
                  className="inline-flex items-center gap-1 rounded-[6px] border border-primary/20 bg-background/40 px-1.5 py-0.5 text-primary transition-colors hover:bg-background"
                  aria-label={t("cancel")}
                >
                  <Square className="h-2.5 w-2.5" fill="currentColor" />
                  {t("cancel")}
                </button>
              )}
              {showRegenerate && (
                <button
                  type="button"
                  onClick={handleRegenerate}
                  className="inline-flex items-center gap-1 rounded-[6px] border border-transparent px-1.5 py-0.5 text-primary/70 transition-colors hover:border-primary/20 hover:bg-background/40 hover:text-primary"
                  aria-label={t("regenerate")}
                >
                  <RotateCcw className="h-2.5 w-2.5" />
                  {t("regenerate")}
                </button>
              )}
            </div>
            <span>
              {formatDistanceToNow(new Date(msg.createdAt), { addSuffix: false, locale: dateLocale })}
            </span>
          </div>
        </div>
      </div>
    </div>
  )
}

function AiMessageMeta({
  msg,
  t,
}: {
  readonly msg: ChatMessageProps["msg"]
  readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
  const totalTokens = msg.aiUsage?.totalTokens
  const cost = msg.aiUsage?.estimatedCostUsd
  const toolCount = msg.aiToolCalls?.length ?? 0

  const hasCost = typeof cost === "number"
  if (!totalTokens && !hasCost && toolCount === 0) return null

  const costText = hasCost ? `$${cost.toFixed(cost < 0.01 ? 6 : 4)}` : "n/a"

  return (
    <div className="mt-2 flex flex-wrap items-center gap-1.5 border-t border-primary/10 pt-2 font-mono text-[10px] text-primary/75">
      {totalTokens ? (
        <span className="rounded-full bg-primary/10 px-2 py-0.5">
          {t("usageTokens", { tokens: totalTokens })}
        </span>
      ) : null}
      {hasCost ? (
        <span className="rounded-full bg-primary/10 px-2 py-0.5">
          {t("usageCost", { cost: costText })}
        </span>
      ) : null}
      {toolCount > 0 ? (
        <span className="rounded-full bg-primary/10 px-2 py-0.5">
          {t("toolCallsPending", { count: toolCount })}
        </span>
      ) : null}
    </div>
  )
}

function AiMessageDetails({
  metadata,
  t,
}: {
  readonly metadata: ChatMessageProps["msg"]["aiMetadata"] | null
  readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
  const [open, setOpen] = useState(false)
  const handleToggle = useCallback(() => {
    setOpen((value) => !value)
  }, [])

  if (!metadata) return null

  const toolCalls = metadata.details?.toolCalls ?? []
  const retentionDays = metadata.detailsRetentionDays ?? 14

  return (
    <div className="mt-2 border-t border-primary/10 pt-2">
      <button
        type="button"
        onClick={handleToggle}
        className="inline-flex items-center gap-1 rounded-[6px] px-1 py-0.5 font-mono text-[10px] text-primary/75 transition-colors hover:bg-background/50 hover:text-primary"
      >
        <ChevronDown className={cn("h-3 w-3 transition-transform", open && "rotate-180")} />
        {t("details")}
      </button>

      {open && (
        <div className="mt-2 space-y-2 rounded-[10px] border border-primary/15 bg-background/55 p-2 font-mono text-[10px] text-foreground/80">
          <AiDetailSummary metadata={metadata} t={t} />

          {metadata.detailsAvailable === false && (
            <p className="text-muted-foreground">
              {t("detailsPruned", { days: retentionDays })}
            </p>
          )}

          <AiToolDetails toolCalls={toolCalls} t={t} />
        </div>
      )}
    </div>
  )
}

function AiDetailSummary({
  metadata,
  t,
}: {
  readonly metadata: NonNullable<ChatMessageProps["msg"]["aiMetadata"]>
  readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
  const providerModel = [metadata.provider, metadata.modelId].filter(Boolean).join(" / ")
  const promptTokens = metadata.tokens?.promptTokens ?? "n/a"
  const completionTokens = metadata.tokens?.completionTokens ?? "n/a"
  const totalTokens = metadata.tokens?.totalTokens ?? "n/a"
  const cost = formatAiCostLabel(metadata, t)
  const latency = typeof metadata.latencyMs === "number" ? `${metadata.latencyMs}ms` : "n/a"

  return (
    <div className="grid gap-1 sm:grid-cols-2">
      <AiDetailRow label={t("providerModel")} value={providerModel || "n/a"} />
      <AiDetailRow label={t("latency")} value={latency} />
      <AiDetailRow label={t("tokensBreakdown")} value={`P ${promptTokens} / C ${completionTokens} / T ${totalTokens}`} />
      <AiDetailRow label={t("cost")} value={cost} />
    </div>
  )
}

function AiDetailRow({ label, value }: { readonly label: string; readonly value: string | number }) {
  return (
    <div className="min-w-0 rounded-[7px] bg-primary/5 px-2 py-1">
      <div className="text-primary/65">{label}</div>
      <div className="truncate text-foreground">{value}</div>
    </div>
  )
}

function AiToolDetails({
  toolCalls,
  t,
}: {
  readonly toolCalls: NonNullable<NonNullable<ChatMessageProps["msg"]["aiMetadata"]>["details"]>["toolCalls"]
  readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
  if (!toolCalls || toolCalls.length === 0) return null

  return (
    <div className="space-y-1">
      <div className="text-primary/70">{t("tools")}</div>
      {toolCalls.map((tool, index) => (
        <AiToolDetailItem key={tool.id ?? `${tool.name}-${index}`} tool={tool} t={t} />
      ))}
    </div>
  )
}

function AiToolDetailItem({
  tool,
  t,
}: {
  readonly tool: NonNullable<NonNullable<NonNullable<ChatMessageProps["msg"]["aiMetadata"]>["details"]>["toolCalls"]>[number]
  readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
  const status = tool.success === true ? t("toolSucceeded") : tool.success === false ? t("toolFailed") : t("toolPending")

  return (
    <div className="rounded-[8px] border border-border/70 bg-background/70 p-2">
      <div className="mb-1 flex items-center justify-between gap-2">
        <span className="truncate text-primary">{tool.name}</span>
        <span className="shrink-0 text-muted-foreground">{status}</span>
      </div>
      {tool.summary && <div className="mt-1 text-muted-foreground">{tool.summary}</div>}
      {tool.error && <div className="mt-1 text-destructive">{tool.error}</div>}
    </div>
  )
}

function formatAiCost(value: number): string {
  return value.toFixed(value < 0.01 ? 6 : 4)
}

function formatAiCostLabel(
  metadata: NonNullable<ChatMessageProps["msg"]["aiMetadata"]>,
  t: (key: string, values?: Record<string, string | number>) => string
): string {
  if (typeof metadata.costUsd === "number") return `$${formatAiCost(metadata.costUsd)}`
  return t("costFreeOrUnknown")
}

function InvitationHint({ label }: { readonly label: string }) {
  return (
    <div className="mt-2 text-[11px]">
      <Link href="/dashboard/invitations" className="text-primary hover:underline">
        {label}
      </Link>
    </div>
  )
}
