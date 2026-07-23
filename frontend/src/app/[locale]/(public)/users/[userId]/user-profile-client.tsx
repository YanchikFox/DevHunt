"use client"

import { useCallback, useEffect, useMemo, useState } from "react"
import Image from "next/image"
import Link from "next/link"
import { useParams } from "next/navigation"
import { Button } from "@/components/ui/button"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"
import { useResolveSkills } from "@/lib/api/queries/skills"
import {
  MapPin,
  Calendar,
  Star,
  Github,
  MessageCircle,
  BadgeCheck,
} from "lucide-react";
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"
import { useToast } from "@/hooks/use-toast"
import { FollowButton } from "@/components/profile/FollowButton"
import { SuggestedUsersPanel } from "@/components/profile/SuggestedUsersPanel"
import { UserStatsPanel } from "@/components/profile/UserStatsPanel"
import { UserActivityFeed } from "@/components/profile/UserActivityFeed"
import { useUserStats } from "@/lib/api/queries/profile"
import { useProjectsList } from "@/lib/api/queries/projects"
import { UserBadges } from "@/components/profile/UserBadges"
import { useTranslations } from "next-intl"
import { useOnlineStatus } from "@/lib/api/queries/presence"
import { OnlineIndicator } from "@/components/ui/online-indicator"
import { cn } from "@/lib/utils"

type PublicUser = {
  id: string
  name: string
  email: string | null
  role: string
  bio: string | null
  timezone: string | null
  skills: string[]
  experience: number | null
  rating: number | null
  avatar: string | null
  github: string | null
  linkedin: string | null
  website: string | null
  joinedDate: string
  profileVisibility: string
  canViewFullProfile: boolean
  canRequestAccess: boolean
  visibilityMessage: string | null
  relationVisibility: string
  isFollower: boolean
  isFollowing: boolean
  activityVisibility?: string
  isVerified?: boolean
}

type GroupedSkill = { category: string; items: string[] }
type PortfolioQuery = ReturnType<typeof useProjectsList>

function UserProfileHeader({
  user, allowActivity, isOnline, onSendMessage, tProfile,
}: {
  user: PublicUser; allowActivity: boolean; isOnline: boolean
  onSendMessage: () => void; tProfile: ReturnType<typeof useTranslations>
}) {
  return (
    <div className="relative overflow-hidden rounded-[14px] border border-border bg-card">
      <div className="placeholder-stripe h-[140px] relative">
        <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-[oklch(var(--card))]" />
      </div>
      <div className="relative px-5 pb-5">
        <div className="flex flex-col sm:flex-row sm:items-end gap-5 -mt-[52px]">
          <div className="relative shrink-0">
            {user.avatar ? (
              <div className="h-20 w-20 rounded-[14px] border-4 border-card shadow-md overflow-hidden">
                <Image src={user.avatar} alt={user.name} width={80} height={80} className="h-full w-full object-cover" />
              </div>
            ) : (
              <div
                className="flex h-20 w-20 items-center justify-center rounded-[14px] border-4 border-card shadow-md text-white font-mono font-semibold text-[36px]"
                style={{ background: `oklch(0.72 0.14 ${(user.name?.charCodeAt(0) ?? 65) * 7 % 360})` }}
              >
                {user.name.charAt(0)}
              </div>
            )}
            <OnlineIndicator isOnline={isOnline} size="lg" />
          </div>
          <div className="flex-1 min-w-0 pb-1">
            <div className="flex items-center gap-2.5 mb-1">
              <h1 className="text-[28px] font-semibold tracking-[-0.4px] text-foreground">{user.name}</h1>
              {user.isVerified && <BadgeCheck className="h-5 w-5 text-primary" />}
              <span className="chip text-[11px] capitalize border-transparent bg-secondary">{user.role}</span>
            </div>
            <p className="text-[14px] text-muted-foreground">{user.bio || ""}</p>
            <div className="flex flex-wrap gap-3 mt-2 text-[12px] text-muted-foreground">
              {user.timezone && (
                <span className="flex items-center gap-1"><MapPin className="h-3 w-3" />{user.timezone}</span>
              )}
              <span className="flex items-center gap-1">
                <Calendar className="h-3 w-3" />
                {tProfile("joined")} {new Date(user.joinedDate).toLocaleDateString("en-US", { month: "short", year: "numeric" })}
              </span>
              {user.rating != null && (
                <span className="flex items-center gap-1">
                  <Star className="h-3 w-3 text-warning fill-warning" />{user.rating.toFixed(1)}
                </span>
              )}
            </div>
          </div>
          <div className="flex gap-2 shrink-0 pb-1">
            {user.github && (
              <a href={`https://github.com/${user.github}`} target="_blank" rel="noreferrer">
                <Button variant="outline" size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
                  <Github className="h-3 w-3" /> GitHub
                </Button>
              </a>
            )}
            {allowActivity && (
              <>
                <FollowButton
                  userId={user.id}
                  initialIsFollowing={user.isFollowing}
                  initialIsFollower={user.isFollower}
                  relationVisibility={user.relationVisibility}
                />
                <Button size="sm" onClick={onSendMessage} className="h-8 text-[12px] gap-1.5 rounded-[8px]">
                  <MessageCircle className="h-3 w-3" />
                  Message
                </Button>
              </>
            )}
            <ReportDialog targetType={REPORT_TARGET_TYPES.USER} targetId={user.id} />
          </div>
        </div>
      </div>
    </div>
  )
}

