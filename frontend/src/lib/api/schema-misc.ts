import { z } from "zod"

// Invitation Schemas

/** Invitation status values */
export const InvitationStatusSchema = z.enum(["pending", "accepted", "declined", "cancelled"])
/** Invitation status type */
export type InvitationStatus = z.infer<typeof InvitationStatusSchema>

/**
 * Invitation type.
 * - `invite` - Owner/lead invites a user to join
 * - `request` - User requests to join the project
 */
export const InvitationTypeSchema = z.enum(["invite", "request"])
/** Invitation type */
export type InvitationType = z.infer<typeof InvitationTypeSchema>

/**
 * Invitation schema.
 * Represents a pending invitation or join request for a project.
 */
export const InvitationSchema = z.object({
  /** Unique invitation identifier */
  id: z.string(),
  /** Target project ID */
  projectId: z.string(),
  /** User being invited (for invites) or requesting (for requests) */
  inviteeId: z.string(),
  /** User who sent the invitation */
  inviterId: z.string(),
  /** Type of invitation */
  type: InvitationTypeSchema,
  /** Proposed role for the invitee */
  role: z.string(),
  /** Optional message from inviter */
  message: z.string().optional(),
  /** Current invitation status */
  status: InvitationStatusSchema,
  /** When invitation was created (ISO 8601) */
  createdAt: z.string(),
})

/** Invitation type */
export type Invitation = z.infer<typeof InvitationSchema>

// Notification Schemas

export const NotificationTypeSchema = z.enum([
  "invitation",
  "taskAssigned",
  "deadline",
  "chatMessage",
  "projectStatusChanged",
  "applicationAccepted",
  "applicationRejected",
  "follow",
  "achievement",
  "moderation",
  "team",
  "admin",
  "general",
])

export type NotificationType = z.infer<typeof NotificationTypeSchema>

export const NotificationSchema = z.object({
  id: z.string(),
  userId: z.string(),
  type: NotificationTypeSchema,
  title: z.string(),
  message: z.string(),
  read: z.boolean().default(false),
  metadata: z.record(z.string(), z.unknown()).optional(),
  createdAt: z.string(),
})

export type Notification = z.infer<typeof NotificationSchema>

// Auth Schemas

const PasswordSchema = z
  .string()
  .min(8, "Password must be at least 8 characters")
  .regex(
    /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/,
    "Password must include upper, lower, number and special character"
  )

export const LoginRequestSchema = z.object({
  email: z.string().email(),
  password: PasswordSchema,
  totpCode: z.string().length(6).optional(),
  recoveryCode: z.string().optional(),
})

export type LoginRequest = z.infer<typeof LoginRequestSchema>

export const RegisterRequestSchema = z.object({
  email: z.string().email(),
  password: PasswordSchema,
  fullName: z.string().min(2, "Full name must be at least 2 characters"),
  username: z
    .string()
    .regex(/^[a-zA-Z0-9_\-.]+$/, "Only letters, digits, underscore, hyphen, dot")
    .min(3, "At least 3 characters")
    .max(50)
    .optional()
    .or(z.literal("")),
})

export type RegisterRequest = z.infer<typeof RegisterRequestSchema>

export const RegisterResponseSchema = z.object({
  message: z.string(),
  userId: z.string(),
  autoVerified: z.boolean().optional(),
})

export type RegisterResponse = z.infer<typeof RegisterResponseSchema>

export const AuthResponseSchema = z.object({
  accessToken: z.string(),
  refreshToken: z.string(),
  email: z.string().email(),
  userId: z.string(),
})

export type AuthResponse = z.infer<typeof AuthResponseSchema>
