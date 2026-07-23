"use client"

import React, { memo, useCallback, useMemo } from "react"
import { useTranslations } from "next-intl"
import { useSortable } from "@dnd-kit/sortable"
import { Button } from "@/components/ui/button"
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
    MoreVertical,
    Edit,
    Trash2,
    User,
    Clock,
    Paperclip,
    MessageSquare,
} from "lucide-react"
import { cn } from "@/lib/utils"
import type { TaskCardProps, Task, PriorityConfigMap, Priority } from "./types"
import {
    getPriorityConfig,
    getTaskId,
    isTaskOverdue,
    formatTaskDate,
    getTaskTitle,
    getTaskDescription,
    getTaskPriority,
    getTaskDueDate,
    getTaskTags,
    getGitHubIssueUrl,
    getGitHubIssueNumber,
    hasTaskMetadata,
} from "./utils"
import {
    DragHandleDots,
    GitHubIssueBadge,
    MiniAvatar,
    PriorityBadge,
    PriorityChip,
    TagChip,
    TaskIndicators,
    TaskShortId,
} from "./TaskCardParts"

// List variant component

interface ListVariantProps {
    readonly task: Task
    readonly isDragging: boolean
    readonly canEdit: boolean
    readonly onEdit: (task: Task) => void
    readonly onDelete: (task: Task) => void
    readonly nodeRef: (node: HTMLElement | null) => void
    readonly style: React.CSSProperties | undefined
    readonly dragProps: Record<string, unknown>
    readonly priorityConfig: PriorityConfigMap
    readonly priority: Priority
    readonly t: ReturnType<typeof useTranslations>
}

function useCardKeyDownHandler(onEdit: (task: Task) => void, task: Task) {
  return useCallback((e: React.KeyboardEvent) => {
    if ((e.target as HTMLElement).closest('[data-no-click]')) return
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault()
      onEdit(task)
    }
  }, [onEdit, task])
}

function TaskCardListVariant({
    task,
    isDragging,
    canEdit,
    onEdit,
    onDelete,
    nodeRef,
    style,
    dragProps,
    priorityConfig,
    priority,
    t,
}: ListVariantProps) {
    const title = getTaskTitle(task)
    const dueDate = getTaskDueDate(task)
    const isOverdue = isTaskOverdue(dueDate)
    const pConfig = priorityConfig[priority] || priorityConfig.medium

    const handleCardClick = useCallback(() => onEdit(task), [onEdit, task])
    const handleDragClick = useCallback((e: React.MouseEvent) => e.stopPropagation(), [])
    const handleDragKeyDown = useCallback((e: React.KeyboardEvent) => e.stopPropagation(), [])
    const handleDeleteClick = useCallback((e: React.MouseEvent) => { e.stopPropagation(); onDelete(task) }, [onDelete, task])
    const handleCardKeyDown = useCardKeyDownHandler(onEdit, task)

    return (
        <button
            type="button"
            ref={nodeRef}
            style={style}
            className={cn(
                "group flex items-center gap-3 px-3 py-2 border-b border-border/30 last:border-b-0 hover:bg-background/50 transition-colors cursor-pointer w-full text-left outline-none focus-visible:ring-1 focus-visible:ring-ring/40",
                isDragging && "opacity-50 bg-muted"
            )}
            onClick={handleCardClick}
            onKeyDown={handleCardKeyDown}
        >
            {/* Drag handle */}
            <button
                type="button"
                tabIndex={canEdit ? 0 : -1}
                data-no-click
                aria-label="Drag to reorder"
                className={cn(
                    "w-4 h-4 flex items-center justify-center cursor-grab active:cursor-grabbing border-0 bg-transparent p-0",
                    canEdit ? "opacity-50 hover:opacity-100" : "opacity-0 pointer-events-none"
                )}
                {...(canEdit ? dragProps : {})}
                onClick={handleDragClick}
                onKeyDown={handleDragKeyDown}
            >
                <DragHandleDots />
            </button>

            <PriorityBadge priority={priority} config={pConfig} t={t} />
            <span className="flex-1 text-sm truncate">{title}</span>
            <TaskIndicators task={task} dueDate={dueDate} isOverdue={isOverdue} />

            {/* Delete button */}
            {canEdit && (
                <div className="opacity-0 group-hover:opacity-100 flex items-center gap-0.5">
                    <Button
                        variant="ghost"
                        size="sm"
                        data-no-click
                        className="h-6 w-6 p-0 text-destructive"
                        onClick={handleDeleteClick}
                    >
                        <Trash2 className="h-3 w-3" />
                    </Button>
                </div>
            )}
        </button>
    )
}

