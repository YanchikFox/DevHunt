/**
 * JWT token utilities
 */

export interface JWTPayload {
  sub: string // User ID
  email: string
  role: string
  exp: number
  iss: string
  aud: string
}

/**
 * Decode JWT token (without verification - only use for reading claims)
 */
export function decodeJWT(token: string): JWTPayload | null {
  try {
    const parts = token.split(".")
    if (parts.length !== 3) return null

    const payload = parts[1]
    const decoded = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")))

    return {
      sub: decoded.sub,
      email:
        decoded.email ||
        decoded["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"],
      role: decoded.role || decoded["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"],
      exp: decoded.exp,
      iss: decoded.iss,
      aud: decoded.aud,
    }
  } catch (e) {
    console.error("Failed to decode JWT:", e)
    return null
  }
}

/**
 * Check if JWT token is expired
 */
export function isTokenExpired(token: string): boolean {
  const payload = decodeJWT(token)
  if (!payload) return true

  const now = Math.floor(Date.now() / 1000)
  return payload.exp < now
}

/**
 * Get user info from JWT token
 */
export function getUserFromToken(token: string | null) {
  if (!token) return null

  if (isTokenExpired(token)) {
    localStorage.removeItem("auth_token")
    return null
  }

  const payload = decodeJWT(token)
  if (!payload) return null

  const emailPart = payload.email.split("@")[0]
  // Capitalize first letter and replace dots/underscores with spaces
  const displayName = emailPart
    .replace(/[._-]/g, " ")
    .split(" ")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ")

  return {
    id: payload.sub,
    email: payload.email,
    name: displayName,
    role: payload.role as "admin" | "participant",
    avatar: undefined,
    skills: [],
    language: "pl" as const,
    createdAt: "",
    updatedAt: "",
  }
}
