/**
 * Mappers to convert backend DTOs to frontend schemas
 */

import type { Notification, Task, Project, TeamMember, Invitation } from "../schema"

/**
 * Universal helper to get property value regardless of casing
 * Supports: camelCase, PascalCase, snake_case
 */
function getProp<T = unknown>(obj: Record<string, unknown>, key: string): T | undefined {
  if (!obj) return undefined

  // Direct match
  if (obj[key] !== undefined) return obj[key] as T

  // Try PascalCase
  const pascal = key.charAt(0).toUpperCase() + key.slice(1)
  if (obj[pascal] !== undefined) return obj[pascal] as T

  // Try camelCase
  const camel = key.charAt(0).toLowerCase() + key.slice(1)
  if (obj[camel] !== undefined) return obj[camel] as T

  // Try snake_case
  const snake = key.replace(/[A-Z]/g, (letter) => `_${letter.toLowerCase()}`)
  if (obj[snake] !== undefined) return obj[snake] as T

  return undefined
}

function requireProp<T = unknown>(obj: Record<string, unknown>, key: string): T {
  const value = getProp<T>(obj, key)
  if (value === undefined || value === null) {
    throw new Error(`Missing required property "${key}" in backend response`)
  }

  return value
}

function getFirstOf<T>(obj: Record<string, unknown>, ...keys: string[]): T | undefined {
  for (const key of keys) {
    const value = getProp<T>(obj, key)
    if (value !== undefined) return value
  }
  return undefined
}

function normalizeTaskStatus(rawStatus: string): string {
  const status = rawStatus.toLowerCase()
  return status === "in_progress" ? "doing" : status
}

function getOptional<T>(dto: Record<string, unknown>, key: string): T | undefined {
  return getProp<T>(dto, key) || undefined
}

function parseTechStack(techStack: string | string[] | undefined): string[] {
  if (Array.isArray(techStack)) return techStack
  if (typeof techStack === "string") return techStack.split(/\s+/).filter(Boolean)
  return []
}

const difficultyMap: Record<string, string> = {
  beginner: "low",
  advanced: "high",
}

function mapDifficultyToComplexity(difficulty: string | undefined): string | undefined {
  if (!difficulty) return undefined
  return difficultyMap[difficulty] || "medium"
}

function asTypedValue<T>(value: unknown, type: string): T | undefined {
  return typeof value === type ? (value as T) : undefined
}

function asNullableNumber(value: unknown): number | null | undefined {
  if (typeof value === "number" || value === null) return value as number | null
  return undefined
}

// Backend response types (now generic - supports any casing)
type BackendNotificationDto = Record<string, unknown>
type BackendTaskDto = Record<string, unknown>
type BackendProjectSummaryDto = Record<string, unknown>
type BackendTeamMemberDto = Record<string, unknown>
type BackendInvitationDto = Record<string, unknown>

export function mapNotification(dto: BackendNotificationDto, userId: string): Notification {
  const content = getProp<string>(dto, "content")
  const relatedEntityType = getProp<string>(dto, "relatedEntityType")
  const relatedEntityId = getProp<string>(dto, "relatedEntityId")
  const priority = getProp<string>(dto, "priority")
  const id = requireProp<string>(dto, "id")
  const type = requireProp<string>(dto, "type")
  const title = requireProp<string>(dto, "title")
  const createdAt = requireProp<string>(dto, "createdAt")

  return {
    id,
    userId,
    type: type as Notification["type"],
    title,
    message: content || title,
    read: getProp(dto, "isRead") ?? false,
    metadata: {
      relatedEntityType,
      relatedEntityId,
      priority,
    },
    createdAt,
  }
}

export function mapTask(dto: BackendTaskDto): Task {
  const id = requireProp<string>(dto, "id")
  const projectId = requireProp<string>(dto, "projectId")
  const title = requireProp<string>(dto, "title")
  const createdAt = requireProp<string>(dto, "createdAt")
  const status = normalizeTaskStatus(getProp<string>(dto, "status") || "")

  return {
    id,
    projectId,
    title,
    description: getOptional(dto, "description"),
    status: status as Task["status"],
    priority: (getProp(dto, "priority") as Task["priority"]) || "medium",
    assigneeId: getOptional(dto, "assignedToUserId"),
    assigneeName: getOptional(dto, "assignedToUserName"),
    assigneeAvatarUrl: getOptional(dto, "assignedToAvatarUrl"),
    dueDate: getOptional(dto, "deadline"),
    columnId: getOptional(dto, "columnId"),
    positionInColumn: getProp(dto, "positionInColumn") ?? 0,
    linkCount: getProp(dto, "linkCount") ?? 0,
    attachmentCount: getProp(dto, "attachmentCount") ?? 0,
    tags: getFirstOf<string>(dto, "tags", "Tags"),
    gitHubIssueId: getOptional(dto, "gitHubIssueId"),
    gitHubIssueNumber: getOptional(dto, "gitHubIssueNumber"),
    gitHubIssueUrl: getOptional(dto, "gitHubIssueUrl"),
    createdAt,
    updatedAt: getFirstOf<string>(dto, "updatedAt", "completedAt", "createdAt") || new Date().toISOString(),
  }
}

