/** Zod schemas and TypeScript types for the DevHunt API. */
import { z } from "zod"
import { routing } from "@/i18n/routing"

// ============================================================================
// User Schemas
// ============================================================================

/**
 * Available user roles in the system.
 * - `participant` - Regular user who can join projects
 * - `company` - Company account that can post internships
 * - `curator` - Mentor who can guide projects
 * - `admin` - System administrator
 */
export const UserRoleSchema = z.enum(["participant", "company", "curator", "admin", "superadmin"])

/** User role type derived from UserRoleSchema */
export type UserRole = z.infer<typeof UserRoleSchema>

/**
 * User schema for validation.
 * Represents a registered user in the DevHunt platform.
 */
export const UserSchema = z.object({
  /** Unique user identifier (UUID) */
  id: z.string(),
  /** User's email address */
  email: z.string().email(),
  /** Display name */
  name: z.string(),
  /** User's role in the system */
  role: UserRoleSchema,
  /** URL to user's avatar image */
  avatar: z.string().url().optional(),
  /** User's biography/description */
  bio: z.string().optional(),
  /** List of user's skills (e.g., ["React", "TypeScript", "Node.js"]) */
  skills: z.array(z.string()),
  /** Experience level for filtering and matching */
  experienceLevel: z.enum(["junior", "middle", "senior"]).optional(),
  /** User's timezone (e.g., "Europe/Moscow") */
  timezone: z.string().optional(),
  /** Preferred UI language */
  language: z.enum([...routing.locales] as [string, ...string[]]).default(routing.defaultLocale),
  /** Account creation timestamp (ISO 8601) */
  createdAt: z.string(),
  /** Last profile update timestamp (ISO 8601) */
  updatedAt: z.string(),
})

/** User type derived from UserSchema */
export type User = z.infer<typeof UserSchema>

// ============================================================================
// Project Schemas
// ============================================================================

/**
 * Project lifecycle statuses.
 * - `draft` - Initial state, not visible to others
 * - `active` - Project is active and visible
 * - `recruiting` - Actively looking for team members
 * - `in_progress` - Development is underway
 * - `completed` - Project finished successfully
 * - `cancelled` - Project was cancelled
 * - `archived` - Project is archived (read-only)
 */
export const ProjectStatusSchema = z.enum([
  "draft",
  "active",
  "recruiting",
  "in_progress",
  "completed",
  "cancelled",
  "archived",
])

/** Project status type */
export type ProjectStatus = z.infer<typeof ProjectStatusSchema>

/**
 * Project visibility levels.
 * - `public` - Visible to everyone
 * - `members` - Visible only to team members
 * - `subscribers` - Visible to subscribers and members
 * - `private` - Visible only to owner
 */
export const ProjectVisibilitySchema = z.enum(["public", "members", "subscribers", "private"])

/** Project visibility type */
export type ProjectVisibility = z.infer<typeof ProjectVisibilitySchema>

/**
 * Project schema for validation.
 * Represents a collaborative project in the DevHunt platform.
 */
