import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockAuth } from "../adapters/mock"
import { authClient } from "../client"
import type { LoginRequest, RegisterRequest, AuthResponse, RegisterResponse } from "../schema"

const USERNAME_REGEX = /^[a-zA-Z0-9_\-.]+$/

/**
 * Response type for /api/auth/me endpoint
 */
export interface CurrentUserResponse {
  id: string
  email: string
  role: string
  fullName?: string
  avatarUrl?: string
  isEmailVerified: boolean
  isVerified: boolean
  createdAt: string
}

/**
 * Get current authenticated user
 * MVP FIX: Now uses the new /api/auth/me endpoint
 */
export function useAuth() {
  return useQuery({
    queryKey: ["auth", "user"],
    queryFn: async (): Promise<CurrentUserResponse | null> => {
      if (USE_MOCKS) {
        const mockUsers = await import("@/mocks/data").then((m) => m.mockUsers)
        const mock = mockUsers[0]
        if (!mock) return null
        const mockResponse: CurrentUserResponse = {
          id: mock.id,
          email: mock.email,
          role: mock.role,
          fullName: mock.name,
          avatarUrl: mock.avatar,
          isEmailVerified: true,
          isVerified: true,
          createdAt: mock.createdAt,
        }
        return mockResponse
      }
      
      try {
        // MVP FIX: Call the new /api/auth/me endpoint
        const response = await authClient.get<CurrentUserResponse>("/auth/me")
        return response.data
      } catch (error: unknown) {
        // 401/403 means not authenticated - return null (not an error)
        const status =
          typeof error === "object" && error !== null && "response" in error
            ? (error as Record<string, unknown>)["response"]
            : undefined
        const statusCode =
          typeof status === "object" && status !== null && "status" in status
            ? (status as Record<string, unknown>)["status"]
            : undefined
        if (statusCode === 401 || statusCode === 403) {
          return null
        }
        // For other errors, also return null to prevent breaking the UI
        const message = error instanceof Error ? error.message : String(error)
        console.warn("Failed to fetch current user:", message)
        return null
      }
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: false,
    // Always enabled - will return null if not authenticated
  })
}

/**
 * Login mutation
 * R7: Authentication now uses httpOnly cookies (set by backend)
 */
export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: LoginRequest): Promise<AuthResponse> => {
      if (USE_MOCKS) {
        return mockAuth.login(data)
      }

      // Backend endpoint: POST /api/Auth/login
      // R7: Backend automatically sets httpOnly cookies (access_token, refresh_token)
      const response = await authClient.post<AuthResponse>("/Auth/login", data)
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth"] })
      queryClient.invalidateQueries({ queryKey: ["profile"] })
    },
  })
}

/**
 * Register mutation
 * R7: Authentication now uses httpOnly cookies (set by backend)
 */
export function useRegister() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: RegisterRequest): Promise<RegisterResponse> => {
      if (USE_MOCKS) {
        const response = await mockAuth.register(data)
        return { message: "Registration successful", userId: response.userId }
      }
      // Backend endpoint: POST /api/Auth/register
      // Returns { message, userId, autoVerified? } — NOT tokens (requires email verification first)
      const response = await authClient.post<RegisterResponse>("/Auth/register", data)
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth"] })
    },
  })
}

/**
 * Check username availability (debounce on the call site)
 */
export function useCheckUsername(username: string) {
  return useQuery({
    queryKey: ["auth", "check-username", username],
    queryFn: async () => {
      const { data } = await authClient.get<{ available: boolean; reason?: string }>(
        `/auth/check-username?username=${encodeURIComponent(username)}`
      )
      return data
    },
    enabled: username.length >= 3 && USERNAME_REGEX.test(username),
    staleTime: 10_000,
  })
}

// ========================================================================
// Two-Factor Authentication (TOTP)
// ========================================================================

export interface TotpSetupResponse {
  secret: string
  qrUri: string
}

export interface TotpSetupCompleteResponse {
  recoveryCodes: string[]
}

export interface TwoFactorRequiredResponse {
  requiresTwoFactor: boolean
  userId: string
}

/** Check if 2FA is enabled for current user */
export function useTotpStatus() {
  return useQuery({
    queryKey: ["auth", "totp-status"],
    queryFn: async () => {
      const { data } = await authClient.get<{ isEnabled: boolean }>("/auth/totp/status")
      return data
    },
    staleTime: 5 * 60 * 1000,
    retry: false,
  })
}

/** Start TOTP setup — returns secret + QR URI */
export function useTotpSetup() {
  return useMutation({
    mutationFn: async () => {
      const { data } = await authClient.post<TotpSetupResponse>("/auth/totp/setup")
      return data
    },
  })
}

/** Verify TOTP code and enable 2FA — returns recovery codes */
export function useVerifyTotpSetup() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (req: { code: string; secret: string }) => {
      const { data } = await authClient.post<TotpSetupCompleteResponse>("/auth/totp/verify-setup", req)
      return data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth", "totp-status"] })
    },
  })
}

/** Disable 2FA */
export function useDisableTotp() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (req: { password: string; totpCode?: string }) => {
      await authClient.post("/auth/totp/disable", req)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth", "totp-status"] })
    },
  })
}

/**
 * Logout mutation
 * R7: Backend clears httpOnly cookies on logout
 */
export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      if (USE_MOCKS) {
        return
      }
      // Backend endpoint: POST /api/Auth/logout
      // R7: Backend automatically expires httpOnly cookies
      await authClient.post("/auth/logout", { refreshToken: "" })
    },
    onSuccess: async () => {
      queryClient.setQueryData(["auth", "user"], null)
      queryClient.setQueryData(["profile"], null)
      queryClient.invalidateQueries({ queryKey: ["auth"] })
      queryClient.invalidateQueries({ queryKey: ["profile"] })
    },
  })
}