export function mapProject(dto: BackendProjectSummaryDto): Project {
  const id = requireProp<string>(dto, "id")
  const title = requireProp<string>(dto, "title")
  const description = requireProp<string>(dto, "description")
  const createdAt = requireProp<string>(dto, "createdAt")
  const updatedAt = requireProp<string>(dto, "updatedAt")
  const ownerId = requireProp<string>(dto, "ownerId")
  const durationDays = getProp<number>(dto, "expectedDurationDays")
  const technologies = parseTechStack(getProp<string | string[]>(dto, "techStack"))
  const complexityLevel = mapDifficultyToComplexity(getProp<string>(dto, "difficultyLevel"))

  return {
    id,
    slug: getProp<string>(dto, "slug") ?? null,
    title,
    description,
    detailedDescription: getOptional(dto, "shortDescription"),
    technologies,
    requiredRoles: getProp(dto, "requiredRoles") || [],
    openRoles: getProp(dto, "openRoles") ?? undefined,
    status: getProp(dto, "status") as Project["status"],
    visibility: getProp(dto, "visibility") as Project["visibility"],
    defaultNewsVisibility: getFirstOf<string>(dto, "defaultNewsVisibility", "DefaultNewsVisibility", "default_news_visibility", "visibility"),
    defaultFilesVisibility: getFirstOf<string>(dto, "defaultFilesVisibility", "DefaultFilesVisibility", "default_files_visibility", "visibility"),
    ownerId,
    teamSize: asTypedValue<number>(getProp(dto, "teamSize"), "number"),
    maxTeamSize: asNullableNumber(getProp(dto, "maxTeamSize")),
    featured: asTypedValue<boolean>(getProp(dto, "featured"), "boolean"),
    rating: asNullableNumber(getProp(dto, "rating")),
    showcasePublished: asTypedValue<boolean>(getProp(dto, "showcasePublished"), "boolean"),
    boostsCount: asTypedValue<number>(getProp(dto, "boostsCount"), "number") ?? 0,
    boostedByMe: asTypedValue<boolean>(getProp(dto, "boostedByMe"), "boolean") ?? false,
    openRolesCount: asTypedValue<number>(getProp(dto, "openRolesCount"), "number") ?? 0,
    duration: durationDays ? `${durationDays} days` : undefined,
    complexityLevel,
    createdAt,
    updatedAt,
    team: getOptional(dto, "team"),
    tasks: getOptional(dto, "tasks"),
    invitations: getOptional(dto, "invitations"),
  } as Project
}

export function mapTeamMember(dto: BackendTeamMemberDto, projectId: string): TeamMember {
  const name = getProp<string>(dto, "name")
  const id = getProp<string>(dto, "id")
  const userId = getProp<string>(dto, "userId")
  if (!id || !userId) {
    throw new Error("Missing required fields: id or userId")
  }
  return {
    id,
    userId,
    projectId,
    role: getProp(dto, "role") as TeamMember["role"],
    canPublishNews: getProp(dto, "canPublishNews") ?? getProp(dto, "CanPublishNews"),
    canManageTasks: getProp(dto, "canManageTasks") ?? getProp(dto, "CanManageTasks"),
    canManageFiles: getProp(dto, "canManageFiles") ?? getProp(dto, "CanManageFiles"),
    canManageGallery: getProp(dto, "canManageGallery") ?? getProp(dto, "CanManageGallery"),
    user: name
      ? {
        id: userId,
        email: "",
        name,
        role: "participant" as const,
        avatar: getProp(dto, "avatar") || undefined,
        skills: [],
        language: "pl" as const,
        createdAt: "",
        updatedAt: "",
      }
      : undefined,
    joinedAt: "",
  }
}

export function mapInvitation(dto: BackendInvitationDto): Invitation {
  const id = requireProp<string>(dto, "id")
  const projectId = requireProp<string>(dto, "projectId")
  const inviteeId = requireProp<string>(dto, "inviteeId")
  const inviterId = requireProp<string>(dto, "inviterId")
  const createdAt = requireProp<string>(dto, "createdAt")

  return {
    id,
    projectId,
    inviteeId,
    inviterId,
    type: (getProp(dto, "type") as Invitation["type"]) || "invite",
    role: (getProp(dto, "role") as string) || "",
    message: getOptional(dto, "message"),
    status: getProp(dto, "status") as Invitation["status"],
    createdAt,
  }
}
