"use client"

import { useState, useCallback } from "react"
import { Skeleton } from "@/components/ui/skeleton"
import type { UserProfile } from "@/lib/api/queries/profile"
import { useResolveSkills } from "@/lib/api/queries/skills"
import { UserBadges } from "@/components/profile/UserBadges"
import { UserActivityFeed } from "@/components/profile/UserActivityFeed"
import { RecentProjectsPanel } from "@/components/profile/RecentProjectsPanel"
import { useOnlineStatus } from "@/lib/api/queries/presence"
import { cn } from "@/lib/utils"
import { useTranslations } from "next-intl"
import {
  ProfileHeader,
  ProfileStatsBar,
  ProfileSkillsCard,
  ProfileProjectsCard,
} from "./profileSections"

// ── Pure helper ──────────────────────────────────────────────────

type GroupedItems = Array<{ category: string; items: string[] }>

function buildGroupedItems(
  rawItems: string[],
  resolved: Array<{ raw: string; name: string; category: string }> | undefined,
): GroupedItems {
  const byRaw = new Map<string, { name: string; category: string }>()
  for (const item of resolved ?? []) {
    byRaw.set(item.raw.trim().toLowerCase(), { name: item.name, category: item.category })
  }

  const grouped = new Map<string, Set<string>>()
  for (const raw of rawItems) {
    const trimmed = raw.trim()
    if (!trimmed) continue

    const resolvedItem = byRaw.get(trimmed.toLowerCase())
    const name = resolvedItem?.name ?? trimmed
    const category = resolvedItem?.category ?? "Other"

    const bucket = grouped.get(category) ?? new Set<string>()
    bucket.add(name)
    grouped.set(category, bucket)
  }

  const categories = Array.from(grouped.keys()).sort((a, b) => {
    if (a === "Other") return 1
    if (b === "Other") return -1
    return a.localeCompare(b)
  })

  return categories.map((category) => ({
    category,
    items: Array.from(grouped.get(category) ?? []).sort((a, b) => a.localeCompare(b)),
  }))
}

// ── Props ────────────────────────────────────────────────────────

type ProfileViewProps = {
  /** The user profile data to display */
  profile: UserProfile
  /** Whether the profile belongs to the current user */
  isOwnProfile?: boolean
  /** Visibility setting for relationships */
  relationVisibility?: string
  /** Whether the current user is a follower */
  isFollower?: boolean
  /** Whether the current user is following this profile */
  isFollowing?: boolean
}

type ProfileTab = "projects" | "badges" | "skills" | "activity"

const PROFILE_TABS: Array<{ value: ProfileTab; labelKey: string; namespace: "profile" | "activityFeed" }> = [
  { value: "projects", labelKey: "projects", namespace: "profile" },
  { value: "badges", labelKey: "achievements", namespace: "profile" },
  { value: "skills", labelKey: "skills", namespace: "profile" },
  { value: "activity", labelKey: "title", namespace: "activityFeed" },
]

// ── Main component ───────────────────────────────────────────────

/**
 * Component for displaying a user's profile details.
 * Includes header with avatar, basic info, skills, projects, and activity stats.
 */
export function ProfileView({
  profile,
  isOwnProfile = false,
  relationVisibility,
  isFollower,
  isFollowing,
}: Readonly<ProfileViewProps>) {
  const [activeTab, setActiveTab] = useState<ProfileTab>("projects")
  const skills = Array.isArray(profile.skills) ? profile.skills : []
  const { data: onlineStatus } = useOnlineStatus([profile.id])
  const resolvedSkillsQuery = useResolveSkills({ names: skills })
  const groupedSkills = buildGroupedItems(skills, resolvedSkillsQuery.data?.items)

  const relationVis = relationVisibility ?? profile.relationVisibility ?? "public"
  const follower = isFollower ?? Boolean(profile.isFollower)
  const following = isFollowing ?? Boolean(profile.isFollowing)
  const isOnline = Boolean(onlineStatus?.[profile.id])
  const activityVisibility = profile.activityVisibility ?? "public"

  const projectPreview =
    Array.isArray(profile.projects) && profile.projects.length > 0
      ? profile.projects.slice(0, 3)
      : []

  return (
    <div className="space-y-6">
      <ProfileHeader
        profile={profile}
        isOwnProfile={isOwnProfile}
        isOnline={isOnline}
        relationVis={relationVis}
        follower={follower}
        following={following}
      />

      <ProfileStatsBar profile={profile} />

      <ProfileTabs activeTab={activeTab} onTabChange={setActiveTab} />

      <div className="min-w-0">
        {activeTab === "projects" && (
          isOwnProfile ? <RecentProjectsPanel /> : <ProfileProjectsCard projects={projectPreview} />
        )}
        {activeTab === "badges" && <UserBadges userId={profile.id} />}
        {activeTab === "skills" && (
          <ProfileSkillsCard skills={skills} groupedSkills={groupedSkills} isOwnProfile={isOwnProfile} />
        )}
        {activeTab === "activity" && (
          <UserActivityFeed userId={profile.id} visibility={activityVisibility} isVisible />
        )}
      </div>
    </div>
  )
}

function ProfileTabs({
  activeTab,
  onTabChange,
}: {
  readonly activeTab: ProfileTab
  readonly onTabChange: (tab: ProfileTab) => void
}) {
  const tProfile = useTranslations("profile")
  const tActivity = useTranslations("activityFeed")

  return (
    <div className="flex gap-1 border-b border-border">
      {PROFILE_TABS.map((tab) => (
        <ProfileTabButton
          key={tab.value}
          tab={tab}
          label={tab.namespace === "profile" ? tProfile(tab.labelKey) : tActivity(tab.labelKey)}
          isActive={activeTab === tab.value}
          onTabChange={onTabChange}
        />
      ))}
    </div>
  )
}

function ProfileTabButton({
  tab,
  label,
  isActive,
  onTabChange,
}: {
  readonly tab: { value: ProfileTab }
  readonly label: string
  readonly isActive: boolean
  readonly onTabChange: (tab: ProfileTab) => void
}) {
  const handleClick = useCallback(() => onTabChange(tab.value), [onTabChange, tab.value])

  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        "border-b-2 px-4 py-2.5 text-[13px] font-medium capitalize transition-colors",
        isActive
          ? "border-primary text-foreground"
          : "border-transparent text-muted-foreground hover:text-foreground"
      )}
    >
      {label}
    </button>
  )
}

/**
 * Skeleton loader for the profile view.
 */
export function ProfileSkeleton() {
  return (
    <div className="space-y-6">
      <Skeleton className="h-48 w-full rounded-xl" />
      <div className="grid grid-cols-4 gap-4">
        <Skeleton className="h-20 w-full rounded-xl" />
        <Skeleton className="h-20 w-full rounded-xl" />
        <Skeleton className="h-20 w-full rounded-xl" />
        <Skeleton className="h-20 w-full rounded-xl" />
      </div>
      <div className="grid gap-6 md:grid-cols-3">
        <Skeleton className="h-96 w-full md:col-span-2 rounded-xl" />
        <Skeleton className="h-96 w-full rounded-xl" />
      </div>
    </div>
  )
}
