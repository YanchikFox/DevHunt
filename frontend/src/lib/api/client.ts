import axios, { type AxiosInstance } from "axios"
import qs from "qs"
import { toCamelCase, toPascalCase } from "@/lib/utils"
import { getCsrfToken, requiresCsrfToken, clearCsrfToken, fetchCsrfToken, refreshCsrfToken } from "./csrf"
import { stripLocalePrefix, extractLocalePrefix } from "@/i18n/locale-utils"

const DEFAULT_CORE_PROXY_PATH = "/api/proxy-core"
const DEFAULT_AUTH_PROXY_PATH = "/api/proxy-auth"

/**
 * Get API base URL based on environment
 */
function getApiBaseUrl(): string {
  if (typeof window !== "undefined") {
    const fromEnv = process.env.NEXT_PUBLIC_API_URL_CLIENT || process.env.NEXT_PUBLIC_API_URL
    if (fromEnv && fromEnv.trim().length > 0) {
      return fromEnv
    }
    return DEFAULT_CORE_PROXY_PATH
  }
  return (
    process.env.CORE_SERVICE_URL ||
    process.env.CORE_API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://core-api:8080/api"
  )
}

/**
 * Get Auth service base URL
 */
function getAuthBaseUrl(): string {
  if (typeof window !== "undefined") {
    const fromEnv = process.env.NEXT_PUBLIC_AUTH_URL_CLIENT || process.env.NEXT_PUBLIC_AUTH_URL
    if (fromEnv && fromEnv.trim().length > 0) {
      return fromEnv
    }
    return DEFAULT_AUTH_PROXY_PATH
  }
  return (
    process.env.AUTH_SERVICE_URL ||
    process.env.NEXT_PUBLIC_AUTH_URL ||
    "http://auth-service:8080/api"
  )
}

/**
 * Create axios instance for API calls
 * R7: Uses httpOnly cookies for authentication (XSS protection)
 */
export const apiClient: AxiosInstance = axios.create({
  baseURL: getApiBaseUrl(),
  timeout: 30000,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true,
  paramsSerializer: (params) => qs.stringify(params, { arrayFormat: "repeat" }),
})

/**
 * Create axios instance for Auth service
 * R7: Uses httpOnly cookies for authentication (XSS protection)
 */
export const authClient: AxiosInstance = axios.create({
  baseURL: getAuthBaseUrl(),
  timeout: 30000,
  headers: {
    "Content-Type": "application/json",
  },
  withCredentials: true,
})

// Request interceptor for case conversion and CSRF (auth via BFF proxy + httpOnly cookies)
apiClient.interceptors.request.use(async (config) => {
  // SEC-011: Add CSRF token for state-changing requests
  if (requiresCsrfToken(config.method || "GET")) {
    let csrfToken = getCsrfToken()
    
    // If no cached token, try to fetch one (CSRF is always from Auth service)
    if (!csrfToken && typeof window !== "undefined") {
      csrfToken = await fetchCsrfToken(getAuthBaseUrl())
    }
    
    if (csrfToken) {
      config.headers["X-CSRF-TOKEN"] = csrfToken
    }
  }

  if (config.data && !(config.data instanceof FormData)) {
    config.data = toPascalCase(config.data)
  }
  return config
})