// Compact variant component

interface CompactVariantProps {
    readonly task: Task
    readonly isDragging: boolean
    readonly canEdit: boolean
    readonly onEdit: (task: Task) => void
    readonly nodeRef: (node: HTMLElement | null) => void
    readonly style: React.CSSProperties | undefined
    readonly dragProps: Record<string, unknown>
    readonly priorityConfig: PriorityConfigMap
    readonly priority: Priority
}

function TaskCardCompactVariant({
    task,
    canEdit,
    onEdit,
    nodeRef,
    style,
    dragProps,
    priorityConfig,
    priority,
}: CompactVariantProps) {
    const title = getTaskTitle(task)
    const pConfig = priorityConfig[priority] || priorityConfig.medium
    const handleEditClick = useCallback((e: React.MouseEvent) => { e.stopPropagation(); onEdit(task) }, [onEdit, task])

    return (
        <div
            ref={nodeRef}
            style={style}
            className={cn(
                "group flex items-center gap-2 p-2 rounded-md hover:bg-muted/60 transition-colors duration-150",
                canEdit && "cursor-grab active:cursor-grabbing"
            )}
            {...dragProps}
        >
            <span className={cn("w-1.5 h-1.5 rounded-full flex-shrink-0", pConfig.color)} />
            <span className="text-sm truncate flex-1">{title}</span>
            {canEdit && (
                <Button
                    variant="ghost"
                    size="sm"
                    className="h-5 w-5 p-0 opacity-0 group-hover:opacity-100"
                    onClick={handleEditClick}
                >
                    <MoreVertical className="h-3 w-3" />
                </Button>
            )}
        </div>
    )
}

// Default card content

interface DefaultCardContentProps {
    readonly task: Task
    readonly canEdit: boolean
    readonly onEdit: (task: Task) => void
    readonly onDelete: (task: Task) => void
    readonly priorityConfig: PriorityConfigMap
    readonly priority: Priority
    readonly t: ReturnType<typeof useTranslations>
}

/** Task tags display — mockup-style outlined chips */
function TaskTagsList({ tags }: { readonly tags: string }) {
    const items = tags
        .split(",")
        .map((t) => t.trim())
        .filter(Boolean)
    if (items.length === 0) return null
    return (
        <span className="flex flex-wrap gap-1 mt-1.5">
            {items.map((tag) => (
                <TagChip key={tag}>{tag}</TagChip>
            ))}
        </span>
    )
}

function TaskCardActions({
    task,
    onEdit,
    onDelete,
    t,
}: {
    readonly task: Task
    readonly onEdit: (task: Task) => void
    readonly onDelete: (task: Task) => void
    readonly t: ReturnType<typeof useTranslations>
}) {
    const handleEditClick = useCallback((e: React.MouseEvent) => { e.stopPropagation(); onEdit(task) }, [onEdit, task])
    const handleDeleteClick = useCallback((e: React.MouseEvent) => { e.stopPropagation(); onDelete(task) }, [onDelete, task])

    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild data-no-click>
                <Button variant="ghost" size="sm" className="h-6 w-6 p-0 opacity-0 group-hover:opacity-100 flex-shrink-0">
                    <MoreVertical className="h-3.5 w-3.5" />
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-36">
                <DropdownMenuItem onClick={handleEditClick}>
                    <Edit className="h-3.5 w-3.5 mr-2" />
                    {t("common.edit")}
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem className="text-destructive" onClick={handleDeleteClick}>
                    <Trash2 className="h-3.5 w-3.5 mr-2" />
                    {t("common.delete")}
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    )
}

