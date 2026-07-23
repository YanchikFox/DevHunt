"use client"

import { memo, useMemo, useState, useCallback } from "react"
import { SortableContext, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable"
import { Button } from "@/components/ui/button"
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
    Plus,
    CheckCircle2,
    MoreVertical,
    Edit,
    Trash2,
    ChevronDown,
    ChevronRight,
} from "lucide-react"
import { cn } from "@/lib/utils"
import type { SortableColumnProps, TaskId, ColumnId, TaskCardVariant, Task } from "./types"
import { getTaskId } from "./utils"
import { TaskCard } from "./TaskCard"
import { DroppableColumn } from "./DroppableColumn"

function ColumnTaskList({ columnId, tasks, taskIds, activeTaskId, canEdit, onEditTask, onDeleteTask, variant }: {
  columnId: string; tasks: Task[]; taskIds: TaskId[]; activeTaskId: string | null
  canEdit: boolean; onEditTask: (task: Task) => void; onDeleteTask: (task: Task) => void; variant: TaskCardVariant
}) {
  return (
    <DroppableColumn dropId={`tasks-${columnId}`} canEdit={canEdit}>
      <SortableContext items={taskIds} strategy={verticalListSortingStrategy}>
        {tasks.map((task) => (
          <TaskCard
            key={getTaskId(task)}
            task={task}
            isDragging={activeTaskId === getTaskId(task)}
            canEdit={canEdit}
            onEdit={onEditTask}
            onDelete={onDeleteTask}
            variant={variant}
          />
        ))}
      </SortableContext>
    </DroppableColumn>
  )
}

// Sub-component for list view mode
function ColumnListView({
    column,
    tasks,
    taskIds,
    canEdit,
    activeTaskId,
    isCollapsed,
    setIsCollapsed,
    onCreateTask,
    onEditTask,
    onDeleteTask,
}: {
    readonly column: SortableColumnProps["column"]
    readonly tasks: SortableColumnProps["tasks"]
    readonly taskIds: TaskId[]
    readonly canEdit: boolean
    readonly activeTaskId: TaskId | null
    readonly isCollapsed: boolean
    readonly setIsCollapsed: (collapsed: boolean) => void
    readonly onCreateTask: (columnId: ColumnId) => void
    readonly onEditTask: SortableColumnProps["onEditTask"]
    readonly onDeleteTask: SortableColumnProps["onDeleteTask"]
}) {
    const handleHeaderClick = () => setIsCollapsed(!isCollapsed)
    const handleHeaderKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === "Enter" || e.key === " ") {
            e.preventDefault()
            setIsCollapsed(!isCollapsed)
        }
    }
    const handleAddTask = useCallback((e: React.MouseEvent) => {
        e.stopPropagation()
        onCreateTask(column.id)
    }, [onCreateTask, column.id])

    return (
        <div className="border-b border-border/40 last:border-b-0">
            <button
                type="button"
                className="flex items-center gap-2 px-3 py-2 hover:bg-background/40 cursor-pointer w-full text-left transition-colors"
                onClick={handleHeaderClick}
                onKeyDown={handleHeaderKeyDown}
            >
                {isCollapsed ? (
                    <ChevronRight className="h-3.5 w-3.5 text-muted-foreground" />
                ) : (
                    <ChevronDown className="h-3.5 w-3.5 text-muted-foreground" />
                )}
                <div
                    className="w-2 h-2 rounded-full"
                    style={{ backgroundColor: column.color || '#6b7280' }}
                />
                <span className="font-medium text-[13px]">{column.name}</span>
                <ColumnCountPill count={tasks.length} wipLimit={column.wipLimit} />
                {canEdit && (
                    <Button
                        variant="ghost"
                        size="sm"
                        className="h-6 w-6 p-0 ml-auto text-muted-foreground hover:text-foreground"
                        onClick={handleAddTask}
                        aria-label="Add task"
                    >
                        <Plus className="h-3.5 w-3.5" />
                    </Button>
                )}
            </button>
            {!isCollapsed && (
                <div className="pb-1">
                    <ColumnTaskList columnId={column.id} tasks={tasks} taskIds={taskIds} activeTaskId={activeTaskId} canEdit={canEdit} onEditTask={onEditTask} onDeleteTask={onDeleteTask} variant="list" />
                </div>
            )}
        </div>
    )
}

