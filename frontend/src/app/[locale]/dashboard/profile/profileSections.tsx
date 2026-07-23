"use client"

import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"
import {
  MapPin,
  Star,
  Github,
  Linkedin,
  Globe,
  Edit,
  Settings,
  ArrowRight,
} from "lucide-react"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import type { UserProfile } from "@/lib/api/queries/profile"
import { SendMessageButton } from "@/components/chat/SendMessageButton"
import { FollowButton } from "@/components/profile/FollowButton"
import { OnlineIndicator } from "@/components/ui/online-indicator"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"
import { useTranslations } from "next-intl"

// ── Pure helpers ─────────────────────────────────────────────────

export function getInitials(profile: UserProfile): string {
  if (profile.fullName) {
    return profile.fullName
      .split(" ")
      .map((n) => n[0])
      .join("")
      .toUpperCase()
      .slice(0, 2)
  }
  return profile.email[0]?.toUpperCase() ?? "U"
}

export function formatProfileDate(date?: string): string {
  if (!date) return "N/A"
  const parsed = new Date(date)
  if (Number.isNaN(parsed.getTime())) return "N/A"
  try {
    return parsed.toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" })
  } catch {
    return "N/A"
  }
}

export type GroupedItems = Array<{ category: string; items: string[] }>

// ── Profile Header Card ──────────────────────────────────────────

interface ProfileHeaderProps {
  readonly profile: UserProfile
  readonly isOwnProfile: boolean
  readonly isOnline: boolean
  readonly relationVis: string
  readonly follower: boolean
  readonly following: boolean
}

export function ProfileHeader({ profile, isOwnProfile, isOnline, relationVis, follower, following }: ProfileHeaderProps) {
  return (
    <div className="relative overflow-hidden rounded-[14px] border border-border bg-card">
      {/* Cover stripe */}
      <div className="placeholder-stripe h-[140px] relative">
        <div className="absolute inset-0 bg-gradient-to-b from-transparent via-transparent to-[oklch(var(--card))]" />
      </div>

      <div className="relative px-5 pb-5">
        <div className="flex flex-col gap-4">
          <div className="flex flex-col sm:flex-row sm:items-end gap-4 -mt-12">
            {/* Avatar */}
            <div className="relative shrink-0">
              <Avatar className="h-24 w-24 border-4 border-card shadow-md">
                <AvatarImage src={profile.avatarUrl} alt={profile.fullName || profile.email} className="object-cover" />
                <AvatarFallback className="text-3xl font-bold bg-primary/10 text-primary">
                  {getInitials(profile)}
                </AvatarFallback>
              </Avatar>
              <OnlineIndicator isOnline={isOnline} size="lg" />
            </div>

            {/* Info */}
            <div className="flex-1 min-w-0 pb-1">
              <h1 className="text-[26px] font-semibold tracking-[-0.3px] text-foreground truncate">
                {profile.fullName || "No name set"}
              </h1>
              <div className="text-[13px] text-muted-foreground">
                @{profile.email?.split("@")[0] ?? "user"}
                {profile.timezone && <> · {profile.timezone}</>}
              </div>
              {profile.bio && (
                <p className="text-[14px] text-foreground mt-2 max-w-[540px] leading-relaxed">
                  {profile.bio}
                </p>
              )}
              <div className="flex flex-wrap gap-4 mt-2.5 text-[12px] text-muted-foreground">
                <span>
                  <strong className="text-foreground">{profile.followersCount ?? 0}</strong> followers
                </span>
                <span>
                  <strong className="text-foreground">{profile.followingCount ?? 0}</strong> following
                </span>
                {profile.github && (
                  <span className="flex items-center gap-1">
                    <Github className="h-3 w-3" /> @{profile.github.split("/").pop()}
                  </span>
                )}
              </div>
            </div>

            {/* Action buttons */}
            <ProfileActions
              profile={profile}
              isOwnProfile={isOwnProfile}
              relationVis={relationVis}
              follower={follower}
              following={following}
            />
          </div>
        </div>
      </div>
    </div>
  )
}

// ── Header sub-parts ─────────────────────────────────────────────

