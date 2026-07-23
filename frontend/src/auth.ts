import NextAuth from "next-auth"
import Credentials from "next-auth/providers/credentials"
import { authConfig } from "./auth.config"

/**
 * NextAuth configuration and initialization.
 * Integrates with the DevHunt AuthService for user authentication.
 *
 * R7: Uses httpOnly cookies by default (secure against XSS attacks).
 */
export const {
  handlers: { GET, POST },
  auth,
  signIn,
  signOut,
} = NextAuth({
  ...authConfig,
  providers: [
    Credentials({
      name: "DevHunt",
      credentials: {
        email: { label: "Email", type: "email" },
        password: { label: "Password", type: "password" },
        totpCode: { label: "totpCode", type: "text" },
        recoveryCode: { label: "recoveryCode", type: "text" },
        accessToken: { label: "accessToken", type: "text" },
        refreshToken: { label: "refreshToken", type: "text" },
        userId: { label: "userId", type: "text" },
      },
      /**
       * Authorizes the user by calling the backend AuthService.
       * @param credentials - The user's email and password.
       * @returns The user object if successful, null otherwise.
       */
      async authorize(credentials) {
        // If OAuth callback provided tokens, verify them before trusting (L-03)
        if (credentials?.accessToken && credentials?.refreshToken && credentials?.userId) {
          try {
            const authServiceUrl =
              process.env.AUTH_SERVICE_URL ||
              process.env.NEXT_PUBLIC_AUTH_API_URL ||
              "http://auth-service:8080"
            const meUrl = authServiceUrl.startsWith("/")
              ? `${authServiceUrl}/auth/me`
              : `${authServiceUrl}/api/auth/me`

            const meResponse = await fetch(meUrl, {
              headers: { Authorization: `Bearer ${credentials.accessToken}` },
            })

            if (!meResponse.ok) {
              console.error("OAuth token verification failed:", String(meResponse.status))
              return null
            }

            const userData: { id?: string } = await meResponse.json()
            if (userData.id !== credentials.userId) {
              console.error("OAuth token userId mismatch")
              return null
            }
          } catch (error) {
            if (error instanceof TypeError) {
              console.error("Auth service unreachable during OAuth verification:", error)
            } else {
              console.error("OAuth token verification error:", error)
            }
            return null
          }

          return {
            id: credentials.userId as string,
            email: credentials.email as string | undefined,
            accessToken: credentials.accessToken as string,
            refreshToken: credentials.refreshToken as string,
          }
        }

        if (!credentials?.email || !credentials?.password) {
          return null
        }

        try {
          // R7: Call DevHunt AuthService for authentication
          const authServiceUrl =
            process.env.AUTH_SERVICE_URL ||
            process.env.NEXT_PUBLIC_AUTH_API_URL ||
            "http://auth-service:8080"

          // If AUTH_SERVICE_URL is a proxy path (e.g. /api/proxy-auth), call it directly.
          // Otherwise call AuthService directly at /api/auth/login.
          const loginUrl = authServiceUrl.startsWith("/")
            ? `${authServiceUrl}/auth/login`
            : `${authServiceUrl}/api/auth/login`

          const loginBody: Record<string, string> = {
            email: credentials.email as string,
            password: credentials.password as string,
          }
          if (credentials.totpCode) loginBody.totpCode = credentials.totpCode as string
          if (credentials.recoveryCode) loginBody.recoveryCode = credentials.recoveryCode as string

          const response = await fetch(loginUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(loginBody),
          })

          if (!response.ok) {
            const errorBody = await response.text().catch(() => "")
            console.error(
              "AuthService login failed",
              JSON.stringify({
                status: response.status,
                statusText: response.statusText,
                body: errorBody,
              })
            )
            // Special handling for 403 - email not verified
            if (response.status === 403) {
              throw new Error(`EmailNotVerified:${credentials.email}`)
            }
            return null
          }

          interface AuthLoginResponse {
            userId?: string
            email?: string
            accessToken?: string
            refreshToken?: string
            requiresTwoFactor?: boolean
          }
          let data: AuthLoginResponse | undefined
          try {
            data = await response.json()
          } catch {
            const text = await response.text().catch(() => "")
            console.error("AuthService returned non-JSON response", text)
            return null
          }

          if (!data) return null

          // 2FA required — signal frontend via special error
          if (data.requiresTwoFactor) {
            throw new Error(`TwoFactorRequired:${credentials.email}`)
          }

          if (!data.accessToken || !data.userId) return null

          return {
            id: data.userId,
            email: data.email,
            accessToken: data.accessToken,
            refreshToken: data.refreshToken,
          }
        } catch (error) {
          // Re-throw special errors so NextAuth passes them to the client
          if (error instanceof Error && (
            error.message.startsWith("EmailNotVerified:") ||
            error.message.startsWith("TwoFactorRequired:")
          )) {
            throw error
          }
          // L-12: Differentiate auth failures from infrastructure errors
          if (error instanceof TypeError) {
            // Network error — backend unreachable
            console.error("Auth service unreachable:", error)
          } else {
            console.error("Auth error:", error)
          }
          return null
        }
      },
    }),
  ],
  callbacks: {
    /**
     * JWT callback to store access and refresh tokens with auto-refresh support.
     * Checks token expiry and refreshes before it expires.
     */
    async jwt({ token, user }) {
      // Initial sign in - store tokens and calculate expiry
      if (user) {
        token.accessToken = user.accessToken
        token.refreshToken = user.refreshToken
        token.userId = user.id
        // Access tokens expire in 30 minutes, refresh 5 minutes before
        token.accessTokenExpires = Date.now() + 25 * 60 * 1000 // 25 minutes
      }

      // Return previous token if not expired
      if (token.accessTokenExpires && Date.now() < (token.accessTokenExpires as number)) {
        return token
      }

      // Token expired or about to expire - try to refresh
      if (token.refreshToken) {
        try {
          const authServiceUrl =
            process.env.AUTH_SERVICE_URL ||
            process.env.NEXT_PUBLIC_AUTH_API_URL ||
            "http://auth-service:8080"

          const refreshUrl = authServiceUrl.startsWith("/")
            ? `${authServiceUrl}/auth/refresh`
            : `${authServiceUrl}/api/auth/refresh`

          const response = await fetch(refreshUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken: token.refreshToken }),
          })

          if (response.ok) {
            const data = await response.json()
            return {
              ...token,
              accessToken: data.accessToken,
              refreshToken: data.refreshToken ?? token.refreshToken,
              accessTokenExpires: Date.now() + 25 * 60 * 1000, // 25 minutes
            }
          } else {
            // Refresh failed - clear tokens to force re-login
            // skipcq: JS-A1004 - response.status is HTTP status code (number), not user input
            console.error("Token refresh failed with status:", String(response.status))
            return {
              ...token,
              accessToken: undefined,
              refreshToken: undefined,
              accessTokenExpires: undefined,
              error: "RefreshTokenError",
            }
          }
        } catch (error) {
          console.error("Token refresh error:", error)
          return {
            ...token,
            error: "RefreshTokenError",
          }
        }
      }

      return token
    },
    /**
     * Session callback — exposes user id only; tokens stay in the encrypted JWT (BFF / server routes).
     */
    async session({ session, token }) {
      if (token) {
        session.user.id = token.userId as string
        if (token.error) {
          session.error = token.error as string
        }
      }
      return session
    },
  },
  secret: (() => {
    const secret = process.env.AUTH_SECRET || process.env.NEXTAUTH_SECRET || process.env.JWT_KEY
    const isBuildPhase = process.env.NEXT_PHASE === "phase-production-build"
    const isProduction = process.env.NODE_ENV === "production"

    if (!secret && isProduction && !isBuildPhase) {
      throw new Error(
        "FATAL: AUTH_SECRET or NEXTAUTH_SECRET must be set in production. " +
        "Generate with: openssl rand -base64 32"
      )
    }
    // Fallback only allowed in development or during Next.js build phase
    return secret || "devhunt-local-dev-secret-do-not-use-in-prod"
  })(),
  trustHost: true, // Ensure trustHost is set here as well
})
