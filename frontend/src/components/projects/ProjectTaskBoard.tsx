"use client"

import { useTranslations } from "next-intl"
import { Card } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import {
  Plus,
  Columns3,
  Search,
  X,
  List,
} from "lucide-react"
import { Skeleton } from "@/components/ui/skeleton"
import {
  DndContext,
  DragOverlay,
  DragStartEvent,
  DragEndEvent,
  closestCorners,
  closestCenter,
  PointerSensor,
  useSensor,
  useSensors,
  defaultDropAnimationSideEffects,
  type DropAnimation,
} from "@dnd-kit/core"
import {
  SortableContext,
  arrayMove,
  horizontalListSortingStrategy,
} from "@dnd-kit/sortable"
import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { cn } from "@/lib/utils"

import {
  type Task,
  type ViewMode,
  type ProjectTaskBoardProps,
  type TasksByColumn,
  type TaskId,
  type ColumnId,
  type DragType,
  getPriorityConfig,
  collectAvailableTags,
  splitTags,
  getTaskId,
} from "./task-board"
import { SortableColumn } from "./task-board/SortableColumn"
import { FilterDropdown } from "./task-board/FilterDropdown"
import type { TaskColumn } from "@/lib/api/task-board-types"

// Re-export types for consumers
export type { ProjectTaskBoardProps } from "./task-board"

// Filter predicate functions to reduce complexity
const matchesSearch = (task: Task, query: string): boolean => {
  if (!query) return true
  const title = (task.Title || task.title || "").toLowerCase()
  const desc = (task.Description || task.description || "").toLowerCase()
  const lowerQuery = query.toLowerCase()
  return title.includes(lowerQuery) || desc.includes(lowerQuery)
}

const matchesPriority = (task: Task, priorities: string[]): boolean => {
  if (priorities.length === 0) return true
  const taskPriority = (task.Priority || task.priority || "medium").toLowerCase()
  return priorities.includes(taskPriority)
}

const matchesTags = (task: Task, tags: string[]): boolean => {
  if (tags.length === 0) return true
  const taskTags = splitTags(task.Tags || task.tags)
  return tags.some((tag) => taskTags.includes(tag))
}

// Hooks for the main component
function useTaskFiltering(
  tasksByColumn: TasksByColumn,
  searchQuery: string,
  priorityFilter: string[],
  tagFilter: string[]
) {
  return useMemo(() => {
    const result: TasksByColumn = {}

    for (const [columnId, tasks] of Object.entries(tasksByColumn)) {
      result[columnId] = tasks.filter((task) =>
        matchesSearch(task, searchQuery) &&
        matchesPriority(task, priorityFilter) &&
        matchesTags(task, tagFilter)
      )
    }

    return result
  }, [tasksByColumn, searchQuery, priorityFilter, tagFilter])
}

// Helper to check if event target is an editable element
const isEditableElement = (target: EventTarget | null): boolean =>
  target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement

// Helper to check if key is search shortcut
const isSearchShortcut = (e: globalThis.KeyboardEvent): boolean =>
  e.key === "/" && !e.metaKey && !e.ctrlKey

function useKeyboardShortcuts() {
  useEffect(() => {
    const handleKeyDown = (e: globalThis.KeyboardEvent) => {
      if (isEditableElement(e.target)) return
      if (!isSearchShortcut(e)) return

      e.preventDefault()
      document.getElementById("task-search")?.focus()
    }

    globalThis.addEventListener("keydown", handleKeyDown)
    return () => globalThis.removeEventListener("keydown", handleKeyDown)
  }, [])
}

function TaskBoardSkeleton() {
  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <Skeleton className="h-9 w-[200px]" />
        <Skeleton className="h-9 w-9" />
        <Skeleton className="h-9 w-24 ml-auto" />
      </div>
      <div className="flex gap-4">
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-[300px] w-[280px] rounded-xl" />
        ))}
      </div>
    </div>
  )
}

