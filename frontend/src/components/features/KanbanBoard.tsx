"use client"

import { useState, useCallback } from "react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { Plus, GitBranch, User, Clock, GitCommit } from "lucide-react"
import { cn } from "@/lib/utils"

/**
 * Represents a task in the Kanban board.
 */
export interface Task {
  id: string
  title: string
  description: string
  assignee?: string
  status: "todo" | "in-progress" | "review" | "done"
  priority: "low" | "medium" | "high"
  relatedCommits?: Array<{ hash: string; branch: string }>
  createdAt: string
}

/**
 * Props for the KanbanBoard component.
 */
export interface KanbanBoardProps {
  /** List of tasks to display on the board */
  tasks?: Task[]
}

const mockTasks: Task[] = [
  {
    id: "1",
    title: "Implement user authentication",
    description: "Add JWT-based auth with GitHub OAuth integration",
    assignee: "Alex Johnson",
    status: "done",
    priority: "high",
    relatedCommits: [{ hash: "c3d4e5f6a1b2", branch: "main" }],
    createdAt: new Date(Date.now() - 96 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "2",
    title: "Add dark mode toggle",
    description: "Implement theme switcher with persistence",
    assignee: "Emma Davis",
    status: "in-progress",
    priority: "medium",
    relatedCommits: [
      { hash: "e5f6a1b2c3d4", branch: "feature/dark-mode" },
      { hash: "g7h8i9j0k1l2", branch: "feature/dark-mode" },
    ],
    createdAt: new Date(Date.now() - 72 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "3",
    title: "Push notifications setup",
    description: "Configure WebSocket for real-time notifications",
    assignee: "Alex Johnson",
    status: "review",
    priority: "medium",
    relatedCommits: [
      { hash: "f6a1b2c3d4e5", branch: "feature/notifications" },
      { hash: "j0k1l2m3n4o5", branch: "feature/notifications" },
    ],
    createdAt: new Date(Date.now() - 68 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "4",
    title: "Security vulnerability fix",
    description: "Patch critical authentication bypass",
    assignee: "Sarah Chen",
    status: "done",
    priority: "high",
    relatedCommits: [{ hash: "m3n4o5p6q7r8", branch: "hotfix/security" }],
    createdAt: new Date(Date.now() - 40 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "5",
    title: "Add analytics dashboard",
    description: "Create project analytics and metrics visualization",
    assignee: "Emma Davis",
    status: "todo",
    priority: "low",
    createdAt: new Date(Date.now() - 20 * 60 * 60 * 1000).toISOString(),
  },
  {
    id: "6",
    title: "API rate limiting",
    description: "Implement request throttling to prevent abuse",
    assignee: "Alex Johnson",
    status: "in-progress",
    priority: "medium",
    relatedCommits: [{ hash: "w3x4y5z6a1b2", branch: "main" }],
    createdAt: new Date(Date.now() - 16 * 60 * 60 * 1000).toISOString(),
  },
]

const statusConfig = {
  todo: {
    label: "To Do",
    color: "bg-muted-foreground",
    textColor: "text-muted-foreground",
    bgColor: "bg-muted/50",
  },
  "in-progress": {
    label: "In Progress",
    color: "bg-blue-500",
    textColor: "text-blue-600",
    bgColor: "bg-blue-50 dark:bg-blue-900/20",
  },
  review: {
    label: "Review",
    color: "bg-purple-500",
    textColor: "text-purple-600",
    bgColor: "bg-purple-50 dark:bg-purple-900/20",
  },
  done: {
    label: "Done",
    color: "bg-green-500",
    textColor: "text-green-600",
    bgColor: "bg-green-50 dark:bg-green-900/20",
  },
}

const priorityConfig = {
  low: { label: "Low", color: "bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400" },
  medium: {
    label: "Medium",
    color: "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/20 dark:text-yellow-400",
  },
  high: { label: "High", color: "bg-red-100 text-red-700 dark:bg-red-900/20 dark:text-red-400" },
}

/**
 * Displays a Kanban board for project task management.
 * Supports drag-and-drop (conceptually) and task status visualization.
 *
 * @example
 * ```tsx
 * <KanbanBoard tasks={[...]} />
 * ```
 */
export function KanbanBoard({ tasks = mockTasks }: KanbanBoardProps) {
  const [selectedTask, setSelectedTask] = useState<string | null>(null)

  const tasksByStatus = {
    todo: tasks.filter((t) => t.status === "todo"),
    "in-progress": tasks.filter((t) => t.status === "in-progress"),
    review: tasks.filter((t) => t.status === "review"),
    done: tasks.filter((t) => t.status === "done"),
  }

  const handleTaskSelect = useCallback((taskId: string) => {
    setSelectedTask((prev) => (prev === taskId ? null : taskId))
  }, [])

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold">Project Kanban Board</h2>
          <p className="text-muted-foreground">Track tasks and their progress</p>
        </div>
        <Button>
          <Plus className="h-4 w-4 mr-2" />
          New Task
        </Button>
      </div>

      {/* Kanban Columns */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {Object.entries(statusConfig).map(([status, config]) => (
          <Card key={status} className="border-2">
            <CardHeader className={cn("pb-3", config.bgColor)}>
              <CardTitle className="text-sm flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <div className={cn("w-2 h-2 rounded-full", config.color)} />
                  <span>{config.label}</span>
                </div>
                <Badge variant="outline" className="border-current">
                  {tasksByStatus[status as keyof typeof tasksByStatus].length}
                </Badge>
              </CardTitle>
            </CardHeader>
            <CardContent className="p-3 space-y-2 max-h-[600px] overflow-y-auto">
              {tasksByStatus[status as keyof typeof tasksByStatus].map((task) => (
                <KanbanTaskCard
                  key={task.id}
                  task={task}
                  isSelected={selectedTask === task.id}
                  onSelect={handleTaskSelect}
                />
              ))}

              {/* Empty State */}
              {tasksByStatus[status as keyof typeof tasksByStatus].length === 0 && (
                <div className="text-center py-8 text-sm text-muted-foreground">No tasks</div>
              )}
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  )
}

interface KanbanTaskCardProps {
  task: Task
  isSelected: boolean
  onSelect: (taskId: string) => void
}

function KanbanTaskCard({ task, isSelected, onSelect }: KanbanTaskCardProps) {
  const handleClick = useCallback(() => {
    onSelect(task.id)
  }, [onSelect, task.id])

  return (
    <Card
      className={cn(
        "border-2 cursor-pointer transition-all hover:border-primary/50 hover:shadow-md group",
        isSelected && "border-primary bg-primary/5"
      )}
      onClick={handleClick}
    >
      <CardContent className="p-4 space-y-3">
        {/* Task Header */}
        <div>
          <h4 className="font-semibold text-sm mb-1 group-hover:text-primary transition-colors line-clamp-2">
            {task.title}
          </h4>
          <p className="text-xs text-muted-foreground line-clamp-2">{task.description}</p>
        </div>

        {/* Priority Badge */}
        <Badge className={cn("text-xs", priorityConfig[task.priority].color)}>
          {priorityConfig[task.priority].label}
        </Badge>

        {/* Related Commits */}
        {task.relatedCommits && task.relatedCommits.length > 0 && (
          <div className="space-y-1 pt-2 border-t">
            <p className="text-xs font-medium text-muted-foreground">Linked Commits</p>
            {task.relatedCommits.map((commit) => (
              <div
                key={commit.hash}
                className="flex items-center gap-2 text-xs font-mono bg-muted/30 rounded px-2 py-1"
              >
                <GitCommit className="h-3 w-3 text-primary" />
                <span className="text-blue-600 dark:text-blue-400">{commit.hash.slice(0, 7)}</span>
                <GitBranch className="h-3 w-3 text-muted-foreground ml-auto" />
                <span className="text-muted-foreground">{commit.branch}</span>
              </div>
            ))}
          </div>
        )}

        {/* Task Footer */}
        <div className="flex items-center justify-between pt-2 border-t">
          {task.assignee && (
            <div className="flex items-center gap-1.5">
              <User className="h-3 w-3 text-muted-foreground" />
              <span className="text-xs font-medium truncate">{task.assignee}</span>
            </div>
          )}
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <Clock className="h-3 w-3" />
            {new Date(task.createdAt).toLocaleDateString()}
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
