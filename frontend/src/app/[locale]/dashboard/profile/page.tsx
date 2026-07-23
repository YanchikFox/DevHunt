"use client"

import { Card, CardContent } from "@/components/ui/card"
import { ProfileView, ProfileSkeleton } from "./profile-view"
import { useProfile } from "@/lib/api/queries/profile"

export default function ProfilePage() {
  const { data: profile, isLoading, error } = useProfile()

  if (isLoading) {
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
      <ProfileView profile={profile} isOwnProfile />
    </div>
  )
}
