/**
 * Explicit allowlists for BFF proxy routes (DEV-21).
 * Returns 404 for blocked or unknown paths to avoid endpoint enumeration.
 */

export type ProxyService = "core" | "auth" | "ml"

const BLOCKED_PREFIXES = [
  "internal",
  "metrics",
  "swagger",
  "health",
  "hangfire",
  "api-docs",
] as const

/** Paths the Next.js app may forward to Core API (relative to /api/). */
const CORE_ALLOWED_PREFIXES = [
  "profile",
  "users",
  "feed",
  "activities",
  "projects",
  "notifications",
  "invitations",
  "integrations",
  "chat",
  "ai",
  "skills",
  "showcase",
  "moderation",
  "admin",
  "badges",
  "support",
  "feature-flags",
  "teams",
  "recommendations",
  "reports",
  "curator",
  "superadmin",
  "platform",
  "me",
  "csrf-token",
] as const

/** Paths forwardable to Auth service (relative to /api/). */
const AUTH_ALLOWED_PREFIXES = [
  "auth",
] as const

/** Paths forwardable to ML service (relative to /api/). */
const ML_ALLOWED_PREFIXES = [
  "ai",
] as const

/**
 * Normalizes proxy path segments to a lowercase slash-separated path.
 */
export function normalizeProxyPath(pathSegments: string[]): string {
  return pathSegments
    .map((segment) => decodeURIComponent(segment).trim())
    .filter(Boolean)
    .join("/")
    .toLowerCase()
}

function matchesAllowedPrefix(path: string, allowed: readonly string[]): boolean {
  return allowed.some(
    (prefix) => path === prefix || path.startsWith(`${prefix}/`),
  )
}

function isBlockedPath(path: string): boolean {
  return BLOCKED_PREFIXES.some(
    (blocked) => path === blocked || path.startsWith(`${blocked}/`),
  )
}

/**
 * Returns whether the given proxy target path is allowed for the service.
 */
export function isProxyPathAllowed(
  service: ProxyService,
  pathSegments: string[],
): boolean {
  const path = normalizeProxyPath(pathSegments)
  if (!path || isBlockedPath(path)) {
    return false
  }

  switch (service) {
    case "core":
      return matchesAllowedPrefix(path, CORE_ALLOWED_PREFIXES)
    case "auth":
      return matchesAllowedPrefix(path, AUTH_ALLOWED_PREFIXES)
    case "ml":
      return matchesAllowedPrefix(path, ML_ALLOWED_PREFIXES)
    default:
      return false
  }
}
