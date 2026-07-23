"use client"

import { useCallback } from "react"
import { signOut } from "next-auth/react"
import { useLogout } from "@/lib/api/queries/auth"

/**
 * Returns a stable callback that logs out via the backend API,
 * then clears the NextAuth session and redirects to "/".
 */
export function useHandleLogout() {
  const logout = useLogout()

  return useCallback(async () => {
    try {
      await logout.mutateAsync()
    } catch (error) {
      console.error("Logout API failed (continuing with signOut):", error)
    } finally {
      await signOut({ redirect: false })
      if (typeof window !== "undefined") {
        window.location.href = "/"
      }
    }
  }, [logout])
}
