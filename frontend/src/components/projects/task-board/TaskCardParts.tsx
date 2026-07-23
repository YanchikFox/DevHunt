import Image from "next/image"
import React, { useCallback } from "react"
import type { useTranslations } from "next-intl"
import { Calendar, Paperclip, User } from "lucide-react"
import { cn } from "@/lib/utils"
import type { Task } from "./types"
import { formatTaskDate, getGitHubIssueNumber, getGitHubIssueUrl } from "./utils"

/** Custom GitHub icon SVG (replaces deprecated lucide-react Github icon) */
function GitHubIcon({ className }: { readonly className?: string }) {
    return (
        <svg
            viewBox="0 0 24 24"
            fill="currentColor"
            className={className}
            aria-hidden="true"
        >
            <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z" />
        </svg>
    )
}

/** Drag handle dots indicator */
export function DragHandleDots() {
    return (
        <span className="flex flex-col gap-0.5">
            <span className="w-1 h-1 rounded-full bg-muted-foreground/40 block" />
            <span className="w-1 h-1 rounded-full bg-muted-foreground/40 block" />
        </span>
    )
}

/** GitHub issue link badge */
export function GitHubIssueBadge({
    url,
    issueNumber,
    size = "xs",
}: {
    readonly url: string
    readonly issueNumber?: number
    readonly size?: "xs" | "sm"
}) {
    const sizeClasses = size === "xs" ? "text-xs" : "text-[11px]"
    const handleClick = useCallback((e: React.MouseEvent) => e.stopPropagation(), [])

    return (
        <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            onClick={handleClick}
            className={cn(
                sizeClasses,
                "flex items-center gap-1 px-1.5 py-0.5 rounded-md border border-primary/20 bg-primary/10 text-primary hover:bg-primary/15 transition-colors"
            )}
            title={`GitHub Issue #${issueNumber || ''}`}
        >
            <GitHubIcon className="h-3 w-3" />
            {issueNumber && `#${issueNumber}`}
        </a>
    )
}

/** Single metadata indicator item */
function MetadataIndicator({
    icon: Icon,
    children,
    className,
}: {
    readonly icon: React.ComponentType<{ className?: string }>
    readonly children: React.ReactNode
    readonly className?: string
}) {
    return (
        <span className={cn("text-xs text-muted-foreground flex items-center gap-1", className)}>
            <Icon className="h-3 w-3" />
            {children}
        </span>
    )
}

/** Task metadata indicators (attachments, comments, assignee) */
export function TaskIndicators({
    task,
    dueDate,
    isOverdue,
    showGitHub = true,
}: {
    readonly task: Task
    readonly dueDate?: string
    readonly isOverdue?: boolean
    readonly showGitHub?: boolean
}) {
    const attachmentsCount = task.attachmentsCount || 0
    const gitHubIssueUrl = getGitHubIssueUrl(task)
    const gitHubIssueNumber = getGitHubIssueNumber(task)

    return (
        <>
            {attachmentsCount > 0 && (
                <MetadataIndicator icon={Paperclip}>{attachmentsCount}</MetadataIndicator>
            )}
            {task.assigneeName && (
                <MetadataIndicator icon={User}>
                    <span className="max-w-[60px] truncate">{task.assigneeName}</span>
                </MetadataIndicator>
            )}
            {showGitHub && gitHubIssueUrl && (
                <GitHubIssueBadge url={gitHubIssueUrl} issueNumber={gitHubIssueNumber} />
            )}
            {dueDate && (
                <MetadataIndicator
                    icon={Calendar}
                    className={isOverdue ? "text-red-500" : undefined}
                >
                    {formatTaskDate(dueDate)}
                </MetadataIndicator>
            )}
        </>
    )
}

/** Priority indicator with color and label (used in list/compact variants) */
export function PriorityBadge({
    priority,
    config,
    t,
}: {
    readonly priority: string
    readonly config: { color: string }
    readonly t: ReturnType<typeof useTranslations>
}) {
    return (
        <>
            <span className={cn("w-1.5 h-1.5 rounded-full flex-shrink-0", config.color)} />
            <span className={cn("text-[10px] font-semibold uppercase tracking-wide", config.color.replace("bg-", "text-"))}>
                {t(`tasks.${priority}`)}
            </span>
        </>
    )
}

/**
 * Priority chip — filled pill with colored dot + label.
 * Mimics the mockup board's task priority chip.
 */
export function PriorityChip({
    priority,
    config,
    t,
}: {
    readonly priority: string
    readonly config: { color: string; chipTint: string; chipText: string }
    readonly t: ReturnType<typeof useTranslations>
}) {
    return (
        <span
            className={cn(
                "inline-flex items-center gap-1.5 rounded-full px-2 py-[2px] text-[10px] font-medium uppercase tracking-wide border border-transparent",
                config.chipTint,
                config.chipText,
            )}
        >
            <span className={cn("w-[5px] h-[5px] rounded-full inline-block", config.color)} />
            {t(`tasks.${priority}`)}
        </span>
    )
}

/** Small outlined chip, used for tags on the task card. */
export function TagChip({ children }: { readonly children: React.ReactNode }) {
    return (
        <span className="inline-flex items-center rounded-full border border-border/60 bg-muted/40 px-2 py-[2px] text-[10px] text-muted-foreground">
            {children}
        </span>
    )
}

/** Deterministic color for an assignee initials avatar (keeps cards varied). */
function avatarBgFor(name: string): string {
    const palette = [
        "bg-indigo-500/15 text-indigo-600 dark:text-indigo-300",
        "bg-emerald-500/15 text-emerald-600 dark:text-emerald-300",
        "bg-rose-500/15 text-rose-600 dark:text-rose-300",
        "bg-amber-500/15 text-amber-600 dark:text-amber-300",
        "bg-sky-500/15 text-sky-600 dark:text-sky-300",
        "bg-violet-500/15 text-violet-600 dark:text-violet-300",
    ]
    let hash = 0
    for (let i = 0; i < name.length; i++) hash = (hash * 31 + name.charCodeAt(i)) | 0
    return palette[Math.abs(hash) % palette.length]
}

/** Short initials extracted from a name. */
function initialsOf(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean)
    if (parts.length === 0) return "?"
    if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
}

/** Small circular avatar — picture if available, initials fallback. */
export function MiniAvatar({ name, avatarUrl, size = 20 }: { readonly name: string; readonly avatarUrl?: string; readonly size?: number }) {
    const dim = { width: size, height: size }
    if (avatarUrl) {
        return (
            <Image
                src={avatarUrl}
                alt={name}
                width={size}
                height={size}
                className="rounded-full object-cover"
                style={dim}
            />
        )
    }
    return (
        <span
            className={cn("inline-flex items-center justify-center rounded-full text-[10px] font-medium", avatarBgFor(name))}
            style={dim}
            aria-label={name}
            title={name}
        >
            {initialsOf(name)}
        </span>
    )
}

/** Compact identifier rendered in the card footer (#abcd1234). */
export function TaskShortId({ id }: { readonly id: string }) {
    const short = id.length > 8 ? id.slice(0, 8) : id
    return (
        <span className="font-mono text-[10px] text-muted-foreground tracking-tight" title={id}>
            #{short}
        </span>
    )
}