// Add response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => {
    if (response.data) {
      response.data = toCamelCase(response.data)
    }
    return response
  },
  async (error) => {
    let errorMessage = error.message

    if (error.response?.data) {
      // Try to get error message from different response formats
      if (typeof error.response.data === "string") {
        errorMessage = error.response.data
      } else if (error.response.data.message) {
        errorMessage = error.response.data.message
      } else if (error.response.data.error) {
        errorMessage = error.response.data.error
      } else if (error.response.data.title) {
        errorMessage = error.response.data.title
      }
    }

    // Add user-friendly status messages
    const status = error.response?.status
    if (status === 400) {
      errorMessage = errorMessage || "Invalid request. Please check your input."
    } else if (status === 401) {
      const isProfileRequest = error.config?.url?.includes("/Profile/me")

      // Only redirect to login when on a protected page (dashboard/admin).
      // Public pages (home, projects, community, etc.) should stay accessible.
      const isOnProtectedPage = (() => {
        if (typeof window === "undefined") return false
        const pathname = window.location.pathname
        // Strip locale prefix to get the logical path
        const logicalPath = stripLocalePrefix(pathname)
        return logicalPath.startsWith("/dashboard") || logicalPath.startsWith("/admin")
      })()

      if (!isProfileRequest && isOnProtectedPage && typeof window !== "undefined") {
        const localePrefix = extractLocalePrefix(window.location.pathname)
        window.location.href = `${localePrefix}/login`
      }
      errorMessage = errorMessage || "Authentication required. Please log in."
    } else if (status === 403) {
      // SEC-011: Handle CSRF validation failure
      // Check for CSRF-related errors (backend may return different formats)
      const responseData = error.response?.data
      const responseText = JSON.stringify(responseData || "").toLowerCase()
      const isCsrfUserMismatch = responseData?.code === "CSRF_USER_MISMATCH"
      const isCsrfError = 
        responseData?.code === "CSRF_VALIDATION_ERROR" ||
        isCsrfUserMismatch ||
        responseText.includes("csrf") ||
        responseText.includes("antiforgery") ||
        responseText.includes("different claims-based user")
      
      if (isCsrfError) {
        // For user mismatch, automatically refresh token and retry once
        if (isCsrfUserMismatch && !error.config?._csrfRetried) {
          const newToken = await refreshCsrfToken()
          if (newToken && error.config) {
            error.config._csrfRetried = true
            error.config.headers["X-CSRF-TOKEN"] = newToken
            return apiClient.request(error.config)
          }
        }
        clearCsrfToken()
        errorMessage = "Security token expired. Please try again."
      } else {
        errorMessage = errorMessage || "You don't have permission to perform this action."
      }
    } else if (status === 404) {
      errorMessage = errorMessage || "The requested resource was not found."
    } else if (status === 409) {
      errorMessage = errorMessage || "This action conflicts with existing data."
    } else if (status === 413) {
      errorMessage = errorMessage || "File is too large. Maximum size is 5MB."
    } else if (status === 415) {
      errorMessage = errorMessage || "Unsupported file type."
    } else if (status === 429) {
      errorMessage = errorMessage || "Too many requests. Please try again later."
    } else if (status === 500) {
      errorMessage = errorMessage || "Server error. Please try again later."
    } else if (status === 503) {
      const body = error.response?.data
      const isMaintenance =
        typeof body?.message === "string" && body.message.toLowerCase().includes("maintenance")
      errorMessage = body?.message || "Service temporarily unavailable. Please try again later."
      // Redirect non-admin users to the maintenance page
      if (isMaintenance && globalThis.window !== undefined) {
        const loc = globalThis.window.location
        const localePrefix = extractLocalePrefix(loc.pathname)
        if (!loc.pathname.endsWith("/maintenance")) {
          loc.href = `${localePrefix}/maintenance`
        }
      }
    }

    // Attach formatted message to error (both .userMessage and .message for catch blocks)
    error.userMessage = errorMessage
    if (errorMessage && errorMessage !== error.message) {
      error.message = errorMessage
    }

    return Promise.reject(error)
  }
)

authClient.interceptors.request.use(async (config) => {
  // SEC-011: Add CSRF token for state-changing requests (except auth endpoints)
  const exemptPaths = ["/auth/login", "/auth/register", "/auth/refresh", "/auth/forgot-password", "/auth/reset-password"]
  const isExempt = exemptPaths.some(path => config.url?.includes(path))
  
  if (!isExempt && requiresCsrfToken(config.method || "GET")) {
    let csrfToken = getCsrfToken()
    
    if (!csrfToken && typeof window !== "undefined") {
      csrfToken = await fetchCsrfToken(getAuthBaseUrl())
    }
    
    if (csrfToken) {
      config.headers["X-CSRF-TOKEN"] = csrfToken
    }
  }

  if (config.data && !(config.data instanceof FormData)) {
    config.data = toPascalCase(config.data)
  }
  return config
})

authClient.interceptors.response.use(
  (response) => {
    if (response.data) {
      response.data = toCamelCase(response.data)
    }
    return response
  },
  (error) => {
    let errorMessage = error.message

    if (error.response?.data) {
      if (typeof error.response.data === "string") {
        errorMessage = error.response.data
      } else if (error.response.data.message) {
        errorMessage = error.response.data.message
      } else if (error.response.data.error) {
        errorMessage = error.response.data.error
      }
    }

    const status = error.response?.status
    if (status === 401) {
      // Do NOT redirect on 401 from authClient.
      // The useAuth() hook already handles 401 gracefully by returning null.
      // Redirecting here causes public pages (home, projects, etc.) to break
      // because Header calls useAuth() → /auth/me → 401 → redirect to login.
      errorMessage = errorMessage || "Authentication failed. Please check your credentials."
    } else if (status === 400) {
      errorMessage = errorMessage || "Invalid credentials or request data."
    } else if (status === 409) {
      errorMessage = errorMessage || "User already exists."
    }

    error.userMessage = errorMessage
    return Promise.reject(error)
  }
)

/**
 * Helper function to simulate delay (for mocks)
 * @param ms - Milliseconds to delay
 */
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
