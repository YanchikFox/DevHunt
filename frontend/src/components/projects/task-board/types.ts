"use client"

import type { TaskColumn } from "@/lib/api/task-board-types"

// =============================================================================
// Domain-Specific Type Aliases
// =============================================================================
// These type aliases provide semantic meaning to primitives, improving code
// readability and enabling future type-safe refinements (e.g., branded types).

/** Unique identifier for a task board column */
export type ColumnId = string

/** Unique identifier for a task */
export type TaskId = string

/** Priority level for tasks */
export type Priority = "low" | "medium" | "high" | "urgent"

/** Type of item being dragged in the board */
export type DragType = "task" | "column"

/** Filter for searching tasks */
export type SearchQuery = string

/** View mode for the task board */
export type ViewMode = "board" | "list"

/** Task card display variant */
export type TaskCardVariant = "default" | "compact" | "list"

/** Map of column IDs to their tasks */
export type TasksByColumn = Record<ColumnId, Task[]>

// =============================================================================
// Entity Interfaces
// =============================================================================

// Task entity with both camelCase and PascalCase variants for API compatibility
export interface Task {
    readonly Id?: TaskId
    readonly id?: TaskId
    readonly Title?: string
    readonly title?: string
    readonly Description?: string
    readonly description?: string
    readonly Priority?: string  // Use Priority type for strict typing in local code
    readonly priority?: string
    readonly Deadline?: string
    readonly deadline?: string
    readonly dueDate?: string
    readonly columnId?: ColumnId
    readonly assigneeName?: string
    readonly assigneeAvatarUrl?: string
    readonly attachmentsCount?: number
    readonly commentsCount?: number
    readonly Tags?: string
    readonly tags?: string
    // GitHub Issue sync fields
    readonly GitHubIssueId?: number
    readonly gitHubIssueId?: number
    readonly GitHubIssueNumber?: number
    readonly gitHubIssueNumber?: number
    readonly GitHubIssueUrl?: string
    readonly gitHubIssueUrl?: string
}

export interface ProjectTaskBoardProps {
    readonly columns: TaskColumn[]
    readonly tasksByColumn: TasksByColumn
    readonly isLoading: boolean
    readonly canEdit: boolean
    readonly onAddColumn: () => void
    readonly onEditColumn: (column: TaskColumn) => void
    readonly onDeleteColumn: (column: TaskColumn) => void
    readonly onCreateTask: (columnId: ColumnId) => void
    readonly onEditTask: (task: Task) => void
    readonly onDeleteTask: (task: Task) => void
    /** Cross-column move. `positionInColumn` is optional — omit to append. */
    readonly onMoveTask: (taskId: TaskId, targetColumnId: ColumnId, positionInColumn?: number) => Promise<void>
    /** Reorder tasks inside a single column. Receives the new order of task ids. */
    readonly onReorderTasks: (columnId: ColumnId, orderedTaskIds: TaskId[]) => Promise<void> | void
    readonly onReorderColumns: (columnIds: ColumnId[]) => Promise<void> | void
    /**
     * Optional slot rendered on the left of the toolbar row (title, caption,
     * team avatars, …). Lets the parent share a single header row with the
     * search / filter / view controls instead of stacking them in two rows.
     */
    readonly headerStart?: React.ReactNode
}

export interface TaskCardProps {
    readonly task: Task
    readonly isDragging: boolean
    readonly canEdit: boolean
    readonly onEdit: (task: Task) => void
    readonly onDelete: (task: Task) => void
    readonly variant?: TaskCardVariant
}

export interface DroppableColumnProps {
    readonly dropId: ColumnId
    readonly children: React.ReactNode
    readonly canEdit: boolean
}

export interface SortableColumnProps {
    readonly column: TaskColumn
    readonly tasks: Task[]
    readonly canEdit: boolean
    readonly activeTaskId: TaskId | null
    readonly viewMode: ViewMode
    readonly onEditColumn: (column: TaskColumn) => void
    readonly onDeleteColumn: (column: TaskColumn) => void
    readonly onCreateTask: (columnId: ColumnId) => void
    readonly onEditTask: (task: Task) => void
    readonly onDeleteTask: (task: Task) => void
}

export interface PriorityConfig {
    /** Solid color dot class (e.g. `bg-red-500`). */
    readonly color: string
    /** Tailwind class used as filled-pill background for the chip. */
    readonly chipTint: string
    /** Foreground class used on chip text. */
    readonly chipText: string
    readonly label: string
    readonly order: number
}

/** Map of priority values to their configuration. Uses string keys for API compatibility. */
export type PriorityConfigMap = Readonly<Record<string, PriorityConfig>>