function ProfileOverviewTab({
  user, groupedSkills, portfolioQuery, activityVisibility, localePrefix,
  tEmptyStates, tProfile, tCommon, t,
}: {
  user: PublicUser; groupedSkills: GroupedSkill[]; portfolioQuery: PortfolioQuery
  activityVisibility: string | undefined; localePrefix: string
  tEmptyStates: ReturnType<typeof useTranslations>; tProfile: ReturnType<typeof useTranslations>
  tCommon: ReturnType<typeof useTranslations>; t: ReturnType<typeof useTranslations>
}) {
  return (
    <div className="grid gap-5 lg:grid-cols-[1fr_300px] mt-5">
      <div className="space-y-5">
        <div className="rounded-[14px] border border-border bg-card">
          <div className="px-4 py-3 border-b border-border">
            <span className="caption">[Skills]</span>
          </div>
          <div className="p-4">
            {user.skills.length ? (
              <div className="space-y-4">
                {groupedSkills.map((group) => (
                  <div key={group.category} className="space-y-2">
                    <p className="caption text-[10px]">{group.category}</p>
                    <div className="flex flex-wrap gap-1.5">
                      {group.items.map((skill) => (
                        <span key={`${group.category}:${skill}`} className="chip text-[11px]">{skill}</span>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-[13px] text-muted-foreground">{tEmptyStates("noSkillsYet")}</p>
            )}
          </div>
        </div>
        <UserBadges userId={user.id} compact />
        <div className="rounded-[14px] border border-border bg-card">
          <div className="px-4 py-3 border-b border-border">
            <span className="caption">[{tProfile("portfolio")}]</span>
          </div>
          <div className="p-4 space-y-2">
            {portfolioQuery.isLoading ? (
              <p className="text-[13px] text-muted-foreground">{tCommon("loading")}</p>
            ) : (portfolioQuery.data?.length ?? 0) > 0 ? (
              <>
                {portfolioQuery.data!.slice(0, 6).map((project) => {
                  const hue = (project.title?.charCodeAt(0) ?? 65) * 7 % 360
                  return (
                    <div key={project.id} className="group flex items-center gap-3 rounded-[10px] border border-border p-3 hover:border-primary/20 hover:bg-muted/20 transition-all">
                      <div
                        className="h-8 w-8 rounded-[8px] flex items-center justify-center text-white font-mono font-semibold text-[12px] shrink-0"
                        style={{ background: `oklch(0.72 0.12 ${hue})` }}
                      >
                        {project.title?.[0] ?? "?"}
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="text-[13px] font-medium text-foreground group-hover:text-primary transition-colors truncate">{project.title}</p>
                        {project.description && (
                          <p className="text-[11px] text-muted-foreground line-clamp-1">{project.description}</p>
                        )}
                      </div>
                      <Button variant="outline" size="sm" asChild className="h-7 text-[11px] rounded-[6px] shrink-0">
                        <Link href={`${localePrefix}/showcase/${project.id}`}>{tCommon("view")}</Link>
                      </Button>
                    </div>
                  )
                })}
                {portfolioQuery.data!.length > 6 && (
                  <div className="pt-1">
                    <Button variant="ghost" size="sm" asChild className="h-7 text-[11px] text-primary">
                      <Link href={`${localePrefix}/showcase`}>{t("userProfile.moreProjects")}</Link>
                    </Button>
                  </div>
                )}
              </>
            ) : (
              <p className="text-[13px] text-muted-foreground">{tEmptyStates("noShowcaseProjectsYet")}</p>
            )}
          </div>
        </div>
      </div>
      <div className="space-y-4">
        <SuggestedUsersPanel isVisible={Boolean(activityVisibility)} />
      </div>
    </div>
  )
}

export function UserProfileClient({ user }: Readonly<{ user: PublicUser }>) {
  const t = useTranslations()
  const tCommon = useTranslations("common")
  const tProfile = useTranslations("profile")
  const tEmptyStates = useTranslations("emptyStates")
  const { toast } = useToast()
  const params = useParams<{ locale: string }>()
  const localePrefix = params?.locale ? `/${params.locale}` : ""

  const profileVisibility = (user.profileVisibility || "public").toLowerCase()
  const rawActivityVisibility = (user.activityVisibility || user.relationVisibility || "public").toLowerCase()
  const canSeeProfile =
    profileVisibility === "public" || (profileVisibility === "followers" && user.isFollower)

  const statsAllowed = useMemo(() => {
    if (!canSeeProfile) return false
    if (profileVisibility === "followers") return user.isFollower
    return true
  }, [canSeeProfile, profileVisibility, user.isFollower])

  const activityVisibility = useMemo(() => {
    if (rawActivityVisibility === "public") return "public"
    if (rawActivityVisibility === "followers" && user.isFollower) return "followers"
    if (rawActivityVisibility === "private") return undefined
    return undefined
  }, [rawActivityVisibility, user.isFollower])

  const allowActivity = Boolean(activityVisibility)
  const statsQuery = useUserStats(user.id, statsAllowed)
  const portfolioQuery = useProjectsList({ ownerId: user.id, showcase: true, excludeDrafts: true })

  const tabs = useMemo(
    () => [
      { key: "overview", label: "Overview" },
      { key: "activity", label: "Activity", visible: allowActivity },
      { key: "stats", label: "Stats", visible: statsAllowed },
    ],
    [allowActivity, statsAllowed]
  )

  const visibleTabs = useMemo(() => tabs.filter((tab) => tab.visible !== false), [tabs])
  const defaultTab = visibleTabs[0]?.key ?? "overview"
  const [activeTab, setActiveTab] = useState(defaultTab)

  const handleTabClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
    const key = event.currentTarget.dataset.tabKey
    if (key) setActiveTab(key)
  }, [])

  useEffect(() => {
    if (!visibleTabs.some((tab) => tab.key === activeTab)) {
      setActiveTab(defaultTab)
    }
  }, [activeTab, defaultTab, visibleTabs])

  const handleSendMessage = useCallback(() => {
    toast({ title: t("toasts.comingSoon"), description: t("toasts.messagingAvailableSoon") })
  }, [toast, t])

  const { data: onlineStatus } = useOnlineStatus([user.id])
  const resolvedSkillsQuery = useResolveSkills({ names: user.skills })
  const groupedSkills = useMemo(() => {
    const byRaw = new Map<string, { name: string; category: string }>()
    for (const item of resolvedSkillsQuery.data?.items ?? []) {
      byRaw.set(item.raw.trim().toLowerCase(), { name: item.name, category: item.category })
    }
    const grouped = new Map<string, Set<string>>()
    for (const raw of user.skills ?? []) {
      const trimmed = String(raw ?? "").trim()
      if (!trimmed) continue
      const resolved = byRaw.get(trimmed.toLowerCase())
      const name = resolved?.name ?? trimmed
      const category = resolved?.category ?? "Other"
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
  }, [resolvedSkillsQuery.data?.items, user.skills])

  const tabContent = () => {
    if (activeTab === "activity" && activityVisibility) {
      return (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
          <div className="lg:col-span-2">
            <UserActivityFeed userId={user.id} visibility={activityVisibility} isVisible={Boolean(activityVisibility)} />
          </div>
          <div><SuggestedUsersPanel isVisible={Boolean(activityVisibility)} /></div>
        </div>
      )
    }
    if (activeTab === "stats" && statsAllowed) {
      return <UserStatsPanel stats={statsQuery.data ?? undefined} isVisible={statsQuery.isSuccess} />
    }
    return (
      <ProfileOverviewTab
        user={user} groupedSkills={groupedSkills} portfolioQuery={portfolioQuery}
        activityVisibility={activityVisibility} localePrefix={localePrefix}
        tEmptyStates={tEmptyStates} tProfile={tProfile} tCommon={tCommon} t={t}
      />
    )
  }

  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1">
        <div className="max-w-[1100px] mx-auto px-5 py-7 space-y-5">
          {!canSeeProfile ? (
            <div className="rounded-[14px] border border-border bg-card p-10 text-center">
              <h2 className="text-[18px] font-semibold text-foreground mb-2">{t("profilePage.profileIsPrivate")}</h2>
              <p className="text-[13px] text-muted-foreground">{t("userProfile.followToAccessProfile")}</p>
            </div>
          ) : (
            <>
              <UserProfileHeader
                user={user} allowActivity={allowActivity}
                isOnline={Boolean(onlineStatus?.[user.id])}
                onSendMessage={handleSendMessage} tProfile={tProfile}
              />
              <div className="flex gap-1 border-b border-border">
                {visibleTabs.map((tab) => (
                  <button
                    key={tab.key}
                    className={cn(
                      "px-3.5 py-2.5 text-[13px] font-medium capitalize transition-colors border-b-2",
                      activeTab === tab.key
                        ? "text-foreground border-primary"
                        : "text-muted-foreground border-transparent hover:text-foreground"
                    )}
                    data-tab-key={tab.key}
                    onClick={handleTabClick}
                  >
                    {tab.label}
                  </button>
                ))}
                {user.visibilityMessage && (
                  <span className="ml-auto text-[11px] text-muted-foreground self-center font-mono">
                    {user.visibilityMessage}
                  </span>
                )}
              </div>
              {tabContent()}
            </>
          )}
        </div>
      </main>
      <Footer />
    </div>
  )
}
