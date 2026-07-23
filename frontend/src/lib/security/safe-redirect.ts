/**
 * Default post-login redirect when callbackUrl is missing or unsafe.
 */
export const DEFAULT_SAFE_REDIRECT = "/dashboard"

/**
 * Returns true when the URL is a safe same-origin relative path for client-side navigation.
 *
 * Allowed: `/dashboard`, `/profile`, `/projects/abc`
 * Blocked: `https://evil.com`, `//evil.com`, `javascript:alert(1)`, `/\\evil.com`
 *
 * @param url - Raw callback URL from query params or user input.
 */
export function isSafeRedirectUrl(url: string | null | undefined): url is string {
  if (!url || typeof url !== "string") return false

  const trimmed = url.trim()
  if (trimmed.length === 0) return false

  if (!trimmed.startsWith("/") || trimmed.startsWith("//")) return false

  if (trimmed.includes("\\")) return false

  const lower = trimmed.toLowerCase()
  if (lower.startsWith("/javascript:") || lower.includes("://")) return false

  return true
}

/**
 * Resolves a user-supplied callback URL to a safe internal path or the default fallback.
 *
 * @param url - Raw callback URL from query params.
 * @param fallback - Path used when validation fails (default: `/dashboard`).
 */
export function resolveSafeRedirectUrl(
  url: string | null | undefined,
  fallback: string = DEFAULT_SAFE_REDIRECT,
): string {
  return isSafeRedirectUrl(url) ? url : fallback
}