/** Compact count pill — turns red when WIP limit is exceeded (mockup behavior). */
function ColumnCountPill({
    count,
    wipLimit,
}: {
    readonly count: number
    readonly wipLimit?: number | null
}) {
    const over = typeof wipLimit === "number" && count > wipLimit
    return (
        <span
            className={cn(
                "inline-flex items-center font-mono rounded-full px-1.5 py-[1px] text-[10px] tabular-nums",
                over
                    ? "bg-red-500/15 text-red-600 dark:text-red-400"
                    : "bg-background/80 text-muted-foreground"
            )}
        >
            {count}
            {typeof wipLimit === "number" ? `/${wipLimit}` : ""}
        </span>
    )
}

// Sub-component for board view header
function ColumnBoardHeader({
    column,
    tasks,
    canEdit,
    dragHandleProps,
    onEditColumn,
    onDeleteColumn,
    onAddTask,
}: {
    readonly column: SortableColumnProps["column"]
    readonly tasks: SortableColumnProps["tasks"]
    readonly canEdit: boolean
    readonly dragHandleProps: Record<string, unknown>
    readonly onEditColumn: SortableColumnProps["onEditColumn"]
    readonly onDeleteColumn: SortableColumnProps["onDeleteColumn"]
    readonly onAddTask: () => void
}) {
    const handleEdit = useCallback(() => onEditColumn(column), [onEditColumn, column])
    const handleDelete = useCallback(() => onDeleteColumn(column), [onDeleteColumn, column])

    return (
        <div className="flex items-center gap-2 px-2 pt-2 pb-2.5 flex-shrink-0">
            <div
                className={cn(
                    "flex items-center gap-2 flex-1 min-w-0",
                    canEdit && "cursor-grab active:cursor-grabbing select-none"
                )}
                {...dragHandleProps}
            >
                <span
                    className="w-2 h-2 rounded-full flex-shrink-0"
                    style={{ backgroundColor: column.color || "var(--color-muted-foreground)" }}
                />
                {column.isCompleted && <CheckCircle2 className="h-3 w-3 text-emerald-500 flex-shrink-0" />}
                <span className="font-medium text-[13px] truncate">{column.name}</span>
                <ColumnCountPill count={tasks.length} wipLimit={column.wipLimit} />
            </div>
            {canEdit && (
                <div className="flex items-center gap-0.5">
                    <Button
                        variant="ghost"
                        size="sm"
                        className="h-6 w-6 p-0 text-muted-foreground hover:text-foreground"
                        onClick={onAddTask}
                        title="Add task"
                        aria-label="Add task"
                    >
                        <Plus className="h-3.5 w-3.5" />
                    </Button>
                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <Button
                                variant="ghost"
                                size="sm"
                                className="h-6 w-6 p-0 text-muted-foreground hover:text-foreground opacity-60 group-hover/column:opacity-100 transition-opacity"
                            >
                                <MoreVertical className="h-3.5 w-3.5" />
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-40">
                            <DropdownMenuItem onClick={handleEdit}>
                                <Edit className="h-3.5 w-3.5 mr-2" />
                                Edit
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem className="text-destructive" onClick={handleDelete}>
                                <Trash2 className="h-3.5 w-3.5 mr-2" />
                                Delete
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                </div>
            )}
        </div>
    )
}

