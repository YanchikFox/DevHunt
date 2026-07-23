import { Metadata } from "next"
import { notFound, redirect } from "next/navigation"
import { cookies } from "next/headers"
import { UserProfileClient } from "./user-profile-client"

async function getViewerProfile() {
  const cookieStore = cookies()
  const cookieHeader = cookieStore.toString()
  if (!cookieHeader) return null

  try {
    const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://core-api:8080"
    const response = await fetch(`${API_URL}/api/profile/me`, {
      headers: {
        "Content-Type": "application/json",
        Cookie: cookieHeader,
      },
      cache: "no-store",
    })

    if (!response.ok) {
      return null
    }

    return response.json()
  } catch (error) {
    console.error("Failed to fetch viewer profile:", error)
    return null
  }
}

async function getUser(userId: string) {
  try {
    const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://core-api:8080"
    const cookieStore = cookies()
    const cookieHeader = cookieStore.toString()

    const response = await fetch(`${API_URL}/api/users/${userId}`, {
      headers: {
        "Content-Type": "application/json",
        ...(cookieHeader ? { Cookie: cookieHeader } : {}),
      },
      next: { revalidate: 300 },
    })

    if (!response.ok) {
      return null
    }

    return response.json()
  } catch (error) {
    console.error("Failed to fetch user:", error)
    return null
  }
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string; userId: string }>
}): Promise<Metadata> {
  const resolvedParams = await params
  const user = await getUser(resolvedParams.userId)

  const canonical = `/${resolvedParams.locale}/users/${resolvedParams.userId}`

  if (!user) {
    return {
      title: "User Not Found - DevHunt",
      alternates: {
        canonical,
      },
    }
  }

  const avatar = user.avatarUrl || user.AvatarUrl
  const ogImages = avatar ? [avatar] : ["/opengraph-image"]

  return {
    title: `${user.FullName || user.Email || "User"} - DevHunt Profile`,
    description: user.Bio || `Profile of ${user.FullName || "developer"} on DevHunt`,
    alternates: {
      canonical,
    },
    openGraph: {
      title: user.FullName || user.Email,
      description: user.Bio || "Developer profile on DevHunt",
      type: "profile",
      url: canonical,
      images: ogImages,
    },
    twitter: {
      card: "summary_large_image",
      title: user.FullName || user.Email,
      description: user.Bio || "Developer profile on DevHunt",
      images: ogImages,
    },
  }
}

export default async function UserProfilePage({
  params,
}: {
  params: Promise<{ locale: string; userId: string }>
}) {
  const resolvedParams = await params
  const [user, viewer] = await Promise.all([getUser(resolvedParams.userId), getViewerProfile()])

  if (!user) {
    notFound()
  }

  if (viewer) {
    const targetId = user.id ?? user.Id ?? resolvedParams.userId
    const viewerId = viewer.id ?? viewer.Id
    const localePrefix = resolvedParams.locale ? `/${resolvedParams.locale}` : ""
    if (viewerId === targetId) {
      redirect(`${localePrefix}/dashboard/profile`)
    }
    redirect(`${localePrefix}/dashboard/profile/${targetId}`)
  }

  // Transform API response to component format (API uses camelCase)
  const userData = {
    id: user.id ?? user.Id,
    name:
      user.fullName ||
      user.FullName ||
      user.email?.split("@")[0] ||
      user.Email?.split("@")[0] ||
      "User",
    email: user.email ?? user.Email ?? null,
    role: user.role ?? user.Role,
    bio: user.bio ?? user.Bio ?? null,
    timezone: user.timezone ?? user.Timezone ?? null,
    skills: Array.isArray(user.skills ?? user.Skills)
      ? (user.skills ?? user.Skills).map((s: string | { name?: string; Name?: string }) =>
          typeof s === "string" ? s : s.name ?? s.Name ?? ""
        )
      : [],
    experience: user.experience ?? user.Experience ?? null,
    rating: user.rating ?? user.Rating ?? null,
    avatar: user.avatarUrl ?? user.AvatarUrl ?? null,
    github: user.github ?? user.Github ?? null,
    linkedin: user.linkedin ?? user.Linkedin ?? null,
    website: user.website ?? user.Website ?? null,
    joinedDate: user.createdAt ?? user.CreatedAt,
    profileVisibility: user.profileVisibility ?? user.ProfileVisibility ?? "public",
    canViewFullProfile: user.canViewFullProfile ?? user.CanViewFullProfile ?? true,
    canRequestAccess: user.canRequestAccess ?? user.CanRequestAccess ?? false,
    visibilityMessage: user.visibilityMessage ?? user.VisibilityMessage ?? null,
    isVerified: user.isVerified ?? user.IsVerified ?? false,
    isActive: user.isActive ?? user.IsActive ?? true,
    updatedDate: user.updatedAt ?? user.UpdatedAt ?? null,
    lastLogin: user.lastLogin ?? user.LastLogin ?? null,
    relationVisibility: user.relationVisibility ?? user.RelationVisibility ?? "public",
    isFollower: Boolean(user.isFollower ?? user.IsFollower),
    isFollowing: Boolean(user.isFollowing ?? user.IsFollowing),
    activityVisibility: user.activityVisibility ?? user.ActivityVisibility ?? "public",
  }

  return <UserProfileClient user={userData} />
}