// Search input with clear button — slim, ghost-on-transparent styling so the
// toolbar reads like the rest of the app chrome (press `/` to focus). Expands
// on focus so it doesn't eat horizontal space when idle.
function SearchInput({
  value,
  onChange,
}: {
  readonly value: string
  readonly onChange: (value: string) => void
}) {
  const t = useTranslations()
  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => onChange(e.target.value), [onChange])
  const handleClear = useCallback(() => onChange(""), [onChange])
  return (
    <div className="relative group/search">
      <Search className="absolute left-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
      <Input
        id="task-search"
        value={value}
        onChange={handleChange}
        placeholder={t("taskBoard.searchPlaceholder")}
        className={cn(
          "pl-7 pr-6 h-7 w-[180px] rounded-md border-0 bg-muted/40 text-[12px]",
          "shadow-none focus-visible:ring-1 focus-visible:ring-ring/40 focus-visible:bg-background",
          "transition-[width,background-color] focus-visible:w-[240px] placeholder:text-muted-foreground/70"
        )}
      />
      {value && (
        <Button
          variant="ghost"
          size="sm"
          className="absolute right-0.5 top-1/2 -translate-y-1/2 h-5 w-5 p-0 text-muted-foreground hover:text-foreground"
          onClick={handleClear}
        >
          <X className="h-3 w-3" />
        </Button>
      )}
      <kbd
        className={cn(
          "pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 hidden h-4 select-none items-center gap-0.5 rounded border border-border/60 bg-background/80 px-1 font-mono text-[9px] text-muted-foreground",
          !value && "group-focus-within/search:hidden md:inline-flex"
        )}
      >
        /
      </kbd>
    </div>
  )
}

// Priority and Tags filter dropdowns are now consolidated in FilterDropdown component

