"use client"

import { useEffect, useMemo, useState } from "react"
import { useProfileByUserId } from "@/lib/api/queries/profile"
import { useAuth } from "@/lib/api/queries/auth"
import { Card, CardContent } from "@/components/ui/card"
import { ProfileSkeleton, ProfileView } from "../profile-view"
import { SuggestedUsersPanel } from "@/components/profile/SuggestedUsersPanel"

type Params = Promise<{ userId: string }> | { userId: string }

/**
 * Page component for viewing another user's profile in the dashboard.
 *
 * @param props - Component props
 * @param props.params - Route parameters containing the userId
 */
export default function MemberProfilePage({ params }: { params: Params }) {
  const initialUserId = useMemo(() => {
    if ("then" in params && typeof params.then === "function") {
      return null
    }
    // At this point params is not a Promise — it is the resolved { userId: string } object.
    // We verified it has no `.then`, so the sync branch is safe to read directly.
    const syncParams = params as Exclude<Params, Promise<{ userId: string }>>
    return syncParams.userId
  }, [params])

  const [resolvedUserId, setResolvedUserId] = useState<string | null>(initialUserId)

  useEffect(() => {
    if ("then" in params && typeof params.then === "function") {
      params.then((value) => setResolvedUserId(value.userId))
    }
  }, [params])

  const finalUserId = resolvedUserId ?? initialUserId ?? ""
  const { data: profile, isLoading, error } = useProfileByUserId(finalUserId)
  const { data: currentUser } = useAuth()
  const isOwnProfile = !!currentUser && currentUser.id === finalUserId
  const relationVisibility = profile?.relationVisibility ?? "public"
  const isFollower = Boolean(profile?.isFollower)
  const isFollowing = Boolean(profile?.isFollowing)

  if (isLoading || !finalUserId) {
    return <ProfileSkeleton />
  }

  if (error || !profile) {
    return (
      <Card>
        <CardContent className="p-8 text-center">
          <p className="text-muted-foreground">
            {error instanceof Error ? error.message : "Failed to load profile"}
          </p>
        </CardContent>
      </Card>
    )
  }

  return (
    <div className="space-y-6 animate-in fade-in slide-in-from-bottom-4 duration-500">
      <ProfileView
        profile={profile}
        isOwnProfile={isOwnProfile}
        relationVisibility={relationVisibility}
        isFollower={isFollower}
        isFollowing={isFollowing}
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2" />
        <div className="lg:col-span-1">
          <SuggestedUsersPanel />
        </div>
      </div>
    </div>
  )
}