function ProfileActions({
  profile, isOwnProfile, relationVis, follower, following,
}: {
  readonly profile: UserProfile
  readonly isOwnProfile: boolean
  readonly relationVis: string
  readonly follower: boolean
  readonly following: boolean
}) {
  const t = useTranslations("profile")

  if (isOwnProfile) {
    return (
      <div className="flex flex-wrap gap-2 sm:self-end sm:mb-1">
        <Link href="/dashboard/profile/edit">
          <Button variant="outline" size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
            <Edit className="h-3 w-3" />
            {t("editProfile")}
          </Button>
        </Link>
        <Link href="/dashboard/profile/privacy">
          <Button variant="outline" size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
            <Settings className="h-3 w-3" />
            {t("privacy")}
          </Button>
        </Link>
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-2 sm:self-end sm:mb-1">
      <FollowButton
        userId={profile.id}
        initialIsFollowing={following}
        initialIsFollower={follower}
        relationVisibility={relationVis}
      />
      <SendMessageButton
        recipientId={profile.id}
        recipientName={profile.fullName || profile.email}
        variant="outline"
        size="sm"
      />
      <ReportDialog targetType={REPORT_TARGET_TYPES.USER} targetId={profile.id} />
    </div>
  )
}

// ── Stats Bar ────────────────────────────────────────────────────

export function ProfileStatsBar({ profile }: { readonly profile: UserProfile }) {
  const tStats = useTranslations("stats")

  return (
    <div className="grid overflow-hidden rounded-[14px] border border-border bg-card sm:grid-cols-4">
      <StatCell value={profile.followersCount ?? 0} label={tStats("followers")} />
      <StatCell value={profile.followingCount ?? 0} label={tStats("following")} />
      <StatCell value={profile.projectsCount ?? 0} label={tStats("projects")} />
      <div className="flex flex-col items-center justify-center border-border p-4 max-sm:border-t sm:border-l">
        <span className="font-serif text-[26px] font-normal text-foreground tabular-nums flex items-center gap-1">
          {profile.rating?.toFixed(1) ?? "0.0"} <Star className="h-4 w-4 fill-warning text-warning" />
        </span>
        <span className="caption mt-1">Rating</span>
      </div>
    </div>
  )
}

function StatCell({ value, label }: { readonly value: number; readonly label: string }) {
  return (
    <div className="flex flex-col items-center justify-center border-border p-4 max-sm:border-t sm:border-l first:border-l-0 first:max-sm:border-t-0">
      <span className="font-serif text-[26px] font-normal text-foreground tabular-nums">
        {value}
      </span>
      <span className="caption mt-1">{label}</span>
    </div>
  )
}

// ── About Card ───────────────────────────────────────────────────

function ProfileEmptyState({ message, isOwnProfile, ctaLabel, ctaHref, buttonVariant = "outline", buttonSize = "h-8 text-[12px] rounded-[8px]" }: {
  message: string; isOwnProfile: boolean; ctaLabel: string; ctaHref: string
  buttonVariant?: "outline" | "ghost"; buttonSize?: string
}) {
  return (
    <div className="flex flex-col items-center justify-center py-6 px-4 border border-dashed border-border rounded-[10px] bg-muted/10">
      <p className="text-[13px] text-muted-foreground mb-3">{message}</p>
      {isOwnProfile && (
        <Button asChild size="sm" variant={buttonVariant} className={buttonSize}>
          <Link href={ctaHref}>{ctaLabel}</Link>
        </Button>
      )}
    </div>
  )
}

export function ProfileAboutCard({ profile, isOwnProfile }: { readonly profile: UserProfile; readonly isOwnProfile: boolean }) {
  const tCommon = useTranslations("common")
  const tEmptyStates = useTranslations("emptyStates")

  return (
    <div className="rounded-[14px] border border-border bg-card">
      <div className="px-4 py-3 border-b border-border">
        <span className="caption">[About]</span>
      </div>
      <div className="p-4 space-y-4">
        {profile.bio ? (
          <p className="text-[14px] leading-[1.6] text-foreground">{profile.bio}</p>
        ) : (
          <ProfileEmptyState message={tEmptyStates("noBioYet")} isOwnProfile={isOwnProfile} ctaLabel={tCommon("addBio")} ctaHref="/dashboard/profile/edit" />
        )}

        <SocialLinks profile={profile} />
      </div>
    </div>
  )
}

function SocialLinks({ profile }: { readonly profile: UserProfile }) {
  const hasLinks = profile.github || profile.linkedin || profile.website
  if (!profile.timezone && !hasLinks) return null

  return (
    <div className="flex flex-wrap gap-2 pt-3 border-t border-border">
      {profile.timezone && (
        <span className="chip text-[11px]">
          <MapPin className="h-3 w-3" />
          {profile.timezone}
        </span>
      )}
      {profile.github && (
        <a href={profile.github} target="_blank" rel="noopener noreferrer" className="chip text-[11px] hover:border-primary/30 transition-colors">
          <Github className="h-3 w-3" /> GitHub
        </a>
      )}
      {profile.linkedin && (
        <a href={profile.linkedin} target="_blank" rel="noopener noreferrer" className="chip text-[11px] hover:border-primary/30 transition-colors">
          <Linkedin className="h-3 w-3" /> LinkedIn
        </a>
      )}
      {profile.website && (
        <a href={profile.website} target="_blank" rel="noopener noreferrer" className="chip text-[11px] hover:border-primary/30 transition-colors">
          <Globe className="h-3 w-3" /> Website
        </a>
      )}
    </div>
  )
}

// ── Skills Card ──────────────────────────────────────────────────

interface SkillsCardProps {
  readonly skills: string[]
  readonly groupedSkills: GroupedItems
  readonly isOwnProfile: boolean
}

export function ProfileSkillsCard({ skills, groupedSkills, isOwnProfile }: SkillsCardProps) {
  const t = useTranslations("profile")
  const tCommon = useTranslations("common")
  const tEmptyStates = useTranslations("emptyStates")

  return (
    <div className="rounded-[14px] border border-border bg-card">
      <div className="px-4 py-3 border-b border-border">
        <span className="caption">[{t("skills")}]</span>
      </div>
      <div className="p-4">
        {skills.length > 0 ? (
          <div className="space-y-4">
            {groupedSkills.map((group) => (
              <div key={group.category} className="space-y-2">
                <p className="caption text-[10px]">
                  {group.category}
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {group.items.map((skill) => (
                    <span
                      key={`${group.category}:${skill}`}
                      className="chip text-[11px]"
                    >
                      {skill}
                    </span>
                  ))}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <ProfileEmptyState message={tEmptyStates("noSkillsYet")} isOwnProfile={isOwnProfile} ctaLabel={tCommon("addSkills")} ctaHref="/dashboard/profile/edit" buttonVariant="ghost" buttonSize="h-7 text-[11px]" />
        )}
      </div>
    </div>
  )
}

// ── Projects Preview Card ────────────────────────────────────────

interface ProjectPreview {
  readonly id: string
  readonly title?: string
  readonly description?: string
}

export function ProfileProjectsCard({ projects }: { readonly projects: ProjectPreview[] }) {
  const t = useTranslations("profile")

  if (projects.length === 0) return null

  return (
    <div className="rounded-[14px] border border-border bg-card">
      <div className="px-4 py-3 border-b border-border flex items-center justify-between">
        <span className="caption">[{t("projects")}]</span>
        <Link href="/dashboard/projects" className="text-muted-foreground hover:text-primary transition-colors">
          <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </div>
      <div className="p-4 space-y-2">
        {projects.map((project, idx) => (
          <Link href={`/dashboard/projects/${project.id}`} key={project.id ?? idx}>
            <div className="group flex items-center gap-3 rounded-[10px] border border-border p-3 hover:border-primary/20 hover:bg-muted/20 transition-all cursor-pointer">
              <div className="h-8 w-8 rounded-[8px] flex items-center justify-center bg-primary/10 font-mono font-semibold text-[12px] text-primary shrink-0">
                {project.title?.[0] ?? "?"}
              </div>
              <div className="min-w-0 flex-1">
                <h4 className="text-[13px] font-medium text-foreground group-hover:text-primary transition-colors truncate">
                  {project.title || "Untitled"}
                </h4>
                {project.description && (
                  <p className="text-[11px] text-muted-foreground truncate">{project.description}</p>
                )}
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
