"use client"

import { useMemo } from "react";
import { useTranslations } from "next-intl"
import { useAuth } from "@/lib/api/queries/auth"
import { useProfile, useUserStats } from "@/lib/api/queries/profile"
import { useProjectsList } from "@/lib/api/queries/projects"
import { Link } from "@/i18n/routing"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { Plus, Sparkles, FolderKanban, TrendingUp, Users, Zap } from "lucide-react"
import { formatDistanceToNowStrict } from "date-fns"
import { GlobalFeed } from "@/components/profile/GlobalFeed"
import { useNotifications } from "@/lib/api/queries/notifications"
import { cn } from "@/lib/utils"

const STATUS_STYLES: Record<string, { bg: string; text: string }> = {
  idea:       { bg: "bg-info/15",       text: "text-info" },
  recruiting: { bg: "bg-warning/15",    text: "text-warning" },
  active:     { bg: "bg-success/15",    text: "text-success" },
  beta:       { bg: "bg-primary/10",    text: "text-primary" },
  release:    { bg: "bg-primary/10",    text: "text-primary" },
  completed:  { bg: "bg-muted",         text: "text-muted-foreground" },
}

function getStatusStyle(status: string) {
  return STATUS_STYLES[status.toLowerCase()] ?? { bg: "bg-muted", text: "text-muted-foreground" }
}

function formatStatus(status: string) {
  return status.replace(/_/g, " ").replace(/\b\w/g, c => c.toUpperCase())
}

function formatRelativeUpdated(value: string | undefined) {
  if (!value) return ""
  try {
    const str = formatDistanceToNowStrict(new Date(value), { addSuffix: false })
    return str.replace(/^(\d+) (second|minute|hour|day|week|month|year)s?$/,
      (_m, n: string, unit: string) => `${n}${unit[0]}`) + " ago"
  } catch {
    return ""
  }
}

function getGreeting() {
  const hour = new Date().getHours()
  if (hour < 12) return "Good morning"
  if (hour < 17) return "Good afternoon"
  return "Good evening"
}

function getDayLabel() {
  return new Date().toLocaleDateString("en-US", { weekday: "long", month: "short", day: "numeric", year: "numeric" })
}