export const ProjectSchema = z.object({
  /** Unique project identifier (UUID) */
  id: z.string(),
  /** URL-safe handle (e.g. "helix" in /projects/helix). Optional until user sets one. */
  slug: z.string().nullable().optional(),
  /** Project title (displayed in lists and headers) */
  title: z.string(),
  /** Short description for project cards */
  description: z.string(),
  /** Full project description with markdown support */
  detailedDescription: z.string().optional(),
  /** List of project goals/milestones */
  goals: z.array(z.string()).optional(),
  /** Tech stack used in the project (e.g., ["React", "Node.js", "PostgreSQL"]) */
  technologies: z.array(z.string()),
  /** Roles needed for the project (e.g., ["Frontend Developer", "Designer"]) */
  requiredRoles: z.array(z.string()),
  /** Structured open roles with hours/equity metadata */
  openRoles: z.array(z.object({
    role: z.string(),
    totalNeeded: z.number(),
    hoursPerWeek: z.number().nullable().optional(),
    equityOptional: z.boolean(),
  })).optional(),
  /** Current project lifecycle status */
  status: ProjectStatusSchema,
  /** Who can view this project */
  visibility: ProjectVisibilitySchema,
  /** Default visibility for project news posts */
  defaultNewsVisibility: z.string().optional(),
  /** Default visibility for project files */
  defaultFilesVisibility: z.string().optional(),
  /** User ID of the project owner */
  ownerId: z.string(),
  /** Populated owner user object (optional, depends on API response) */
  owner: UserSchema.optional(),
  /** Current number of team members */
  teamSize: z.number().optional(),
  /** Maximum allowed team size (null = unlimited) */
  maxTeamSize: z.number().nullable().optional(),
  /** Whether project is featured on homepage */
  featured: z.boolean().optional(),
  /** Average project rating (1-5, null if no ratings) */
  rating: z.number().nullable().optional(),
  /** Whether project is published to public showcase */
  showcasePublished: z.boolean().optional(),
  /** Expected project duration (e.g., "3 months") */
  duration: z.string().optional(),
  /** Project complexity for filtering */
  complexityLevel: z.enum(["low", "medium", "high"]).optional(),
  /** Project timezone for scheduling */
  timezone: z.string().optional(),
  /** Total boosts received (GitHub-star-style count). */
  boostsCount: z.number().optional(),
  /** Whether the current viewer has boosted this project. */
  boostedByMe: z.boolean().optional(),
  /** Number of unfilled role seats across the project's open roles. */
  openRolesCount: z.number().optional(),
  /** Project creation timestamp (ISO 8601) */
  createdAt: z.string(),
  /** Last update timestamp (ISO 8601) */
  updatedAt: z.string(),
})

/** Project type derived from ProjectSchema */
export type Project = z.infer<typeof ProjectSchema>

// ============================================================================
// Team Schemas
// ============================================================================

/**
 * Team member roles within a project.
 * Determines permissions and responsibilities.
 */
export const TeamMemberRoleSchema = z.enum([
  "owner",
  "lead",
  "developer",
  "designer",
  "devops",
  "qa",
])

/** Team member role type */
export type TeamMemberRole = z.infer<typeof TeamMemberRoleSchema>

/**
 * Team member schema.
 * Represents a user's membership in a project team.
 */
export const TeamMemberSchema = z.object({
  /** Unique membership identifier */
  id: z.string(),
  /** User ID of the team member */
  userId: z.string(),
  /** Project ID this membership belongs to */
  projectId: z.string(),
  /** Member's role in the project */
  role: TeamMemberRoleSchema,
  /** Populated user object (optional) */
  user: UserSchema.optional(),
  /** When the member joined the team (ISO 8601) */
  joinedAt: z.string(),
  /** Permission: can publish news to project feed */
  canPublishNews: z.boolean().optional(),
  /** Permission: can create/edit/delete tasks */
  canManageTasks: z.boolean().optional(),
  /** Permission: can upload/delete project files */
  canManageFiles: z.boolean().optional(),
  /** Permission: can manage project gallery images */
  canManageGallery: z.boolean().optional(),
})

/** Team member type */
export type TeamMember = z.infer<typeof TeamMemberSchema>

// ============================================================================
// Task Schemas
// ============================================================================

/**
 * Task status values for Kanban board.
 * - `todo` - Not started
 * - `doing` - In progress
 * - `review` - Awaiting review
 * - `done` - Completed
 * - `archived` - Archived (hidden from board)
 * - `cancelled` - Cancelled
 * - `in_progress` - Legacy status (deprecated, use `doing`)
 */
export const TaskStatusSchema = z.enum([
  "todo",
  "doing",
  "review",
  "done",
  "archived",
  "cancelled",
  "in_progress",
])
/** Task status type */
export type TaskStatus = z.infer<typeof TaskStatusSchema>

/**
 * Task priority levels.
 * Affects visual styling and sorting.
 */
export const TaskPrioritySchema = z.enum(["low", "medium", "high", "urgent"])
/** Task priority type */
export type TaskPriority = z.infer<typeof TaskPrioritySchema>

/**
 * Task schema.
 * Represents a work item in a project's task board.
 */
