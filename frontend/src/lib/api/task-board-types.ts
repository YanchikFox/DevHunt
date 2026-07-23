import { z } from "zod"

// Task Column schemas
export const TaskColumnSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  name: z.string(),
  position: z.number(),
  color: z.string().optional(),
  isDefault: z.boolean().optional(),
  isCompleted: z.boolean().optional(),
  wipLimit: z.number().optional(),
  canvasX: z.number().optional(),
  canvasY: z.number().optional(),
  canvasWidth: z.number().optional(),
  canvasHeight: z.number().optional(),
  taskCount: z.number().default(0),
})

export type TaskColumn = z.infer<typeof TaskColumnSchema>

// Task Link schemas
export const TaskLinkTypeSchema = z.enum([
  "blocks",
  "blocked_by",
  "depends_on",
  "related_to",
  "duplicate_of",
  "parent_of",
  "child_of",
])
export type TaskLinkType = z.infer<typeof TaskLinkTypeSchema>

export const TaskLinkSchema = z.object({
  id: z.string(),
  sourceTaskId: z.string(),
  sourceTaskTitle: z.string(),
  targetTaskId: z.string(),
  targetTaskTitle: z.string(),
  linkType: TaskLinkTypeSchema,
  createdByUserId: z.string(),
  createdAt: z.string(),
})

export type TaskLink = z.infer<typeof TaskLinkSchema>

// Task Attachment schemas
export const TaskAttachmentSchema = z.object({
  id: z.string(),
  taskId: z.string(),
  projectFileId: z.string().optional(),
  fileName: z.string(),
  contentType: z.string().optional(),
  fileSize: z.number().optional(),
  attachedByUserId: z.string(),
  attachedByUserName: z.string().optional(),
  attachedAt: z.string(),
})

export type TaskAttachment = z.infer<typeof TaskAttachmentSchema>

// Task Board Settings schemas
export const TaskBoardViewModeSchema = z.enum(["board", "canvas"])
export type TaskBoardViewMode = z.infer<typeof TaskBoardViewModeSchema>

export const TaskBoardSettingsSchema = z.object({
  id: z.string(),
  projectId: z.string(),
  viewMode: TaskBoardViewModeSchema,
  canvasZoom: z.number().default(1),
  canvasPanX: z.number().default(0),
  canvasPanY: z.number().default(0),
  showCompletedTasks: z.boolean().default(true),
  defaultColumnId: z.string().optional(),
})

export type TaskBoardSettings = z.infer<typeof TaskBoardSettingsSchema>