export default function DashboardPage() {
  const t = useTranslations()
  const { data: user, isLoading: userLoading } = useAuth()
  const { data: profile } = useProfile()
  const { data: projects, isLoading: projectsLoading } = useProjectsList()
  const { data: userStats } = useUserStats(profile?.id)
  const { data: notificationsData } = useNotifications(false, 1, 4)
  const notifications = notificationsData?.data ?? []
  const unreadCount = notifications.filter((n) => !n.read).length

  const projectsPreview = useMemo(() => (projects ?? []).slice(0, 3), [projects])
  const firstName = user?.fullName?.split(" ")[0] || user?.email?.split("@")[0] || "there"

  if (userLoading || projectsLoading) {
    return (
      <div className="mx-auto max-w-[1280px] space-y-7 fade-in">
        <div className="space-y-2">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="h-10 w-72" />
          <Skeleton className="h-4 w-56" />
        </div>
        <div className="grid gap-5 lg:grid-cols-[1fr_320px]">
          <Skeleton className="h-[400px] rounded-[14px]" />
          <div className="space-y-4">
            <Skeleton className="h-40 rounded-[14px]" />
            <Skeleton className="h-48 rounded-[14px]" />
          </div>
        </div>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-[1280px] space-y-7 fade-in">
      {/* Greeting */}
      <div>
        <div className="caption mb-2">[{getDayLabel()}]</div>
        <h1 className="font-serif text-[38px] font-normal tracking-[-0.02em] leading-none text-foreground">
          {getGreeting()}, {firstName}.
        </h1>
        <p className="text-[14px] text-muted-foreground mt-2">
          {(projects?.length ?? 0)} projects active ·{" "}
          {(userStats?.followersCount ?? 0)} followers ·{" "}
          {t("dashboard.projectsOverview")}
        </p>
      </div>

      {/* Main grid */}
      <div className="grid gap-5 lg:grid-cols-[1fr_320px] lg:items-start">
        {/* Left column */}
        <div className="flex flex-col gap-5 min-w-0">

          {/* AI quick action card */}
          <div className="ai-border rounded-[14px]">
            <div className="ai-glow rounded-[14px] p-5">
              <div className="flex items-center gap-2.5 mb-3">
                <Sparkles className="h-3.5 w-3.5 text-primary" />
                <span className="caption">[AI ASSISTANT]</span>
              </div>
              <p className="text-[17px] font-semibold text-foreground mb-1.5">
                Your AI plan is ready to generate.
              </p>
              <p className="text-[13px] text-muted-foreground mb-4">
                Describe a project in a few sentences — get a staged roadmap, tech stack recommendation, and matched collaborators.
              </p>
              <Link href="/dashboard/projects">
                <Button variant="ghost" className="h-8 px-0 text-[12px] text-primary gap-1 hover:bg-transparent hover:text-primary/80">
                  Go to AI Plan →
                </Button>
              </Link>
            </div>
          </div>

          {/* My projects */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <span className="caption">[Your projects]</span>
              <Link href="/dashboard/projects">
                <Button variant="ghost" className="h-7 px-2 text-[11px] text-muted-foreground gap-1 hover:text-foreground">
                  View all →
                </Button>
              </Link>
            </div>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {projectsPreview.length === 0 ? (
                <div className="col-span-full rounded-[14px] border border-dashed border-border bg-muted/30 p-8 text-center">
                  <FolderKanban className="h-8 w-8 text-muted-foreground/30 mx-auto mb-3" />
                  <p className="text-[13px] text-muted-foreground mb-3">{t("dashboard.noProjectsYet")}</p>
                  <Link href="/dashboard/projects">
                    <Button size="sm" className="h-8 text-[12px] rounded-[8px]">
                      <Plus className="h-3.5 w-3.5 mr-1.5" />
                      {t("dashboard.createNewProject")}
                    </Button>
                  </Link>
                </div>
              ) : (
                projectsPreview.map(project => {
                  const s = getStatusStyle(project.status)
                  const boosts = project.boostsCount ?? 0
                  const openRoles = project.openRolesCount ?? 0
                  const members = project.teamSize || 1
                  const href = `/dashboard/projects/${project.slug || project.id}`
                  const updatedLabel = formatRelativeUpdated(project.updatedAt || project.createdAt)
                  return (
                    <Link
                      key={project.id}
                      href={href}
                      className="group rounded-[14px] border border-border bg-card p-4 flex flex-col gap-3 hover:border-primary/30 hover:-translate-y-0.5 hover:shadow-md transition-all duration-150"
                    >
                      <div className="flex items-center gap-3">
                        <div
                          className="h-9 w-9 rounded-[8px] flex items-center justify-center font-mono font-semibold text-[14px] text-white shrink-0"
                          style={{ background: `oklch(0.72 0.12 ${(project.title?.charCodeAt(0) ?? 65) * 7 % 360})` }}
                        >
                          {project.title?.[0] ?? "?"}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="text-[14px] font-semibold text-foreground truncate group-hover:text-primary transition-colors">
                            {project.title}
                          </p>
                          {project.slug && (
                            <p className="font-mono text-[10px] text-muted-foreground truncate">/{project.slug}</p>
                          )}
                        </div>
                        <span className={cn("inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-[10px] font-medium font-mono border border-transparent shrink-0", s.bg, s.text)}>
                          {formatStatus(project.status)}
                        </span>
                      </div>

                      {project.description && (
                        <p className="text-[12px] text-muted-foreground leading-[1.4] line-clamp-2 min-h-[34px]">
                          {project.description}
                        </p>
                      )}

                      {project.technologies && project.technologies.length > 0 && (
                        <div className="flex flex-wrap gap-1">
                          {project.technologies.slice(0, 3).map(tech => (
                            <span key={tech} className="chip text-[10px]">{tech}</span>
                          ))}
                        </div>
                      )}

                      <div className="mt-auto pt-2.5 border-t border-border flex items-center justify-between gap-3 text-[11px] text-muted-foreground">
                        <span className="flex items-center gap-1">
                          <Users className="h-3 w-3" />{members}
                        </span>
                        <span className="flex items-center gap-1">
                          <Zap className={cn("h-3 w-3", project.boostedByMe && "fill-current text-primary")} />
                          {boosts}
                        </span>
                        {openRoles > 0 && (
                          <span className="text-warning font-medium">{openRoles} open</span>
                        )}
                        {updatedLabel && (
                          <span className="font-mono ml-auto">{updatedLabel}</span>
                        )}
                      </div>
                    </Link>
                  )
                })
              )}
            </div>
          </div>

          {/* Community feed */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <span className="caption">[Community feed]</span>
              <Link href="/community">
                <Button variant="ghost" className="h-7 px-2 text-[11px] text-muted-foreground gap-1 hover:text-foreground">
                  Open →
                </Button>
              </Link>
            </div>
            <GlobalFeed variant="bare" />
          </div>
        </div>

        {/* Right rail */}
        <div className="flex flex-col gap-4 lg:sticky lg:top-24">
          {/* Stats */}
          <div className="rounded-[14px] border border-border bg-card p-4">
            <div className="caption mb-3">[Your week]</div>
            <div className="flex flex-col gap-2.5">
              {[
                { label: "Projects", value: userStats?.projectsCount ?? projects?.length ?? 0, icon: FolderKanban },
                { label: "Followers", value: userStats?.followersCount ?? 0, icon: Users },
                { label: "Following", value: userStats?.followingCount ?? 0, icon: TrendingUp },
              ].map(({ label, value, icon: Icon }) => (
                <div key={label} className="flex items-center justify-between">
                  <span className="flex items-center gap-2 text-[12px] text-muted-foreground">
                    <Icon className="h-3.5 w-3.5" />
                    {label}
                  </span>
                  <span className="text-[16px] font-semibold text-foreground tabular-nums">{value}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Notifications */}
          <div className="rounded-[14px] border border-border bg-card p-4">
            <div className="flex items-center justify-between mb-2.5">
              <span className="caption">[Notifications]</span>
              {unreadCount > 0 && (
                <span className="chip chip-accent text-[10px] px-1.5 py-[2px]">{unreadCount} new</span>
              )}
            </div>
            {notifications.length === 0 ? (
              <p className="py-4 text-[12px] text-muted-foreground">{t("notifications.noNotifications")}</p>
            ) : (
              <div className="flex flex-col">
                {notifications.slice(0, 4).map((n, i) => (
                  <div
                    key={n.id}
                    className={cn(
                      "flex gap-2.5 py-2.5",
                      i < Math.min(notifications.length, 4) - 1 && "border-b border-border"
                    )}
                  >
                    {!n.read ? (
                      <span className="mt-[7px] h-1.5 w-1.5 flex-shrink-0 rounded-full bg-primary" />
                    ) : (
                      <span className="mt-[7px] h-1.5 w-1.5 flex-shrink-0" />
                    )}
                    <div className="min-w-0 flex-1">
                      <p className="text-[12px] leading-snug text-foreground line-clamp-2">{n.title || n.message}</p>
                      <p className="mt-1 font-mono text-[10px] text-muted-foreground">
                        {new Date(n.createdAt).toLocaleDateString(undefined, { month: "short", day: "numeric" })}
                        {" · "}
                        {n.type}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            )}
            <Link href="/dashboard/notifications" className="block">
              <Button
                variant="ghost"
                className="mt-2 h-8 w-full justify-center text-[12px] text-muted-foreground hover:text-foreground"
              >
                View all notifications
              </Button>
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}
