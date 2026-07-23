"use client"

import { useEffect, useState } from "react"
import { useSession } from "next-auth/react"
import { useAuth } from "@/lib/api/queries/auth"
import { useProfile } from "@/lib/api/queries/profile"
import { useRealtimeNotifications } from "@/hooks/use-notifications"
import { useAuthRedirect } from "./useAuthRedirect"
import { useHandleLogout } from "./useHandleLogout"

/* ── Main composition hook ── */

export function useDashboardAuth() {
  const { data: session, status: sessionStatus } = useSession()
  const { data: user, isLoading } = useAuth()
  const { data: profile, isPending: profileLoading } = useProfile()
  const handleLogout = useHandleLogout()

  useRealtimeNotifications()

  const [mounted, setMounted] = useState(false)
  useEffect(() => { setMounted(true) }, [])

  useAuthRedirect(mounted, sessionStatus, isLoading, user, profileLoading, profile)

  const displayName =
    profile?.fullName || session?.user?.name || session?.user?.email || user?.email || "User"

  const isAuthLoading = sessionStatus === "loading" || profileLoading || !mounted

  return {
    session,
    user,
    isLoading,
    profile,
    mounted,
    handleLogout,
    displayName,
    isAuthLoading,
  }
}
