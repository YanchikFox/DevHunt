import type { UserProfile } from "./profile-types"

const toStringValue = (value: unknown, fallback = ""): string => {
  if (typeof value === "string") return value
  if (value == null) return fallback
  return String(value)
}

const toOptionalString = (value: unknown): string | undefined => {
  if (value == null) return undefined
  return typeof value === "string" ? value : String(value)
}

const toNumberValue = (value: unknown): number | undefined => {
  if (typeof value === "number") return value
  if (typeof value === "string" && value.trim() !== "") {
    const parsed = Number(value)
    return Number.isNaN(parsed) ? undefined : parsed
  }
  return undefined
}

const toBooleanValue = (value: unknown, fallback: boolean): boolean => {
  if (typeof value === "boolean") return value
  if (typeof value === "string") return value.toLowerCase() === "true"
  if (typeof value === "number") return value !== 0
  return fallback
}

/**
 * Get a value from DTO supporting both camelCase and PascalCase keys.
 */
function getDtoValue(dto: Record<string, unknown>, camelKey: string): unknown {
  const pascalKey = camelKey.charAt(0).toUpperCase() + camelKey.slice(1)
  return dto?.[camelKey] ?? dto?.[pascalKey]
}

/**
 * Normalize skills array from DTO (handles string or object formats).
 */
function normalizeSkills(dto: Record<string, unknown>): string[] {
  const source = getDtoValue(dto, "skills") ?? []
  if (!Array.isArray(source)) return []

  return source.map((skill: unknown) => {
    if (typeof skill === "string") return skill
    if (skill && typeof skill === "object") {
      const obj = skill as Record<string, unknown>
      const nameValue = getDtoValue(obj, "name")
      return toStringValue(nameValue)
    }
    return toStringValue(skill)
  })
}

/**
 * Normalize projects array from DTO.
 */
function normalizeProjects(dto: Record<string, unknown>): { id: string; title?: string; description?: string }[] {
  const source = getDtoValue(dto, "projects") ?? []
  if (!Array.isArray(source)) return []

  return source.map((project: unknown) => {
    const obj = project as Record<string, unknown>
    return {
      id: toStringValue(getDtoValue(obj, "id")),
      title: toOptionalString(getDtoValue(obj, "title")),
      description: toOptionalString(getDtoValue(obj, "description")),
    }
  })
}

export function mapUserProfileDto(dto: Record<string, unknown>): UserProfile {
  // Stats may be at top level (ProfileResponse) or nested in Stats object (UserProfileDto)
  const stats = (getDtoValue(dto, "stats") ?? {}) as Record<string, unknown>

  return {
    id: toStringValue(getDtoValue(dto, "id")),
    email: toStringValue(getDtoValue(dto, "email")),
    role: toStringValue(getDtoValue(dto, "role")),
    fullName: toOptionalString(getDtoValue(dto, "fullName")),
    bio: toOptionalString(getDtoValue(dto, "bio")),
    timezone: toOptionalString(getDtoValue(dto, "timezone")),
    skills: normalizeSkills(dto),
    experience: toNumberValue(getDtoValue(dto, "experience")),
    rating: toNumberValue(getDtoValue(dto, "rating")),
    avatarUrl: toOptionalString(getDtoValue(dto, "avatarUrl")),
    isVerified: toBooleanValue(getDtoValue(dto, "isVerified"), false),
    isActive: toBooleanValue(getDtoValue(dto, "isActive"), true),
    createdAt: toStringValue(getDtoValue(dto, "createdAt"), ""),
    updatedAt: toOptionalString(getDtoValue(dto, "updatedAt")),
    lastLogin: toOptionalString(getDtoValue(dto, "lastLogin")),
    language: toOptionalString(getDtoValue(dto, "language")),
    github: toOptionalString(getDtoValue(dto, "github")),
    githubUsername: toOptionalString(getDtoValue(dto, "githubUsername")),
    linkedin: toOptionalString(getDtoValue(dto, "linkedin")),
    website: toOptionalString(getDtoValue(dto, "website")),
    relationVisibility: toOptionalString(getDtoValue(dto, "relationVisibility")),
    activityVisibility: toOptionalString(getDtoValue(dto, "activityVisibility")),
    isFollower: toBooleanValue(getDtoValue(dto, "isFollower"), false),
    isFollowing: toBooleanValue(getDtoValue(dto, "isFollowing"), false),
    followersCount: toNumberValue(getDtoValue(dto, "followersCount")) ?? toNumberValue(getDtoValue(stats, "followersCount")),
    followingCount: toNumberValue(getDtoValue(dto, "followingCount")) ?? toNumberValue(getDtoValue(stats, "followingCount")),
    projectsCount: toNumberValue(getDtoValue(dto, "projectsCount")) ?? toNumberValue(getDtoValue(stats, "projectsCount")),
    projects: normalizeProjects(dto),
  }
}