function DefaultCardContent({
    task,
    canEdit,
    onEdit,
    onDelete,
    priorityConfig,
    priority,
    t,
}: DefaultCardContentProps) {
    const title = getTaskTitle(task)
    const description = getTaskDescription(task)
    const dueDate = getTaskDueDate(task)
    const tags = getTaskTags(task)
    const pConfig = priorityConfig[priority] || priorityConfig.medium
    const isOverdue = isTaskOverdue(dueDate)
    const taskId = getTaskId(task)
    const showFooterMeta = hasTaskMetadata(task)

    return (
        <span className="p-2.5 block">
            {/* Title + action menu */}
            <span className="flex items-start gap-1.5">
                <span className="flex-1 min-w-0">
                    <span className="font-medium text-[13px] leading-[1.4] line-clamp-2 block">{title}</span>
                    {description && (
                        <span className="text-[11px] text-muted-foreground line-clamp-2 mt-1 block">{description}</span>
                    )}
                </span>
                {canEdit && <TaskCardActions task={task} onEdit={onEdit} onDelete={onDelete} t={t} />}
            </span>

            {/* Priority + tags row */}
            <span className="flex items-center gap-1.5 flex-wrap mt-2">
                <PriorityChip priority={priority} config={pConfig} t={t} />
                {tags && <TaskTagsList tags={tags} />}
            </span>

            {/* Footer: assignee avatar + short id, with extra metadata (due date, comments, github) */}
            {(showFooterMeta || taskId) && (
                <span className="flex items-center justify-between gap-2 mt-2 pt-2 border-t border-border/40">
                    <span className="flex items-center gap-1.5 min-w-0">
                        {task.assigneeName ? (
                            <>
                                <MiniAvatar name={task.assigneeName} avatarUrl={task.assigneeAvatarUrl} size={20} />
                                <span className="text-[11px] text-muted-foreground truncate max-w-[100px]">
                                    {task.assigneeName}
                                </span>
                            </>
                        ) : (
                            <span className="inline-flex items-center justify-center h-5 w-5 rounded-full border border-dashed border-border/60 text-muted-foreground">
                                <User className="h-3 w-3" />
                            </span>
                        )}
                    </span>
                    <span className="flex items-center gap-1.5 flex-shrink-0">
                        <TaskFooterIndicators task={task} dueDate={dueDate} isOverdue={isOverdue} />
                        {taskId && <TaskShortId id={taskId} />}
                    </span>
                </span>
            )}
        </span>
    )
}

/**
 * Compact footer indicators — shown to the right of the footer, next to the id.
 * Keeps icon-only pills (attachments / comments / github / due date).
 */
function TaskFooterIndicators({
    task,
    dueDate,
    isOverdue,
}: {
    readonly task: Task
    readonly dueDate?: string
    readonly isOverdue?: boolean
}) {
    const attachmentsCount = task.attachmentsCount || 0
    const commentsCount = task.commentsCount || 0
    const gitHubIssueUrl = getGitHubIssueUrl(task)
    const gitHubIssueNumber = getGitHubIssueNumber(task)
    return (
        <>
            {dueDate && (
                <span className={cn(
                    "inline-flex items-center gap-1 text-[10px]",
                    isOverdue ? "text-red-500" : "text-muted-foreground",
                )}>
                    <Clock className="h-3 w-3" />
                    {formatTaskDate(dueDate)}
                </span>
            )}
            {attachmentsCount > 0 && (
                <span className="inline-flex items-center gap-0.5 text-[10px] text-muted-foreground">
                    <Paperclip className="h-3 w-3" />
                    {attachmentsCount}
                </span>
            )}
            {commentsCount > 0 && (
                <span className="inline-flex items-center gap-0.5 text-[10px] text-muted-foreground">
                    <MessageSquare className="h-3 w-3" />
                    {commentsCount}
                </span>
            )}
            {gitHubIssueUrl && (
                <GitHubIssueBadge url={gitHubIssueUrl} issueNumber={gitHubIssueNumber} size="sm" />
            )}
        </>
    )
}

// Main TaskCard component

/** Horizontal drag handle dots for default card */
function HorizontalDragDots() {
    return (
        <span className="flex gap-0.5">
            <span className="w-1 h-1 rounded-full bg-muted-foreground/40 block" />
            <span className="w-1 h-1 rounded-full bg-muted-foreground/40 block" />
            <span className="w-1 h-1 rounded-full bg-muted-foreground/40 block" />
        </span>
    )
}

