"use client"

import { useState, useCallback, useMemo } from "react"
import Image from "next/image"
import { useTranslations } from "next-intl"
import { useSearchUsers } from "@/lib/api/queries/teams"
import { Button } from "@/components/ui/button"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { MapPin, Award, Eye, UserPlus2, Search } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton"
import { Link } from "@/i18n/routing"
import { FollowButton } from "@/components/profile/FollowButton"
import { SkillsChipsTypeahead } from "@/components/profile/SkillsChipsTypeahead"
import { useOnlineStatus } from "@/lib/api/queries/presence"
import { OnlineIndicator } from "@/components/ui/online-indicator"

interface User {
  id?: string
  name?: string
  email?: string
  avatar?: string
  bio?: string
  experienceLevel?: string
  timezone?: string
  skills?: string[]
}

const ALL_EXPERIENCE_OPTION = "all"

export default function TeamsPage() {
  const t = useTranslations()
  const [skills, setSkills] = useState<string[]>([])
  const [experienceLevel, setExperienceLevel] = useState<string>("")

  const { data: users, isLoading } = useSearchUsers({
    skills: skills.length > 0 ? skills : undefined,
    experienceLevel: experienceLevel || undefined,
  })

  const userIds = useMemo(
    () => (users ?? []).map((u: User) => u.id).filter((id): id is string => typeof id === "string" && id.length > 0),
    [users]
  )
  const { data: onlineStatus } = useOnlineStatus(userIds)

  const experienceSelectValue = experienceLevel || ALL_EXPERIENCE_OPTION

  const handleExperienceChange = useCallback((value: string) => {
    setExperienceLevel(value === ALL_EXPERIENCE_OPTION ? "" : value)
  }, [])

  const handleClearFilters = useCallback(() => { setSkills([]); setExperienceLevel("") }, [])

  return (
    <div className="space-y-6 fade-in">
      <div>
        <span className="caption mb-1.5 block">[Discover]</span>
        <h1 className="font-serif text-[32px] font-normal tracking-[-0.5px] leading-none text-foreground">
          {t("teams.searchMembers")}
        </h1>
        <p className="text-[14px] text-muted-foreground mt-2">
          {t("teamsPage.heroDescription")}
        </p>
      </div>

      {/* SEARCH AND FILTER SECTION */}
      <div className="rounded-[14px] border border-border bg-card p-4">
        <div className="flex flex-col md:flex-row items-end md:items-center gap-3">
          <div className="flex-1 w-full space-y-2">
            <span className="caption mb-1 block">Filter by Skills</span>
            <SkillsChipsTypeahead
              inputId="team-search-skills"
              label={t("profilePage.skills")}
              placeholder={t("placeholders.skillExample")}
              skills={skills}
              setSkills={setSkills}
              maxSkills={10}
              showHelpText={false}
              showLabel={false}
            />
          </div>

          <div className="w-full md:w-auto space-y-2">
            <span className="caption mb-1 block">Experience</span>
            <Select value={experienceSelectValue} onValueChange={handleExperienceChange}>
              <SelectTrigger className="w-full md:w-[180px] h-9 text-[12px] rounded-[10px]">
                <SelectValue placeholder={t("placeholders.experienceLevel")} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL_EXPERIENCE_OPTION}>{t("teamsPage.allLevels")}</SelectItem>
                <SelectItem value="junior">{t("teamsPage.junior")}</SelectItem>
                <SelectItem value="middle">{t("teamsPage.middle")}</SelectItem>
                <SelectItem value="senior">{t("teamsPage.senior")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {/* RESULTS GRID */}
      <div>
        {isLoading ? (
          <div className="grid gap-3.5 grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
              <div key={i} className="rounded-[14px] border border-border bg-card p-4 space-y-3">
                <div className="flex items-center gap-3">
                  <Skeleton className="h-10 w-10 rounded-[10px]" />
                  <div className="flex-1 space-y-1.5">
                    <Skeleton className="h-4 w-28" />
                    <Skeleton className="h-3 w-20" />
                  </div>
                </div>
                <Skeleton className="h-3 w-full" />
                <div className="flex gap-1">
                  <Skeleton className="h-5 w-14 rounded-full" />
                  <Skeleton className="h-5 w-12 rounded-full" />
                </div>
              </div>
            ))}
          </div>
        ) : users && users.length > 0 ? (
          <div className="grid gap-3.5 grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {users.filter(Boolean).map((user: User, index: number) => {
              const displayName =
                typeof user?.name === "string" && user.name.trim().length > 0
                  ? user.name
                  : typeof user?.email === "string" && user.email.length > 0
                    ? user.email
                    : `User ${index + 1}`
              const displayInitial = displayName.charAt(0)?.toUpperCase() ?? "?"
              const badgeExperience =
                typeof user?.experienceLevel === "string" ? user.experienceLevel : undefined
              const timeZone = typeof user?.timezone === "string" ? user.timezone : undefined
              const safeSkills: string[] = Array.isArray(user?.skills)
                ? user.skills.filter(
                  (skill: unknown): skill is string =>
                    typeof skill === "string" && skill.trim().length > 0
                )
                : []
              const userEmail = typeof user?.email === "string" ? user.email : "unknown@example.com"
              const userId = typeof user?.id === "string" && user.id.length > 0 ? user.id : null

              return (
                <div
                  key={userId ?? `${displayName}-${index}`}
                  className="group flex h-full flex-col rounded-[14px] border border-border bg-card p-4 gap-3 hover:border-primary/30 hover:-translate-y-0.5 hover:shadow-md transition-all duration-150"
                >
                  <div className="flex items-start gap-3">
                    <div className="relative shrink-0">
                      {user.avatar ? (
                        <Image
                          src={user.avatar}
                          alt={displayName}
                          width={40}
                          height={40}
                          className="h-10 w-10 rounded-[10px] object-cover"
                        />
                      ) : (
                        <div
                          className="flex h-10 w-10 items-center justify-center rounded-[10px] text-white font-mono font-semibold text-[15px]"
                          style={{ background: `oklch(0.72 0.14 ${(displayName.charCodeAt(0) ?? 65) * 7 % 360})` }}
                        >
                          {displayInitial}
                        </div>
                      )}
                      <OnlineIndicator isOnline={Boolean(userId && onlineStatus?.[userId])} size="md" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-[13px] font-medium text-foreground truncate group-hover:text-primary transition-colors">{displayName}</p>
                      <p className="text-[11px] text-muted-foreground truncate font-mono">{userEmail}</p>
                    </div>
                  </div>

                  {/* Bio */}
                  {typeof user?.bio === "string" && user.bio.trim().length > 0 && (
                    <p className="text-[11px] text-muted-foreground line-clamp-2">{user.bio}</p>
                  )}

                  <div className="flex flex-wrap items-center gap-1.5">
                    {badgeExperience && (
                      <span className="chip text-[10px] capitalize border-transparent bg-primary/10 text-primary">
                        <Award className="h-2.5 w-2.5" />
                        {badgeExperience}
                      </span>
                    )}
                    {timeZone && (
                      <span className="chip text-[10px]">
                        <MapPin className="h-2.5 w-2.5" />
                        {timeZone}
                      </span>
                    )}
                  </div>

                  <div className="flex flex-wrap gap-1">
                    {safeSkills.slice(0, 3).map((skill: string) => (
                      <span key={skill} className="chip text-[10px]">{skill}</span>
                    ))}
                    {safeSkills.length > 3 && (
                      <span className="chip text-[10px]">+{safeSkills.length - 3}</span>
                    )}
                  </div>

                  <div className="mt-auto grid grid-cols-2 gap-2 pt-2 border-t border-border">
                    {userId ? (
                      <>
                        <FollowButton
                          userId={userId}
                          initialIsFollowing={false}
                          initialIsFollower={false}
                          relationVisibility="public"
                          className="w-full"
                          variant="outline"
                        />
                        <Button size="sm" variant="outline" asChild className="w-full gap-1.5 h-8 text-[12px] rounded-[8px]">
                          <Link href={`/dashboard/profile/${userId}`}>
                            <Eye className="h-3 w-3" />
                            {t("teamsPage.viewProfile")}
                          </Link>
                        </Button>
                      </>
                    ) : (
                      <Button size="sm" variant="outline" disabled className="col-span-2 w-full gap-2 h-8 text-[12px] rounded-[8px]">
                        <UserPlus2 className="h-3 w-3" />
                        {t("teamsPage.follow")}
                      </Button>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        ) : (
          <div className="rounded-[14px] border border-dashed border-border bg-card p-12 text-center">
            <Search className="h-10 w-10 text-muted-foreground/30 mx-auto mb-3" />
            <h3 className="text-[15px] font-semibold text-foreground mb-1">{t("teamsPage.noUsersFound")}</h3>
            <p className="text-[13px] text-muted-foreground max-w-md mx-auto mb-4">
              {skills.length > 0 || experienceLevel
                ? t("teamsPage.tryChangingSearchParams")
                : t("teamsPage.startSearchingMembers")}
            </p>
            <button
              onClick={handleClearFilters}
              className="text-[12px] text-primary hover:underline font-medium"
            >
              Clear filters
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