// View mode switcher (board/list) — micro pill that matches the toolbar scale
function ViewModeSwitcher({
  viewMode,
  setViewMode,
  t,
}: {
  readonly viewMode: ViewMode
  readonly setViewMode: (mode: ViewMode) => void
  readonly t: ReturnType<typeof useTranslations>
}) {
  const handleSetBoard = useCallback(() => setViewMode("board"), [setViewMode])
  const handleSetList = useCallback(() => setViewMode("list"), [setViewMode])
  return (
    <div className="flex items-center gap-0 bg-muted/40 rounded-md p-0.5">
      <button
        type="button"
        onClick={handleSetBoard}
        title={t("tasks.kanbanBoard")}
        className={cn(
          "h-6 w-6 inline-flex items-center justify-center rounded-[4px] transition-colors",
          viewMode === "board"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
      >
        <Columns3 className="h-3.5 w-3.5" />
      </button>
      <button
        type="button"
        onClick={handleSetList}
        title={t("tasks.listView")}
        className={cn(
          "h-6 w-6 inline-flex items-center justify-center rounded-[4px] transition-colors",
          viewMode === "list"
            ? "bg-background text-foreground shadow-sm"
            : "text-muted-foreground hover:text-foreground"
        )}
      >
        <List className="h-3.5 w-3.5" />
      </button>
    </div>
  )
}

// Task count display — mono caption that fades into the toolbar rather than
// shouting like a full-weight text chunk.
function TaskCount({
  filtered,
  total,
  t,
}: {
  readonly filtered: number
  readonly total: number
  readonly t: ReturnType<typeof useTranslations>
}) {
  return (
    <span className="font-mono text-[11px] tabular-nums text-muted-foreground">
      {filtered === total
        ? t("tasks.taskCount", { count: total })
        : t("tasks.taskCountFiltered", { filtered, total })}
    </span>
  )
}

// Toolbar props interface
interface TaskBoardToolbarProps {
  readonly searchQuery: string
  readonly setSearchQuery: (query: string) => void
  readonly priorityFilter: string[]
  readonly setPriorityFilter: (filter: string[]) => void
  readonly tagFilter: string[]
  readonly setTagFilter: (filter: string[]) => void
  readonly availableTags: string[]
  readonly tagLabels: Record<string, string>
  readonly priorityConfig: ReturnType<typeof getPriorityConfig>
  readonly filteredTotalTasks: number
  readonly totalTasks: number
  readonly viewMode: ViewMode
  readonly setViewMode: (mode: ViewMode) => void
  readonly t: ReturnType<typeof useTranslations>
}

// Toolbar component — slim, single-row, visually quiet. Add-column is
// rendered *inside* the board now (via `AddColumnPlaceholder`) so we don't
// duplicate the action in two places.
//
// The toolbar can optionally share its row with a `headerStart` slot (e.g.
// the page title + team avatars) — the slot is left-aligned and the controls
// float to the right, giving us one unified header instead of two bars.
function TaskBoardToolbar({
  searchQuery,
  setSearchQuery,
  priorityFilter,
  setPriorityFilter,
  tagFilter,
  setTagFilter,
  availableTags,
  tagLabels,
  priorityConfig,
  filteredTotalTasks,
  totalTasks,
  viewMode,
  setViewMode,
  headerStart,
  t,
}: TaskBoardToolbarProps & { readonly headerStart?: React.ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-4 flex-wrap min-h-[28px]">
      {headerStart ? (
        <div className="flex items-end gap-4 flex-wrap min-w-0">{headerStart}</div>
      ) : null}
      <div className="flex items-center gap-2 flex-wrap ml-auto">
        <SearchInput value={searchQuery} onChange={setSearchQuery} />
        <div className="h-4 w-px bg-border/60 mx-0.5" aria-hidden />
        <FilterDropdown
          filter={priorityFilter}
          setFilter={setPriorityFilter}
          items={Object.keys(priorityConfig)}
          getLabel={(key) => priorityConfig[key].label}
          getColor={(key) => priorityConfig[key].color}
          title={t("tasks.priority")}
          filterLabel={t("tasks.filterByPriority")}
          t={t}
        />
        <FilterDropdown
          filter={tagFilter}
          setFilter={setTagFilter}
          items={availableTags}
          getLabel={(tag) => tagLabels[tag] ?? tag}
          title={t("tasks.tags")}
          filterLabel={t("tasks.filterByTag")}
          contentWidth="w-44"
          t={t}
        />
        <TaskCount filtered={filteredTotalTasks} total={totalTasks} t={t} />
        <ViewModeSwitcher viewMode={viewMode} setViewMode={setViewMode} t={t} />
      </div>
    </div>
  )
}

// Drag overlay content
function DragOverlayContent({
  dragType,
  activeTask,
  activeColumn,
  filteredTasksByColumn,
  priorityConfig,
}: {
  readonly dragType: DragType | null
  readonly activeTask: Task | null
  readonly activeColumn: { id: ColumnId; name: string; color?: string } | null
  readonly filteredTasksByColumn: TasksByColumn
  readonly priorityConfig: ReturnType<typeof getPriorityConfig>
}) {
  if (dragType === "task" && activeTask) {
    const priority = (activeTask.Priority || activeTask.priority || "medium").toLowerCase()
    return (
      <div className="rounded-lg bg-card border border-primary/30 p-2.5 shadow-xl rotate-2 w-[260px]">
        <div className="flex items-center gap-2">
          <div className={cn(
            "w-1.5 h-1.5 rounded-full",
            priorityConfig[priority]?.color || "bg-blue-500"
          )} />
          <h4 className="font-medium text-[13px] leading-tight line-clamp-2">{activeTask.Title || activeTask.title}</h4>
        </div>
      </div>
    )
  }

  if (dragType === "column" && activeColumn) {
    return (
      <div className="w-[280px] rounded-[14px] bg-muted/60 dark:bg-muted/40 shadow-lg ring-1 ring-border/60 p-2">
        <div className="flex items-center gap-2">
          <div
            className="w-2 h-2 rounded-full"
            style={{ backgroundColor: activeColumn.color || '#6b7280' }}
          />
          <span className="font-medium text-[13px]">{activeColumn.name}</span>
          <Badge variant="secondary" className="text-[10px] h-5 px-1.5 ml-auto">
            {filteredTasksByColumn[activeColumn.id]?.length || 0}
          </Badge>
        </div>
      </div>
    )
  }

  return null
}

// Add column placeholder for board view
function AddColumnPlaceholder({
  onClick,
}: {
  readonly onClick: () => void
}) {
  const t = useTranslations()
    return (
    <button
      type="button"
      onClick={onClick}
      className="group flex-shrink-0 w-[280px] h-[120px] self-start rounded-[14px] border border-dashed border-border/60 hover:border-primary/40 hover:bg-muted/20 cursor-pointer transition-all flex items-center justify-center bg-transparent p-0"
    >
      <span className="flex items-center gap-2 text-muted-foreground group-hover:text-foreground transition-colors">
        <Plus className="h-3.5 w-3.5" />
        <span className="text-[13px] font-medium">{t("taskBoard.addColumn")}</span>
      </span>
    </button>
  )
}

// Common props for column rendering in both views
interface ColumnRenderProps {
  readonly columns: TaskColumn[]
  readonly filteredTasksByColumn: TasksByColumn
  readonly canEdit: boolean
  readonly activeTaskId: TaskId | null
  readonly viewMode: ViewMode
  readonly onEditColumn: (column: TaskColumn) => void
  readonly onDeleteColumn: (column: TaskColumn) => void
  readonly onCreateTask: (columnId: ColumnId) => void
  readonly onEditTask: (task: Task) => void
  readonly onDeleteTask: (task: Task) => void
}

// Renders columns for both views (reduces duplication)
function renderColumn(
  column: TaskColumn,
  props: Omit<ColumnRenderProps, 'columns'>
) {
  const { filteredTasksByColumn, canEdit, activeTaskId, viewMode, onEditColumn, onDeleteColumn, onCreateTask, onEditTask, onDeleteTask } = props
  return (
    <SortableColumn
      key={column.id}
      column={column}
      tasks={filteredTasksByColumn[column.id] || []}
      canEdit={canEdit}
      activeTaskId={activeTaskId}
      viewMode={viewMode}
      onEditColumn={onEditColumn}
      onDeleteColumn={onDeleteColumn}
      onCreateTask={onCreateTask}
      onEditTask={onEditTask}
      onDeleteTask={onDeleteTask}
    />
  )
}

// List view — compact collapsible sections that mirror the board's tile
// aesthetic (rounded 14px, muted tint, no hard card border). Each column
// renders as a subtle section divided by a hairline, plus a ghost-style
// "add column" footer that only appears when the board already has content.
function ListView({ columns, canEdit, onAddColumn, ...renderProps }: ColumnRenderProps & { readonly onAddColumn: () => void }) {
  const t = useTranslations()
  return (
    <div className="rounded-[14px] bg-muted/40 dark:bg-muted/25 overflow-hidden">
      {columns.length === 0 ? (
        <div className="p-8 text-center text-[12px] text-muted-foreground">
          {t("taskBoard.noColumnsInline")}
        </div>
      ) : (
        columns.map((column) => renderColumn(column, { canEdit, ...renderProps }))
      )}
      {canEdit && columns.length > 0 && (
        <button
          type="button"
          onClick={onAddColumn}
          className="w-full flex items-center justify-center gap-1.5 px-3 py-2.5 text-[12px] text-muted-foreground hover:text-foreground hover:bg-background/40 transition-colors border-t border-border/40"
        >
          <Plus className="h-3.5 w-3.5" />
          {t("taskBoard.addColumn")}
        </button>
      )}
    </div>
  )
}

/**
 * Track horizontal overflow on a scroll container so the caller can render
 * edge-fade indicators (left/right gradients) — signalling there's more
 * content without relying on an always-visible scrollbar.
 */
function useScrollShadow(): {
  ref: React.RefObject<HTMLDivElement | null>
  canScrollLeft: boolean
  canScrollRight: boolean
} {
  const ref = useRef<HTMLDivElement | null>(null)
  const [canScrollLeft, setCanScrollLeft] = useState(false)
  const [canScrollRight, setCanScrollRight] = useState(false)

  useEffect(() => {
    const el = ref.current
    if (!el) return

    const update = () => {
      const { scrollLeft, scrollWidth, clientWidth } = el
      setCanScrollLeft(scrollLeft > 2)
      setCanScrollRight(scrollLeft + clientWidth < scrollWidth - 2)
    }

    update()
    el.addEventListener("scroll", update, { passive: true })
    const ro = new ResizeObserver(update)
    ro.observe(el)
    // Observe children too — columns being added/removed changes scrollWidth.
    Array.from(el.children).forEach((child) => ro.observe(child))

    return () => {
      el.removeEventListener("scroll", update)
      ro.disconnect()
    }
  }, [])

  return { ref, canScrollLeft, canScrollRight }
}

// Board view - renders columns as kanban board.
//
// Design notes on scrolling:
//   * The outer wrapper owns the available viewport height so each column
//     stretches to fit and only its *internal* list scrolls — no "page scroll
//     inside page scroll".
//   * We hide the native scrollbar by default (`scrollbar-width: none` +
//     WebKit equivalents) and only reveal a subtle 2px overlay thumb when the
//     user is actually interacting (pointer is over the board). This matches
//     Linear / Notion's quieter aesthetic.
//   * A soft right-edge mask (`mask-image`) fades the last column into the
//     background when there's more content off-screen, signalling "scroll for
//     more" without a visible bar.
function BoardView({ columns, canEdit, onAddColumn, ...renderProps }: ColumnRenderProps & { readonly onAddColumn: () => void }) {
  const { ref, canScrollLeft, canScrollRight } = useScrollShadow()

  return (
    <div className="relative">
      <div
        ref={ref}
        className={cn(
          "task-board-scroll group/board relative flex gap-3.5 pb-2 items-stretch overflow-x-auto overflow-y-hidden",
          // Thin, always-rendered horizontal scrollbar — transparent by default
          // so it doesn't add visual noise, tinted on hover. Lets users drag
          // the bar if they don't have a scroll wheel, while the edge-fades
          // still act as the primary "more →" affordance.
          "[scrollbar-width:thin] [scrollbar-color:transparent_transparent]",
          "hover:[scrollbar-color:rgb(120_120_120/0.30)_transparent]",
          "[&::-webkit-scrollbar]:h-1.5",
          "[&::-webkit-scrollbar-track]:bg-transparent",
          "[&::-webkit-scrollbar-thumb]:bg-transparent",
          "[&::-webkit-scrollbar-thumb]:rounded-full",
          "[&::-webkit-scrollbar-thumb]:transition-colors",
          "hover:[&::-webkit-scrollbar-thumb]:bg-muted-foreground/25"
        )}
        style={{ height: "calc(100dvh - 18rem)", minHeight: "24rem" }}
      >
        {columns.map((column) => renderColumn(column, { canEdit, ...renderProps }))}
        {canEdit && <AddColumnPlaceholder onClick={onAddColumn} />}
      </div>

      {/* Edge-fade indicators — "there's more →" without a visible scrollbar.
          Pointer-events-none so they never eat clicks on the board itself. */}
      <div
        aria-hidden
        className={cn(
          "pointer-events-none absolute inset-y-0 left-0 w-8 bg-gradient-to-r from-background to-transparent transition-opacity duration-200",
          canScrollLeft ? "opacity-100" : "opacity-0"
        )}
      />
      <div
        aria-hidden
        className={cn(
          "pointer-events-none absolute inset-y-0 right-0 w-10 bg-gradient-to-l from-background to-transparent transition-opacity duration-200",
          canScrollRight ? "opacity-100" : "opacity-0"
        )}
      />
    </div>
  )
}

// Empty state component
function EmptyState({
  onAddColumn,
}: {
  readonly onAddColumn: () => void
}) {
  const t = useTranslations()
  return (
    <Card className="p-12">
      <div className="text-center space-y-3">
        <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center mx-auto">
          <Columns3 className="h-6 w-6 text-muted-foreground" />
        </div>
        <div>
          <h3 className="font-semibold">{t("taskBoard.noColumnsYet")}</h3>
          <p className="text-sm text-muted-foreground mt-1">
            {t("taskBoard.noColumnsDescription")}
          </p>
        </div>
        <Button onClick={onAddColumn}>
          <Plus className="h-4 w-4 mr-1.5" />
          {t("taskBoard.createColumn")}
        </Button>
      </div>
    </Card>
  )
}

// Hook for basic task board state (filters, view mode)
function useTaskBoardState(tasksByColumn: Record<string, Task[]>) {
  const t = useTranslations()
  const priorityConfig = getPriorityConfig(t)
  const [viewMode, setViewMode] = useState<ViewMode>("board")
  const [searchQuery, setSearchQuery] = useState("")
  const [priorityFilter, setPriorityFilter] = useState<string[]>([])
  const [tagFilter, setTagFilter] = useState<string[]>([])

  const tagLabels: Record<string, string> = useMemo(() => ({
    frontend: t("tasks.frontend"),
    backend: t("tasks.backend"),
    design: t("tasks.design"),
    devops: t("tasks.devops"),
    qa: t("tasks.qa"),
    sysadmin: t("tasks.sysadmin"),
  }), [t])

  const availableTags = useMemo(() => collectAvailableTags(tasksByColumn), [tasksByColumn])
  const filteredTasksByColumn = useTaskFiltering(tasksByColumn, searchQuery, priorityFilter, tagFilter)
  const totalTasks = useMemo(() => Object.values(tasksByColumn).flat().length, [tasksByColumn])
  const filteredTotalTasks = useMemo(() => Object.values(filteredTasksByColumn).flat().length, [filteredTasksByColumn])

  useKeyboardShortcuts()

  return {
    t,
    priorityConfig,
    viewMode,
    setViewMode,
    searchQuery,
    setSearchQuery,
    priorityFilter,
    setPriorityFilter,
    tagFilter,
    setTagFilter,
    tagLabels,
    availableTags,
    filteredTasksByColumn,
    totalTasks,
    filteredTotalTasks,
  }
}

// DnD hook options interface to reduce parameter count
interface TaskBoardDndOptions {
  readonly columns: TaskColumn[]
  readonly tasksByColumn: TasksByColumn
  readonly canEdit: boolean
  readonly onMoveTask: (taskId: TaskId, targetColumnId: ColumnId, positionInColumn?: number) => Promise<void>
  readonly onReorderTasks: (columnId: ColumnId, orderedTaskIds: TaskId[]) => Promise<void> | void
  readonly onReorderColumns: (columnIds: ColumnId[]) => Promise<void> | void
}

// Snappy drop animation for <DragOverlay/> — defaults feel sluggish (≈250ms with
// a heavy easing curve). This is visibly crisper and preserves the fade-out on
// the original card while the overlay returns.
const DROP_ANIMATION: DropAnimation = {
  duration: 120,
  easing: "cubic-bezier(0.22, 1, 0.36, 1)",
  sideEffects: defaultDropAnimationSideEffects({
    styles: {
      active: { opacity: "0" },
    },
  }),
}

// Helper to toggle body select-none class
const setBodySelectNone = (enabled: boolean) => {
  if (typeof document === "undefined") return
  document.body.classList[enabled ? "add" : "remove"]("select-none")
}

// Helper to find which column a task belongs to
const findTaskColumnId = (tasksByColumn: TasksByColumn, taskId: TaskId): ColumnId | null => {
  for (const [colId, tasks] of Object.entries(tasksByColumn)) {
    if (tasks.some(t => getTaskId(t) === taskId)) return colId
  }
  return null
}

// Helper to compute new column order after reorder
const computeNewColumnOrder = (columns: TaskColumn[], activeId: ColumnId, overId: ColumnId): ColumnId[] | null => {
  const currentOrder = columns.map((c) => c.id)
  const oldIndex = currentOrder.indexOf(activeId)
  const newIndex = currentOrder.indexOf(overId)
  if (oldIndex === -1 || newIndex === -1) return null
  return arrayMove(currentOrder, oldIndex, newIndex)
}

// Helper to find active task from all columns
const findActiveTask = (tasksByColumn: TasksByColumn, taskId: TaskId | null): Task | null => {
  if (!taskId) return null
  return Object.values(tasksByColumn).flat().find(t => getTaskId(t) === taskId) ?? null
}

// Helper to find active column
const findActiveColumn = (columns: TaskColumn[], columnId: ColumnId | null): TaskColumn | null => {
  if (!columnId) return null
  return columns.find((c) => c.id === columnId) ?? null
}

// Hook for drag state management
function useDragState() {
  const [activeTaskId, setActiveTaskId] = useState<TaskId | null>(null)
  const [activeColumnId, setActiveColumnId] = useState<ColumnId | null>(null)
  const [dragType, setDragType] = useState<DragType | null>(null)
  const [isMoving, setIsMoving] = useState(false)

  const resetDragState = useCallback(() => {
    setActiveTaskId(null)
    setActiveColumnId(null)
    setDragType(null)
    setBodySelectNone(false)
  }, [])

  const startDrag = useCallback((event: DragStartEvent) => {
    const type = event.active.data?.current?.type as DragType | undefined
    const id = event.active.id as string
    setDragType(type ?? null)
    if (type === "column") {
      setActiveColumnId(id)
    } else {
      setActiveTaskId(id)
    }
    setBodySelectNone(true)
  }, [])

  return { activeTaskId, activeColumnId, dragType, isMoving, setIsMoving, resetDragState, startDrag }
}

// Handler for column drag end
async function handleColumnDrag(
  activeId: ColumnId,
  overId: ColumnId,
  columns: TaskColumn[],
  onReorderColumns: (ids: ColumnId[]) => Promise<void> | void
): Promise<void> {
  if (activeId === overId) return
  const newOrder = computeNewColumnOrder(columns, activeId, overId)
  if (newOrder) await onReorderColumns(newOrder)
}

// Options for task drag handler
interface TaskDragOptions {
  readonly activeId: TaskId
  readonly overId: string
  readonly tasksByColumn: TasksByColumn
  readonly columnIds: ReadonlySet<ColumnId>
  readonly dragState: { isMoving: boolean; setIsMoving: (v: boolean) => void }
  readonly onMoveTask: (taskId: TaskId, columnId: ColumnId, positionInColumn?: number) => Promise<void>
  readonly onReorderTasks: (columnId: ColumnId, orderedTaskIds: TaskId[]) => Promise<void> | void
}

/**
 * Resolve where the task was dropped. `over.id` can be:
 *   - `tasks-<columnId>` → the empty column droppable
 *   - a raw column id    → column drop target
 *   - a task id          → another card inside a sortable column (reorder hint)
 */
function resolveTaskDropTarget(
  overId: string,
  tasksByColumn: TasksByColumn,
  columnIds: ReadonlySet<ColumnId>
): { targetColumnId: ColumnId; targetIndex: number } | null {
  if (overId.startsWith("tasks-")) {
    const columnId = overId.replace("tasks-", "")
    return { targetColumnId: columnId, targetIndex: tasksByColumn[columnId]?.length ?? 0 }
  }

  if (columnIds.has(overId)) {
    return { targetColumnId: overId, targetIndex: tasksByColumn[overId]?.length ?? 0 }
  }

  // overId is another task id — find its column & index.
  const columnOfOver = findTaskColumnId(tasksByColumn, overId)
  if (!columnOfOver) return null
  const index = tasksByColumn[columnOfOver].findIndex((t) => getTaskId(t) === overId)
  return { targetColumnId: columnOfOver, targetIndex: index < 0 ? tasksByColumn[columnOfOver].length : index }
}

// Handler for task drag end — supports reorder within a column as well as
// cross-column moves with insertion position.
async function handleTaskDrag(options: TaskDragOptions): Promise<void> {
  const { activeId, overId, tasksByColumn, columnIds, dragState, onMoveTask, onReorderTasks } = options
  if (dragState.isMoving) return

  const sourceColumnId = findTaskColumnId(tasksByColumn, activeId)
  if (!sourceColumnId) return

  const target = resolveTaskDropTarget(overId, tasksByColumn, columnIds)
  if (!target) return
  const { targetColumnId, targetIndex } = target

  if (sourceColumnId === targetColumnId) {
    // Same-column reorder — nothing to do if position unchanged.
    const columnTasks = tasksByColumn[sourceColumnId]
    const currentIds = columnTasks.map(getTaskId)
    const oldIndex = currentIds.indexOf(activeId)
    if (oldIndex === -1 || oldIndex === targetIndex) return

    const newOrder = arrayMove(currentIds, oldIndex, targetIndex)
    dragState.setIsMoving(true)
    try {
      await onReorderTasks(sourceColumnId, newOrder)
    } finally {
      dragState.setIsMoving(false)
    }
    return
  }

  // Cross-column move with explicit insertion position.
  dragState.setIsMoving(true)
  try {
    await onMoveTask(activeId, targetColumnId, targetIndex)
  } finally {
    dragState.setIsMoving(false)
  }
}

// Hook for drag-and-drop logic
function useTaskBoardDnd(options: TaskBoardDndOptions) {
  const { columns, tasksByColumn, canEdit, onMoveTask, onReorderTasks, onReorderColumns } = options
  const dragState = useDragState()

  const sensors = useSensors(
    // `distance: 3` makes the drag pick up near-instantly once the user starts
    // moving without swallowing clicks (anything < 3 px is treated as a click).
    useSensor(PointerSensor, { activationConstraint: { distance: 3 } })
  )

  const columnIds = useMemo(() => new Set(columns.map((c) => c.id)), [columns])

  const handleDragEnd = useCallback(async (event: DragEndEvent) => {
    const { active, over } = event
    if (!over || !canEdit) {
      dragState.resetDragState()
      return
    }

    const activeId = active.id as string
    const overId = over.id as string

    if (dragState.dragType === "column") {
      await handleColumnDrag(activeId, overId, columns, onReorderColumns)
    } else if (dragState.dragType === "task") {
      await handleTaskDrag({ activeId, overId, tasksByColumn, columnIds, dragState, onMoveTask, onReorderTasks })
    }

    dragState.resetDragState()
  }, [canEdit, columns, columnIds, dragState, onMoveTask, onReorderTasks, onReorderColumns, tasksByColumn])

  const collisionDetection = useCallback((args: Parameters<typeof closestCorners>[0]) => {
    if (dragState.dragType !== "column") return closestCorners(args)
    const columnIds = new Set<string>(columns.map((c) => c.id))
    const filtered = args.droppableContainers.filter((c) => columnIds.has(String(c.id)))
    return closestCenter({ ...args, droppableContainers: filtered })
  }, [dragState.dragType, columns])

  const activeTask = useMemo(
    () => findActiveTask(tasksByColumn, dragState.activeTaskId),
    [tasksByColumn, dragState.activeTaskId]
  )
  const activeColumn = useMemo(
    () => findActiveColumn(columns, dragState.activeColumnId),
    [columns, dragState.activeColumnId]
  )

  return {
    sensors,
    activeTaskId: dragState.activeTaskId,
    activeTask,
    activeColumn,
    dragType: dragState.dragType,
    handleDragStart: dragState.startDrag,
    handleDragEnd,
    collisionDetection,
  }
}

// Main Component
export function ProjectTaskBoard({
  columns,
  tasksByColumn,
  isLoading,
  canEdit,
  onAddColumn,
  onEditColumn,
  onDeleteColumn,
  onCreateTask,
  onEditTask,
  onDeleteTask,
  onMoveTask,
  onReorderTasks,
  onReorderColumns,
  headerStart,
}: Readonly<ProjectTaskBoardProps>) {
  const state = useTaskBoardState(tasksByColumn)
  const dnd = useTaskBoardDnd({ columns, tasksByColumn, canEdit, onMoveTask, onReorderTasks, onReorderColumns })

  if (isLoading) return <TaskBoardSkeleton />

  const columnRenderProps = {
    columns,
    filteredTasksByColumn: state.filteredTasksByColumn,
    canEdit,
    activeTaskId: dnd.activeTaskId,
    viewMode: state.viewMode,
    onEditColumn,
    onDeleteColumn,
    onCreateTask,
    onEditTask,
    onDeleteTask,
  }

  return (
    <div className="space-y-3">
      <TaskBoardToolbar
        searchQuery={state.searchQuery}
        setSearchQuery={state.setSearchQuery}
        priorityFilter={state.priorityFilter}
        setPriorityFilter={state.setPriorityFilter}
        tagFilter={state.tagFilter}
        setTagFilter={state.setTagFilter}
        availableTags={state.availableTags}
        tagLabels={state.tagLabels}
        priorityConfig={state.priorityConfig}
        filteredTotalTasks={state.filteredTotalTasks}
        totalTasks={state.totalTasks}
        viewMode={state.viewMode}
        setViewMode={state.setViewMode}
        headerStart={headerStart}
        t={state.t}
      />

      <DndContext
        sensors={dnd.sensors}
        collisionDetection={dnd.collisionDetection}
        onDragStart={dnd.handleDragStart}
        onDragEnd={dnd.handleDragEnd}
      >
        <SortableContext items={columns.map((c) => c.id)} strategy={horizontalListSortingStrategy}>
          {state.viewMode === "list" ? (
            <ListView {...columnRenderProps} onAddColumn={onAddColumn} />
          ) : (
            <BoardView {...columnRenderProps} onAddColumn={onAddColumn} />
          )}
        </SortableContext>

        <DragOverlay dropAnimation={DROP_ANIMATION}>
          <DragOverlayContent
            dragType={dnd.dragType}
            activeTask={dnd.activeTask}
            activeColumn={dnd.activeColumn}
            filteredTasksByColumn={state.filteredTasksByColumn}
            priorityConfig={state.priorityConfig}
          />
        </DragOverlay>
      </DndContext>

      {columns.length === 0 && canEdit && state.viewMode !== "list" && (
        <EmptyState onAddColumn={onAddColumn} />
      )}
    </div>
  )
}
