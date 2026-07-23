/**
 * Test project fixtures for E2E tests
 */

export interface ProjectData {
  title: string
  description: string
  technologies: string[]
  roles: string[]
  visibility: "public" | "private"
}

/**
 * Generate unique project data with timestamp
 */
export function generateProjectData(options?: {
  prefix?: string
  technologies?: string[]
  roles?: string[]
  visibility?: "public" | "private"
}): ProjectData {
  const timestamp = Date.now()
  const prefix = options?.prefix ?? "E2E Project"

  return {
    title: `${prefix} ${timestamp}`,
    description: `E2E test project created by Playwright at ${new Date().toISOString()}. This description meets the minimum length requirement.`,
    technologies: options?.technologies ?? ["React", "TypeScript"],
    roles: options?.roles ?? ["Backend Developer"],
    visibility: options?.visibility ?? "public",
  }
}

/**
 * Project status enum matching backend
 */
export const PROJECT_STATUSES = {
  draft: "draft",
  recruiting: "recruiting",
  active: "active",
  completed: "completed",
  archived: "archived",
  cancelled: "cancelled",
} as const

export type ProjectStatus = (typeof PROJECT_STATUSES)[keyof typeof PROJECT_STATUSES]

/**
 * Valid status transitions for lifecycle tests
 */
export const STATUS_TRANSITIONS = [
  { from: "draft", action: "publish", to: "recruiting" },
  { from: "recruiting", action: "activate", to: "active" },
  { from: "active", action: "complete", to: "completed" },
  { from: "draft", action: "archive", to: "archived" },
  { from: "recruiting", action: "archive", to: "archived" },
  { from: "active", action: "archive", to: "archived" },
] as const
