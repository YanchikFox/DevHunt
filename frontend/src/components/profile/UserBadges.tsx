"use client"

import Image from "next/image"
import { useUserBadges, type UserAchievement } from "@/lib/api/queries/badges"
import { Award, Loader2 } from "lucide-react"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { getBadgeStyle } from "./badge-icons"
import { useTranslations } from "next-intl"

interface UserBadgesProps {
  userId: string
  compact?: boolean
}

function BadgeItem({ userBadge, compact }: { userBadge: UserAchievement; compact?: boolean }) {
  const style = getBadgeStyle(userBadge.achievement.code)
  const Icon = style.icon

  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger asChild>
          <div className={`flex flex-col items-center gap-2 ${compact ? "p-2" : "p-3"} rounded-[12px] transition-all duration-200 cursor-default hover:bg-bg-subtle group`}>
            {userBadge.achievement.iconUrl ? (
              <Image
                src={userBadge.achievement.iconUrl}
                alt={userBadge.achievement.title}
                width={compact ? 40 : 56}
                height={compact ? 40 : 56}
                className="rounded-full object-cover ring-2 ring-border shadow-sm"
              />
            ) : (
              <div className={`${compact ? "h-10 w-10" : "h-14 w-14"} flex items-center justify-center rounded-[14px] border border-primary/15 bg-primary/10 transition-transform duration-200 group-hover:scale-105`}>
                <Icon className={`${compact ? "h-5 w-5" : "h-7 w-7"} text-primary`} />
              </div>
            )}
            <span className="font-medium text-center leading-tight text-xs">
              {userBadge.achievement.title}
            </span>
            {!compact && userBadge.achievement.points > 0 && (
              <span className="rounded-full bg-primary/10 px-1.5 py-0.5 text-[10px] font-semibold text-primary">
                +{userBadge.achievement.points} pts
              </span>
            )}
          </div>
        </TooltipTrigger>
        <TooltipContent side="bottom" className="max-w-[200px]">
          <p className="font-semibold text-sm">{userBadge.achievement.title}</p>
          {userBadge.achievement.description && (
            <p className="text-xs text-muted-foreground mt-0.5">{userBadge.achievement.description}</p>
          )}
          <div className="flex items-center gap-2 mt-1.5 text-xs text-muted-foreground">
            {userBadge.achievement.points > 0 && <span>{userBadge.achievement.points} pts</span>}
            <span>{new Date(userBadge.earnedAt).toLocaleDateString()}</span>
          </div>
          {userBadge.progress != null && userBadge.progress < 100 && (
            <div className="mt-1.5">
              <progress value={userBadge.progress} max={100} className="h-1.5 w-full accent-primary" />
              <span className="text-[10px] text-muted-foreground">{userBadge.progress}%</span>
            </div>
          )}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  )
}

function getBadgeKey(userBadge: UserAchievement) {
  return userBadge.id || `${userBadge.achievementId}-${userBadge.earnedAt}`
}

export function UserBadges({ userId, compact }: UserBadgesProps) {
  const t = useTranslations()
  const { data: badges, isLoading } = useUserBadges(userId)
  const badgeList = Array.isArray(badges) ? badges : []

  if (isLoading) {
    return (
      <div className="flex justify-center p-4">
        <Loader2 className="h-4 w-4 animate-spin" />
      </div>
    )
  }

  if (badgeList.length === 0) {
    return (
      <section className="rounded-[14px] border border-border bg-card">
        <div className="border-b border-border px-4 py-3">
          <span className="caption">[{t("profile.achievements")}]</span>
        </div>
        <div className="p-4">
          <div className="flex flex-col items-center justify-center rounded-[12px] border border-dashed border-border bg-bg-subtle px-4 py-8">
            <Award className="h-10 w-10 text-muted-foreground/40 mb-3" />
            <p className="text-sm text-muted-foreground">{t("emptyStates.noAchievementsYet")}</p>
          </div>
        </div>
      </section>
    )
  }

  const byCategory = badgeList.reduce<Record<string, UserAchievement[]>>((acc, ub) => {
    const cat = ub.achievement.category ?? "other"
    if (!acc[cat]) acc[cat] = []
    acc[cat].push(ub)
    return acc
  }, {})

  return (
    <section className="rounded-[14px] border border-border bg-card">
      <div className="border-b border-border px-4 py-3">
        <div className="flex items-center justify-between">
          <span className="caption">[{t("profile.achievements")}]</span>
          <span className="chip text-[10px]">
            {t("badges.totalBadges", { count: badgeList.length })}
          </span>
        </div>
      </div>
      <div className="p-4">
        {compact ? (
          <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-6 gap-1">
            {badgeList.map((ub) => (
              <BadgeItem key={getBadgeKey(ub)} userBadge={ub} compact />
            ))}
          </div>
        ) : (
          <div className="space-y-4">
            {Object.entries(byCategory).map(([category, items]) => (
              <div key={category}>
                <h3 className="text-xs font-semibold uppercase text-muted-foreground tracking-wider mb-2">
                  {t(`badges.categories.${category}`)}
                </h3>
                <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 lg:grid-cols-6 gap-1">
                  {items.map((ub) => (
                    <BadgeItem key={getBadgeKey(ub)} userBadge={ub} />
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </section>
  )
}
