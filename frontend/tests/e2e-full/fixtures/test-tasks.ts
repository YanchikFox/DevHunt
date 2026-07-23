/**
 * Test task fixtures for E2E tests
 */

export interface TaskData {
  title: string
  description: string
  priority: "low" | "medium" | "high" | "urgent"
}

/**
 * Generate unique task data
 */
export function generateTaskData(options?: {
  prefix?: string
  priority?: "low" | "medium" | "high" | "urgent"
}): TaskData {
  const timestamp = Date.now()
  const prefix = options?.prefix ?? "E2E Task"

  return {
    title: `${prefix} ${timestamp}`,
    description: `E2E test task created at ${new Date().toISOString()}`,
    priority: options?.priority ?? "medium",
  }
}

/**
 * Task status enum
 */
export const TASK_STATUSES = {
  todo: "todo",
  doing: "doing",
  review: "review",
  done: "done",
  archived: "archived",
  cancelled: "cancelled",
} as const

export type TaskStatus = (typeof TASK_STATUSES)[keyof typeof TASK_STATUSES]

/**
 * Task priority enum
 */
export const TASK_PRIORITIES = {
  low: "low",
  medium: "medium",
  high: "high",
  urgent: "urgent",
} as const

export type TaskPriority = (typeof TASK_PRIORITIES)[keyof typeof TASK_PRIORITIES]

/**
 * Valid task status transitions
 */
export const TASK_STATUS_TRANSITIONS = [
  { from: "todo", to: "doing" },
  { from: "doing", to: "review" },
  { from: "review", to: "done" },
  { from: "review", to: "doing" }, // Back to doing if changes needed
  { from: "doing", to: "todo" }, // Back to backlog
] as const
