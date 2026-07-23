"use client"

import { useAuth } from "@/lib/api/queries/auth"
import { useProfile } from "@/lib/api/queries/profile"
import { USE_MOCKS } from "@/lib/feature-flags"

/**
 * Hook to get the current authenticated user's profile.
 * Abstracts away the difference between mock mode and real API mode.
 *
 * @returns An object containing the user profile, loading state, and error state.
 */
export function useCurrentUser() {
  const authQuery = useAuth()
  const profileQuery = useProfile()

  if (USE_MOCKS) {
    return {
      user: authQuery.data,
      isLoading: authQuery.isLoading,
      isError: authQuery.isError,
    }
  }

  return {
    user: profileQuery.data,
    isLoading: profileQuery.isLoading,
    isError: profileQuery.isError,
  }
}
