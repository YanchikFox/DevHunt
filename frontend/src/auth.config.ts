import type { NextAuthConfig } from "next-auth"
import { routing } from "@/i18n/routing"

/**
 * Route prefixes that require authentication.
 * Everything else is treated as public (accessible without login).
 */
const PROTECTED_PREFIXES = ["/dashboard", "/admin"]

/**
 * Configuration for NextAuth.
 * Defines pages, session strategy, cookies, and authorization callbacks.
 */
export const authConfig = {
  pages: {
    signIn: "/login",
  },
  providers: [],
  trustHost: true,
  session: {
    strategy: "jwt",
    maxAge: 7 * 24 * 60 * 60, // 7 days — match refresh token lifetime
    updateAge: 5 * 60, // 5 minutes — refresh session cookie on activity
  },
  cookies: {
    sessionToken: {
      name: "authjs.session-token",
      options: {
        httpOnly: true,
        sameSite: "lax",
        path: "/",
        secure: false,
      },
    },
  },
  callbacks: {
    /**
     * Authorization callback to protect routes.
     * Only routes matching PROTECTED_PREFIXES require authentication.
     * All other routes (home, community, projects, showcase, etc.) are public.
     */
    authorized({ auth, request }) {
      const { nextUrl } = request
      const isAuthenticated = Boolean(auth?.user)

      // Strip locale prefix to get the logical path
      let pathWithoutLocale = nextUrl.pathname
      for (const locale of routing.locales) {
        if (nextUrl.pathname.startsWith(`/${locale}/`) || nextUrl.pathname === `/${locale}`) {
          pathWithoutLocale = nextUrl.pathname.substring(`/${locale}`.length) || "/"
          break
        }
      }

      const isProtectedRoute = PROTECTED_PREFIXES.some(
        (prefix) => pathWithoutLocale === prefix || pathWithoutLocale.startsWith(`${prefix}/`)
      )

      // Public routes are always accessible
      if (!isProtectedRoute) {
        return true
      }

      // Protected routes require authentication
      return isAuthenticated
    },
  },
} satisfies NextAuthConfig