export const TaskSchema = z.object({
  /** Unique task identifier */
  id: z.string(),
  /** Parent project ID */
  projectId: z.string(),
  /** Task title */
  title: z.string(),
  /** Task description (supports markdown) */
  description: z.string().optional(),
  /** Current task status */
  status: TaskStatusSchema,
  /** Task priority level */
  priority: TaskPrioritySchema,
  /** Assigned user ID (null = unassigned) */
  assigneeId: z.string().optional(),
  /** Assignee display name (denormalized for performance) */
  assigneeName: z.string().optional(),
  /** Assignee avatar URL (denormalized) */
  assigneeAvatarUrl: z.string().optional(),
  /** Full assignee user object (optional) */
  assignee: UserSchema.optional(),
  /** Task due date (ISO 8601) */
  dueDate: z.string().optional(),
  /** Estimated hours to complete */
  estimatedHours: z.number().optional(),
  /** Actual hours spent */
  actualHours: z.number().optional(),
  /** Custom column ID (for custom Kanban columns) */
  columnId: z.string().optional(),
  /** Position within column for ordering */
  positionInColumn: z.number().default(0),
  /** Number of linked tasks */
  linkCount: z.number().default(0),
  /** Number of file attachments */
  attachmentCount: z.number().default(0),
  /** Comma-separated tags (e.g., "frontend,backend") */
  tags: z.string().optional(),
  /** GitHub Issue ID (for synced tasks) */
  gitHubIssueId: z.number().optional(),
  /** GitHub Issue number (e.g., #123) */
  gitHubIssueNumber: z.number().optional(),
  /** GitHub Issue URL (direct link to issue) */
  gitHubIssueUrl: z.string().optional(),
  /** Task creation timestamp (ISO 8601) */
  createdAt: z.string(),
  /** Last update timestamp (ISO 8601) */
  updatedAt: z.string(),
})

/** Task type */
export type Task = z.infer<typeof TaskSchema>

// Internship schemas
export const InternshipTypeSchema = z.enum(["challenge", "hackathon", "internship", "course"])
export type InternshipType = z.infer<typeof InternshipTypeSchema>

export const InternshipSchema = z.object({
  id: z.string(),
  title: z.string(),
  description: z.string(),
  type: InternshipTypeSchema,
  companyId: z.string().optional(),
  company: UserSchema.optional(),
  requirements: z.array(z.string()),
  duration: z.string(),
  benefits: z.array(z.string()).optional(),
  isRemote: z.boolean().default(false),
  location: z.string().optional(),
  deadline: z.string(),
  maxParticipants: z.number().optional(),
  createdAt: z.string(),
})

export type Internship = z.infer<typeof InternshipSchema>

// Re-export invitation, notification, and auth schemas/types
export {
  InvitationStatusSchema,
  InvitationTypeSchema,
  InvitationSchema,
  NotificationTypeSchema,
  NotificationSchema,
  LoginRequestSchema,
  RegisterRequestSchema,
  RegisterResponseSchema,
  AuthResponseSchema,
  type InvitationStatus,
  type InvitationType,
  type Invitation,
  type NotificationType,
  type Notification,
  type LoginRequest,
  type RegisterRequest,
  type RegisterResponse,
  type AuthResponse,
} from "./schema-misc"

// Re-export task board types
export {
  TaskColumnSchema,
  TaskLinkTypeSchema,
  TaskLinkSchema,
  TaskAttachmentSchema,
  TaskBoardViewModeSchema,
  TaskBoardSettingsSchema,
  type TaskColumn,
  type TaskLinkType,
  type TaskLink,
  type TaskAttachment,
  type TaskBoardViewMode,
  type TaskBoardSettings,
} from "./task-board-types"

export {
  AiTechStackOptionSchema,
  AiPlanTaskSchema,
  AiPlanPhaseSchema,
  AiPlanDraftSchema,
  AiPlanResponseSchema,
  AiPlanApplyResponseSchema,
  type AiTechStackOption,
  type AiPlanTask,
  type AiPlanPhase,
  type AiPlanDraft,
  type AiPlanResponse,
  type AiPlanApplyResponse,
} from "./ai-types"