// Main SortableColumn component
function SortableColumnImpl({
    column,
    tasks,
    canEdit,
    activeTaskId,
    viewMode,
    onEditColumn,
    onDeleteColumn,
    onCreateTask,
    onEditTask,
    onDeleteTask,
}: SortableColumnProps) {
    const [isCollapsed, setIsCollapsed] = useState(false)
    const handleCreateTask = useCallback(() => onCreateTask(column.id), [onCreateTask, column.id])

    // Stable list of task ids for the per-column `SortableContext`. dnd-kit uses
    // reference equality to decide whether to re-initialise the sortable tree,
    // so we keep the identity stable across renders when the order is unchanged.
    const taskIds = useMemo(() => tasks.map((task) => getTaskId(task)), [tasks])

    const {
        attributes,
        listeners,
        setNodeRef,
        transform,
        transition,
        isDragging,
    } = useSortable({
        id: column.id,
        data: { type: "column" },
        disabled: !canEdit,
    })

    const style = {
        transform: transform
            ? `translate3d(${transform.x}px, ${transform.y}px, 0)`
            : undefined,
        transition,
    }

    const dragHandleProps = canEdit ? { ...attributes, ...listeners } : {}

    // List view mode - collapsible section
    if (viewMode === "list") {
        return (
            <ColumnListView
                column={column}
                tasks={tasks}
                taskIds={taskIds}
                canEdit={canEdit}
                activeTaskId={activeTaskId}
                isCollapsed={isCollapsed}
                setIsCollapsed={setIsCollapsed}
                onCreateTask={onCreateTask}
                onEditTask={onEditTask}
                onDeleteTask={onDeleteTask}
            />
        )
    }

    // Board view mode — mockup-style: subtle tile with compact header and tight spacing.
    // Height is driven by the parent (the board container gives each column a capped
    // viewport-relative height) so the column flexes nicely whether there are 3 or 30
    // cards; internal overflow handles the overflow.
    return (
        <div
            ref={setNodeRef}
            className={cn(
                "group/column flex-shrink-0 w-[280px] rounded-[14px] bg-muted/40 dark:bg-muted/25 flex flex-col h-full max-h-full transition-shadow",
                isDragging && "ring-1 ring-primary/40 shadow-lg opacity-70"
            )}
            style={style}
        >
            <ColumnBoardHeader
                column={column}
                tasks={tasks}
                canEdit={canEdit}
                dragHandleProps={dragHandleProps}
                onEditColumn={onEditColumn}
                onDeleteColumn={onDeleteColumn}
                onAddTask={handleCreateTask}
            />

            {/* Column content — `scrollbar-gutter: stable both-edges` mirrors
                the scrollbar reservation on the *left* side too, so the cards
                stay perfectly centered whether the scrollbar is visible or
                not. The scrollbar itself is thin and quiet (transparent thumb
                at rest, tinted on hover) but still draggable for users
                without a wheel / trackpad. */}
            <div
                className={cn(
                    "px-2 pb-2 flex-1 overflow-y-auto min-h-0",
                    "[scrollbar-gutter:stable_both-edges]",
                    "[scrollbar-width:thin] [scrollbar-color:transparent_transparent]",
                    "group-hover/column:[scrollbar-color:rgb(120_120_120/0.30)_transparent]",
                    "[&::-webkit-scrollbar]:w-1.5",
                    "[&::-webkit-scrollbar-track]:bg-transparent",
                    "[&::-webkit-scrollbar-thumb]:bg-transparent",
                    "[&::-webkit-scrollbar-thumb]:rounded-full",
                    "[&::-webkit-scrollbar-thumb]:transition-colors",
                    "group-hover/column:[&::-webkit-scrollbar-thumb]:bg-muted-foreground/25"
                )}
            >
                <ColumnTaskList columnId={column.id} tasks={tasks} taskIds={taskIds} activeTaskId={activeTaskId} canEdit={canEdit} onEditTask={onEditTask} onDeleteTask={onDeleteTask} variant="default" />

                {/* Subtle add-task footer action (hidden when column is empty — header + button suffices there) */}
                {canEdit && tasks.length > 0 && (
                    <Button
                        variant="ghost"
                        size="sm"
                        className="w-full mt-1 text-muted-foreground hover:text-foreground justify-start h-7 text-[12px] opacity-0 group-hover/column:opacity-100 transition-opacity"
                        onClick={handleCreateTask}
                    >
                        <Plus className="h-3 w-3 mr-1.5" />
                        Add task
                    </Button>
                )}
            </div>
        </div>
    )
}

/**
 * Memoise to avoid re-rendering every column on unrelated state changes (active
 * task id, filter input keystrokes, etc.). The comparator deliberately treats
 * array references as identity — parent memoises `tasksByColumn` already.
 */
const MemoSortableColumn = memo(SortableColumnImpl)
MemoSortableColumn.displayName = "SortableColumn"
export { MemoSortableColumn as SortableColumn }