/** Default card variant */
function TaskCardDefaultVariant({
    task,
    canEdit,
    onEdit,
    onDelete,
    nodeRef,
    style,
    dragProps,
    priorityConfig,
    priority,
    t,
}: ListVariantProps) {
    const handleCardClick = useCallback((e: React.MouseEvent) => {
        if ((e.target as HTMLElement).closest('[data-no-click]')) return
        onEdit(task)
    }, [onEdit, task])

    const handleCardKeyDown = useCardKeyDownHandler(onEdit, task)

    const handleDragClick = useCallback((e: React.MouseEvent) => e.stopPropagation(), [])
    const handleDragKeyDown = useCallback((e: React.KeyboardEvent) => e.stopPropagation(), [])

    return (
        <div
            role="button"
            tabIndex={0}
            ref={nodeRef}
            style={style}
            className={cn(
                "group rounded-lg bg-card border border-border/60 hover:border-primary/40 hover:shadow-md cursor-pointer w-full text-left outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
                // Scope transitions to visual-only props so toggling `visibility`
                // (used while dragging) is instantaneous and doesn't delay hiding.
                "transition-[border-color,box-shadow] duration-150"
            )}
            onClick={handleCardClick}
            onKeyDown={handleCardKeyDown}
        >
            {canEdit && (
                <button
                    {...dragProps}
                    type="button"
                    tabIndex={0}
                    aria-label="Drag to reorder"
                    className="flex items-center justify-center h-5 w-full border-0 border-b border-border/30 opacity-0 group-hover:opacity-100 transition-opacity cursor-grab active:cursor-grabbing bg-muted/30 rounded-t-lg p-0"
                    data-no-click
                    onClick={handleDragClick}
                    onKeyDown={handleDragKeyDown}
                >
                    <HorizontalDragDots />
                </button>
            )}

            <DefaultCardContent
                task={task}
                canEdit={canEdit}
                onEdit={onEdit}
                onDelete={onDelete}
                priorityConfig={priorityConfig}
                priority={priority}
                t={t}
            />
        </div>
    )
}

function TaskCardImpl({
    task,
    isDragging,
    canEdit,
    onEdit,
    onDelete,
    variant = "default",
}: Readonly<TaskCardProps>) {
    const taskId = getTaskId(task)
    // `useSortable` gives us reorder semantics within a `SortableContext`, while
    // still behaving like a regular draggable when there's no context (cross-column
    // moves are handled by the outer board's drag handlers).
    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
        isDragging: sortableIsDragging,
    } = useSortable({
        id: taskId,
        disabled: !canEdit,
        data: { type: "task" },
    })

    const dragging = isDragging || sortableIsDragging

    // The board renders a <DragOverlay/> for the *visual* drag, so we hide the
    // original card to avoid duplicate previews and clipping inside the column's
    // vertical scroll container. Everything else (spring, transition) is owned
    // by the overlay so we keep the source item static.
    const style: React.CSSProperties | undefined = dragging
        ? { visibility: "hidden" }
        : transform
            ? {
                transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`,
                transition,
            }
            : undefined

    const t = useTranslations()
    // `getPriorityConfig` allocates a fresh object on every call. Memoising it
    // against the translator keeps the `priorityConfig` reference stable across
    // drags, letting memoised children skip re-rendering.
    const priorityConfig = useMemo(() => getPriorityConfig(t), [t])
    const priority = getTaskPriority(task)
    const dragProps = { ...attributes, ...listeners }

    const commonProps = {
        task,
        isDragging: dragging,
        canEdit,
        onEdit,
        onDelete,
        nodeRef: setNodeRef,
        style,
        dragProps,
        priorityConfig,
        priority,
        t,
    }

    if (variant === "list") {
        return <TaskCardListVariant {...commonProps} />
    }

    if (variant === "compact") {
        return <TaskCardCompactVariant {...commonProps} />
    }

    return <TaskCardDefaultVariant {...commonProps} />
}

/**
 * Memoised to stop every card in the board from re-rendering when one of them
 * is dragged. With 20+ cards in a column this was the main source of the
 * "laggy / jumpy" drag feel — dnd-kit updates context on every pointer move.
 */
export const TaskCard = memo(TaskCardImpl, (prev, next) => {
    if (prev.canEdit !== next.canEdit) return false
    if (prev.isDragging !== next.isDragging) return false
    if (prev.variant !== next.variant) return false
    if (prev.onEdit !== next.onEdit) return false
    if (prev.onDelete !== next.onDelete) return false
    // Shallow-compare the task fields we actually render.
    const a = prev.task
    const b = next.task
    if (a === b) return true
    return (
        (a.Id || a.id) === (b.Id || b.id) &&
        (a.Title || a.title) === (b.Title || b.title) &&
        (a.Description || a.description) === (b.Description || b.description) &&
        (a.Priority || a.priority) === (b.Priority || b.priority) &&
        (a.Deadline || a.deadline || a.dueDate) === (b.Deadline || b.deadline || b.dueDate) &&
        a.assigneeName === b.assigneeName &&
        a.assigneeAvatarUrl === b.assigneeAvatarUrl &&
        (a.attachmentsCount ?? 0) === (b.attachmentsCount ?? 0) &&
        (a.commentsCount ?? 0) === (b.commentsCount ?? 0) &&
        ((a.gitHubIssueNumber ?? a.GitHubIssueNumber ?? null) ===
            (b.gitHubIssueNumber ?? b.GitHubIssueNumber ?? null))
    )
})
TaskCard.displayName = "TaskCard"
