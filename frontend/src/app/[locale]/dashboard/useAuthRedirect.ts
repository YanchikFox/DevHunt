"use client"

import { useEffect } from "react"
import { useRouter } from "@/i18n/routing"
import { USE_MOCKS } from "@/lib/feature-flags"
import type { CurrentUserResponse } from "@/lib/api/queries/auth"
import type { UserProfile } from "@/lib/api/queries/profile"

/**
 * Redirect to /login when authentication is missing.
 * Handles both mock-mode (useAuth) and real-mode (next-auth session + profile).
 */
export function useAuthRedirect(
  mounted: boolean,
  sessionStatus: string,
  isLoading: boolean,
  user: CurrentUserResponse | null | undefined,
  _profileLoading: boolean,
  _profile: UserProfile | undefined,
) {
  const router = useRouter()

  useEffect(() => {
    if (!mounted) return

    if (USE_MOCKS) {
      if (!isLoading && !user) router.push("/login")
      return
    }

    if (sessionStatus === "loading") return

    if (sessionStatus === "unauthenticated") {
      router.push("/login")
      return
    }

    // Session is authenticated — do NOT redirect based on profile state.
    // A failed or slow profile fetch is not the same as being unauthenticated.
    // useProfile failures should surface as an error in the UI, not a logout.
    // The Next.js middleware already blocks unauthenticated access to /dashboard.
  }, [mounted, isLoading, user, router, sessionStatus])
}
