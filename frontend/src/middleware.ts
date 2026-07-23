import NextAuth from "next-auth"
import createMiddleware from "next-intl/middleware"
import { NextResponse } from "next/server"
import { routing } from "./i18n/routing"
import { authConfig } from "./auth.config"

const intlMiddleware = createMiddleware(routing)
const { auth } = NextAuth(authConfig)

/**
 * Route prefixes that require authentication.
 * All other routes are public and accessible without login.
 */
const PROTECTED_PREFIXES = ["/dashboard", "/admin"]

/**
 * Middleware to handle authentication and internationalization.
 * Checks auth for protected routes, then applies i18n middleware.
 */
export default auth((req) => {
  const { pathname } = req.nextUrl
  const isAuthenticated = Boolean(req.auth?.user)

  // Strip locale prefix to get the logical path
  let logicalPath = pathname
  for (const locale of routing.locales) {
    if (pathname.startsWith(`/${locale}/`) || pathname === `/${locale}`) {
      logicalPath = pathname.substring(`/${locale}`.length) || "/"
      break
    }
  }

  const isProtectedRoute = PROTECTED_PREFIXES.some(
    (prefix) => logicalPath === prefix || logicalPath.startsWith(`${prefix}/`)
  )

  // Redirect unauthenticated users away from protected routes
  if (isProtectedRoute && !isAuthenticated) {
    // Determine locale prefix for the redirect URL
    const localeMatch = routing.locales.find(
      (locale) => pathname.startsWith(`/${locale}/`) || pathname === `/${locale}`
    )
    const loginPath = localeMatch ? `/${localeMatch}/login` : "/login"
    return NextResponse.redirect(new URL(loginPath, req.url))
  }

  return intlMiddleware(req)
})

/**
 * Configuration for the middleware matcher.
 * Excludes API routes, Next.js internal routes, static files, and SignalR hubs.
 */
export const config = {
  // Skip all paths that should not be internationalized
  matcher: ["/((?!api|_next|.*chatHub.*|.*notificationHub.*|.*\\..*).*)"],
}
