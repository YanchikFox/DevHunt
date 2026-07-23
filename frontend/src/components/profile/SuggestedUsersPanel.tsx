"use client"

import { Link } from "@/i18n/routing"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Star, BadgeCheck } from "lucide-react"
import { FollowButton } from "@/components/profile/FollowButton"
import { useSuggestedUsers } from "@/lib/api/queries/profile"
import { useTranslations } from "next-intl"

type SuggestedUser = NonNullable<ReturnType<typeof useSuggestedUsers>["data"]>[number]

/**
 * Props for the SuggestedUsersPanel component.
 */
export type SuggestedUsersPanelProps = {
  /** Whether the panel is visible */
  isVisible?: boolean
  /** Maximum number of suggestions to display */
  limit?: number
}

/**
 * Displays a list of suggested users to follow.
 * Shows user avatar, name, mutual projects count, and recent activity score.
 *
 * @example
 * ```tsx
 * <SuggestedUsersPanel isVisible={true} limit={5} />
 * ```
 */
export function SuggestedUsersPanel({ isVisible = true, limit = 6 }: SuggestedUsersPanelProps) {
  const t = useTranslations("suggestedUsers")
  const { data, isLoading, isError } = useSuggestedUsers(limit, Boolean(isVisible))

  if (!isVisible || isError || (!isLoading && (!data || !data.length))) {
    return null
  }

  return (
    <Card className="border-border/50 bg-card/40 backdrop-blur-sm shadow-sm max-h-[480px] flex flex-col">
      <CardHeader className="pb-3 border-b border-border/40 shrink-0">
        <CardTitle className="text-lg font-semibold leading-tight text-foreground">{t("title")}</CardTitle>
      </CardHeader>
      <CardContent className="px-6 pt-4 pb-4 flex-1 min-h-0 overflow-y-auto custom-scrollbar">
        {isLoading && <p className="text-sm leading-relaxed text-muted-foreground">{t("loadingRecommendations")}</p>}
        <ul  className="space-y-3">
          {(data ?? []).map((user) => (
            <SuggestedUserRow key={user.id} user={user} />
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function SuggestedUserRow({ user }: { user: SuggestedUser }) {
  const t = useTranslations("suggestedUsers")
  const displayName = user.name?.trim() || t("defaultUserName")
  const initial = displayName.charAt(0).toUpperCase()

  return (
    <li className="flex items-center justify-between gap-3 rounded-xl border border-border/40 bg-background/40 hover:bg-background/60 px-4 py-3 transition-all group">
      <Link
        href={`/dashboard/profile/${user.id}`}
        className="flex items-center gap-3 flex-1 min-w-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:rounded-lg"
      >
        <Avatar className="h-10 w-10 shrink-0 ring-2 ring-transparent group-hover:ring-primary/20 transition-all">
          {user.avatarUrl ? (
            <AvatarImage src={user.avatarUrl} alt={displayName} />
          ) : (
            <AvatarFallback className="text-sm font-semibold bg-muted text-muted-foreground">
              {initial}
            </AvatarFallback>
          )}
        </Avatar>
        <div className="flex-1 min-w-0">
          <p className="flex items-center gap-1 text-sm font-semibold text-foreground group-hover:text-primary transition-colors">
            <span className="truncate">{displayName}</span>
            {user.isVerified && (
              <BadgeCheck className="h-3.5 w-3.5 text-primary shrink-0" aria-label={t("verified")} />
            )}
          </p>
          <div className="flex items-center gap-2 text-xs text-muted-foreground mt-0.5">
            <span className="flex items-center gap-1">
              <Star className="h-3 w-3 text-amber-500 shrink-0" aria-hidden="true" />
              {user.recentActivityScore}
            </span>
            <span aria-hidden="true" className="text-border/60">•</span>
            <span>{t("mutual", { count: user.mutualProjectsCount })}</span>
          </div>
        </div>
      </Link>
      <FollowButton
        userId={user.id}
        initialIsFollowing={false}
        initialIsFollower={false}
        relationVisibility="public"
        className="h-9 w-9 shrink-0 p-0 rounded-full shadow-sm"
        size="icon"
      />
    </li>
  )
}
