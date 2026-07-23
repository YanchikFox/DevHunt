
// Cache the token to avoid reading cookie on every request
let cachedToken: string | null = null
let tokenFetchPromise: Promise<string | null> | null = null

/**
 * Get CSRF token from cookie (set by backend middleware)
 */
export function getCsrfTokenFromCookie(): string | null {
  if (typeof document === "undefined") {
    return null // Server-side: no cookies
  }

  const cookies = document.cookie.split(";")
  for (const cookie of cookies) {
    const [name, value] = cookie.trim().split("=")
    if (name === "XSRF-REQUEST-TOKEN") {
      return decodeURIComponent(value)
    }
  }
  return null
}

/**
 * Fetch CSRF token from backend endpoint
 * Called on app initialization and after token expiry
 */
export async function fetchCsrfToken(baseUrl: string): Promise<string | null> {
  // Reuse existing fetch promise if in progress
  if (tokenFetchPromise) {
    return tokenFetchPromise
  }

  tokenFetchPromise = (async () => {
    try {
      const response = await fetch(`${baseUrl}/Auth/csrf-token`, {
        method: "GET",
        credentials: "include", // Include cookies
      })

      if (!response.ok) {
        // skipcq: JS-A1004 - response.status is HTTP status code (number), not user input
        console.warn("Failed to fetch CSRF token, status:", String(response.status))
        return null
      }

      const data = await response.json()
      cachedToken = data.requestToken || null
      return cachedToken
    } catch (error) {
      console.warn("Error fetching CSRF token:", error)
      return null
    } finally {
      tokenFetchPromise = null
    }
  })()

  return tokenFetchPromise
}

/**
 * Get the current CSRF token (from cache, cookie, or fetch new one)
 */
export function getCsrfToken(): string | null {
  // First, try cached token
  if (cachedToken) {
    return cachedToken
  }

  // Then, try cookie
  const cookieToken = getCsrfTokenFromCookie()
  if (cookieToken) {
    cachedToken = cookieToken
    return cachedToken
  }

  return null
}

/**
 * Clear cached CSRF token AND the cookie (call after login, logout or on 403 error)
 * This forces a fresh token to be issued for the new user
 */
export function clearCsrfToken(): void {
  cachedToken = null
  tokenFetchPromise = null
  
  // Also clear the cookie so backend issues a fresh token
  if (typeof document !== "undefined") {
    document.cookie = "XSRF-REQUEST-TOKEN=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT"
    // Also try with SameSite variations
    document.cookie = "XSRF-REQUEST-TOKEN=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax"
    document.cookie = "XSRF-REQUEST-TOKEN=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Strict"
  }
}

/**
 * Refresh CSRF token after user identity changes (login/logout).
 * Clears old token and fetches a new one for the current user.
 * 
 * MUST be called after successful login to get a token tied to the new user identity.
 */
export async function refreshCsrfToken(baseUrl?: string): Promise<string | null> {
  // Clear the old token first
  clearCsrfToken()
  
  // Determine the base URL for fetching new token
  const url = baseUrl || (typeof window !== "undefined" ? getAuthBaseUrl() : "")
  if (!url) {
    console.warn("Cannot refresh CSRF token: no base URL available")
    return null
  }
  
  // Fetch new token for the current (authenticated) user
  return fetchCsrfToken(url)
}

/**
 * Helper to get Auth base URL for CSRF token endpoint
 */
function getAuthBaseUrl(): string {
  const fromEnv = process.env.NEXT_PUBLIC_AUTH_URL_CLIENT || process.env.NEXT_PUBLIC_AUTH_URL
  if (fromEnv && fromEnv.trim().length > 0) {
    return fromEnv
  }
  return "/api/proxy-auth"
}

/**
 * Check if request method requires CSRF token
 */
export function requiresCsrfToken(method: string): boolean {
  const safeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"]
  return !safeMethods.includes(method.toUpperCase())
}
