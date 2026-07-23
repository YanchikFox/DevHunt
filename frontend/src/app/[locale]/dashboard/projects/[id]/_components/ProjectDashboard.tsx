"use client"

import Image from "next/image"
import { useState, useEffect, useCallback } from "react";
import { useSearchParams } from "next/navigation"
import { motion, AnimatePresence } from "framer-motion"
import {
    Github,
    MoreVertical,
    Edit,
    Play,
    CheckCircle2,
    XCircle,
    Trash2,
    ArrowLeft,
    RefreshCw,
    Loader2,
    Unlink,
    Replace,
    Plus,
    Zap,
} from "lucide-react"
import { cn } from "@/lib/utils"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import { useToggleProjectBoost } from "@/lib/api/queries/project-extras"
import { useToast } from "@/hooks/use-toast"
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

// Tab Contents
import { OverviewTabContent, ActivityTabContent, TeamTabContent } from "@/components/projects/tabs"
import { ProjectOverview } from "@/components/projects/ProjectOverview"
import type { UserActivityFeedItem } from "@/lib/api/queries/profile"
import { ProjectDocs } from "@/components/projects/ProjectDocs"
import { ShowcaseEditor } from "@/components/projects/ShowcaseEditor"
import { ProjectTaskBoard } from "@/components/projects/ProjectTaskBoard"
import { ProjectIssuesTab } from "@/components/projects/ProjectIssuesTab"
import { ProjectChat } from "@/components/projects/ProjectChat"
import { GitHubRepoSelector } from "@/components/features/GitHubRepoSelector"

import type { ProjectDetailState } from "../_hooks/useProjectDetail"

interface ProjectNavItemConfig {
    value: string
    label: string
    badge?: number
}

function ProjectNavItem({ item, isActive, onTabChange }: { item: ProjectNavItemConfig; isActive: boolean; onTabChange: (value: TabValue) => void }) {
    const handleClick = useCallback(() => onTabChange(item.value as TabValue), [onTabChange, item.value])
    return (
        <button
            onClick={handleClick}
            className={cn(
                "border-b-2 px-3 py-2.5 text-[13px] font-medium capitalize transition-colors",
                isActive
                    ? "border-primary text-foreground"
                    : "border-transparent text-muted-foreground hover:text-foreground"
            )}
        >
            {item.label}
            {item.badge !== undefined && item.badge > 0 && (
                <span className="ml-1.5 font-mono text-[10px] text-muted-foreground">{item.badge}</span>
            )}
        </button>
    )
}

interface ProjectDashboardProps {
    detail: ProjectDetailState
}

type TabValue = "overview" | "tasks" | "team" | "docs" | "showcase" | "activity" | "issues" | "chat" | "media"


interface ProjectHeaderAreaProps {
  detail: ProjectDetailState
  canJoinTeam: boolean
  needsRepoConfig: boolean
  toggleBoostPending: boolean
  onToggleBoost: () => Promise<void>
  onSyncGithub: () => void
  onOpenRepoSelector: () => void
  onDisconnect: () => void
  onJoinTeam: () => void
  getStatusTranslationKey: (s: string) => string
}

function ProjectHeaderArea({ detail, canJoinTeam, needsRepoConfig, toggleBoostPending, onToggleBoost, onSyncGithub, onOpenRepoSelector, onDisconnect, onJoinTeam, getStatusTranslationKey }: ProjectHeaderAreaProps) {
  const { project, canEdit, canManageSettings, t, getStatusColor, githubIntegration, dialogs, statusActions, syncIntegrationPending, handleConnectGitHub, connectGitHubPending, disconnectGitHubPending, projectId } = detail
  if (!project) return null
  return (
    <div className="-mt-[52px] flex flex-wrap items-end gap-4">
      <div className="flex h-20 w-20 items-center justify-center rounded-[14px] border-4 border-background bg-primary/10 font-mono text-[36px] font-semibold text-primary shadow-md">
        {project.title?.[0] ?? "?"}
      </div>
      <div className="min-w-[240px] flex-1 pb-1">
        <div className="mb-2 flex flex-wrap items-center gap-2">
          <h1 className="m-0 text-[28px] font-semibold tracking-[-0.4px] text-foreground">{project.title}</h1>
          <Badge className={cn("px-2.5 py-0.5 text-xs font-semibold shadow-sm", getStatusColor(project.status))}>
            {t(`projects.${getStatusTranslationKey(project.status)}`)}
          </Badge>
          <span className="font-mono text-[11px] text-muted-foreground">/{project.slug || projectId.slice(0, 8)}</span>
        </div>
        <p className="max-w-[720px] text-[14px] leading-relaxed text-muted-foreground">{project.description}</p>
      </div>
      <div className="flex flex-wrap gap-2 pb-1">
        <Button variant={project.boostedByMe ? "default" : "outline"} size="sm" onClick={onToggleBoost} disabled={toggleBoostPending} aria-pressed={project.boostedByMe ?? false}
          className={cn("h-8 gap-1.5 rounded-[8px] text-[12px] tabular-nums", project.boostedByMe && "bg-primary/10 text-primary hover:bg-primary/15 border-primary/30")}>
          <Zap className={cn("h-3.5 w-3.5 transition-transform", project.boostedByMe && "fill-current", toggleBoostPending && "animate-pulse")} />
          <span>{project.boostsCount ?? 0}</span>
          <span className="hidden sm:inline ml-0.5">{project.boostedByMe ? (t("projects.boosted") || "Boosted") : (t("projects.boost") || "Boost")}</span>
        </Button>
        {githubIntegration && !needsRepoConfig && (
          <Button variant="outline" size="sm" onClick={onSyncGithub} disabled={syncIntegrationPending} className="h-8 gap-1.5 rounded-[8px] text-[12px]">
            {syncIntegrationPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RefreshCw className="h-3.5 w-3.5" />}
            {t("integrations.syncFromGitHub")}
          </Button>
        )}
        {githubIntegration && needsRepoConfig && (
          <Button variant="outline" size="sm" onClick={onOpenRepoSelector} className="h-8 gap-1.5 rounded-[8px] text-[12px]">
            <Github className="h-3.5 w-3.5" />{t("integrations.changeRepo")}
          </Button>
        )}
        {!githubIntegration && canManageSettings && handleConnectGitHub && (
          <Button variant="outline" size="sm" onClick={handleConnectGitHub} disabled={connectGitHubPending} className="h-8 gap-1.5 rounded-[8px] text-[12px]">
            {connectGitHubPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Github className="h-3.5 w-3.5" />}
            {t("integrations.connectGitHub")}
          </Button>
        )}
        {canJoinTeam && (
          <Button size="sm" onClick={onJoinTeam} className="h-8 gap-1.5 rounded-[8px] text-[12px]">
            <Plus className="h-3.5 w-3.5" />{t("teams.joinTeam")}
          </Button>
        )}
        {(canManageSettings || canEdit) && (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm" className="h-8 gap-1.5 rounded-[8px] text-[12px]">
                {t("common.actions")}<MoreVertical className="h-3.5 w-3.5 text-muted-foreground" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              {canEdit && (<DropdownMenuItem onClick={dialogs.handleOpenEditDialog}><Edit className="mr-2 h-4 w-4" />{t("common.edit")}</DropdownMenuItem>)}
              <DropdownMenuSeparator />
              {project.status === "draft" && (<DropdownMenuItem onClick={statusActions.handlePublishProject}><Play className="mr-2 h-4 w-4" />{t("projects.publish")}</DropdownMenuItem>)}
              {project.status === "recruiting" && (<DropdownMenuItem onClick={statusActions.handleActivateProject}><Play className="mr-2 h-4 w-4" />{t("projects.activate")}</DropdownMenuItem>)}
              {project.status === "active" && (<DropdownMenuItem onClick={statusActions.handleCompleteProject}><CheckCircle2 className="mr-2 h-4 w-4" />{t("projects.complete")}</DropdownMenuItem>)}
              {["draft", "recruiting", "active"].includes(project.status) && (<DropdownMenuItem onClick={statusActions.handleArchiveProject} className="text-orange-500 focus:text-orange-600"><XCircle className="mr-2 h-4 w-4" />{t("projects.archive")}</DropdownMenuItem>)}
              {project.status === "archived" && (<DropdownMenuItem onClick={statusActions.handleUnarchiveProject}><Play className="mr-2 h-4 w-4" />{t("projects.unarchive")}</DropdownMenuItem>)}
              {(project.status === "recruiting" || project.status === "active") && (<DropdownMenuItem onClick={statusActions.handleUnpublishProject}><ArrowLeft className="mr-2 h-4 w-4" />{t("projects.unpublish")}</DropdownMenuItem>)}
              {githubIntegration && !needsRepoConfig && (
                <><DropdownMenuSeparator />
                <DropdownMenuItem onClick={onOpenRepoSelector}><Replace className="mr-2 h-4 w-4" />{t("integrations.changeRepo")}</DropdownMenuItem>
                <DropdownMenuItem onClick={onDisconnect} disabled={disconnectGitHubPending} className="text-destructive focus:text-destructive"><Unlink className="mr-2 h-4 w-4" />{t("integrations.disconnect")}</DropdownMenuItem></>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={dialogs.handleOpenDeleteDialog} className="text-destructive focus:text-destructive"><Trash2 className="mr-2 h-4 w-4" />{t("projects.delete")}</DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </div>
    </div>
  )
}

interface ProjectTabContentProps {
  detail: ProjectDetailState
  activeTab: TabValue
}

function ProjectTabContent({ detail, activeTab }: ProjectTabContentProps) {
  const { project, canEdit, canManageTeam, canViewTasks, canEditTasks, t, dialogs, team, tasks, media, activity, projectId, permissions, teamLoading, currentUserId, tasksLoading, canManageSettings } = detail
  if (!project) return null
  return (
    <div className="mt-6 min-w-0">
      <AnimatePresence mode="wait">
        <motion.div key={activeTab} initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -10 }} transition={{ duration: 0.2 }} className="min-h-full">
          {activeTab === "overview" && (
            <div className="grid gap-6 xl:grid-cols-[1fr_300px]">
              <div className="flex flex-col gap-5">
                <ProjectOverview project={project} teamSize={team.projectTeam.length} isCurrentUserInTeam={team.isCurrentUserInTeam} currentUserId={currentUserId}
                  pendingMyJoinRequest={team.pendingMyJoinRequest} cancellingInvitationId={team.cancellingInvitationId}
                  onApplyToRole={team.onApplyToRole} onCancelInvitation={team.handleCancelInvitation}
                  canManageRoles={canManageSettings || canEdit} onManageRoles={dialogs.handleOpenEditDialog} />
                <ActivityPreview activities={activity.projectActivities} t={t} />
              </div>
              <ProjectRightRail project={project} teamMembers={team.projectTeam} t={t} />
            </div>
          )}
          {activeTab === "activity" && (
            <ActivityTabContent activities={activity.projectActivities} groupedActivities={activity.groupedProjectActivities}
              isFetching={activity.projectActivity.isFetching} hasNextPage={activity.projectActivity.hasNextPage}
              isFetchingNextPage={activity.projectActivity.isFetchingNextPage} onLoadMore={activity.handleLoadMoreActivity} />
          )}
          {activeTab === "tasks" && canViewTasks && (
            <div className="h-full">
              <ProjectTaskBoard columns={tasks.columns} tasksByColumn={tasks.tasksByColumn} isLoading={tasksLoading || tasks.columnsLoading} canEdit={canEditTasks}
                onAddColumn={tasks.handleOpenAddColumnDialog} onEditColumn={tasks.handleEditColumn} onDeleteColumn={tasks.handleRequestDeleteColumn}
                onCreateTask={tasks.handleCreateTaskInColumn} onEditTask={tasks.handleOpenTaskPanel} onDeleteTask={tasks.handleDeleteTaskClick}
                onMoveTask={tasks.handleMoveTask} onReorderTasks={tasks.handleReorderTasks} onReorderColumns={tasks.handleReorderColumns}
                headerStart={(
                  <><div><div className="caption mb-1 text-muted-foreground">[{t("tasks.title")}]</div><h2 className="text-xl font-semibold tracking-tight leading-none">{t("tasks.taskBoardTitle")}</h2></div>
                  {team.projectTeam.length > 0 && (<BoardTeamStack members={team.projectTeam} />)}</>
                )}
              />
            </div>
          )}
          {activeTab === "team" && (
            <TeamTabContent projectTeam={team.projectTeam} teamLoading={teamLoading} projectId={projectId} ownerId={project.ownerId} canManageTeam={canManageTeam}
              isCurrentUserInTeam={team.isCurrentUserInTeam} currentUserId={currentUserId} pendingJoinRequests={team.pendingJoinRequests}
              incomingInvitationsLoading={team.incomingInvitationsLoading} respondingInvitationId={team.respondingInvitationId}
              onRespondToJoinRequest={team.handleRespondToJoinRequest} pendingSentInvites={team.pendingSentInvites}
              sentInvitationsLoading={team.sentInvitationsLoading} cancellingInvitationId={team.cancellingInvitationId}
              onCancelInvitation={team.handleCancelInvitation} isInviteDialogOpen={dialogs.isInviteDialogOpen}
              setIsInviteDialogOpen={dialogs.setIsInviteDialogOpen} inviteEmail={team.inviteEmail} setInviteEmail={team.setInviteEmail}
              inviteRole={team.inviteRole} setInviteRole={team.setInviteRole} inviteMessage={team.inviteMessage}
              setInviteMessage={team.setInviteMessage} isInviting={team.isInviting} onSubmitInvite={team.onSubmitInvite}
              onCloseInviteDialog={dialogs.handleCloseInviteDialog} pendingMyJoinRequest={team.pendingMyJoinRequest} onOpenRequestDialog={team.openRequestDialog}
            />
          )}
          {activeTab === "chat" && <ProjectChat projectId={projectId} />}
          {activeTab === "docs" && <ProjectDocs projectId={projectId} canManage={canEdit} />}
          {activeTab === "showcase" && <ShowcaseEditor projectId={projectId} canManage={canEdit} />}
          {activeTab === "media" && (
            <OverviewTabContent project={project} teamSize={team.projectTeam.length} projectMedia={media.projectMedia} projectNewsData={media.projectNewsData ?? null}
              newsLoading={media.newsLoading} newsRefetching={media.newsRefetching} permissions={permissions ?? null}
              onOpenGalleryImage={media.handleOpenGalleryImage} onOpenUploadMediaDialog={media.handleOpenUploadMediaDialog}
              onOpenAddNewsDialog={media.handleOpenAddNewsDialog} onRefreshNews={media.handleRefreshNews}
              onDeleteNews={media.handleRequestDeleteNews} onTogglePinNews={media.handleTogglePinNews} onChangeNewsVisibility={media.handleChangeNewsVisibility}
            />
          )}
          {activeTab === "issues" && <ProjectIssuesTab projectId={projectId} />}
        </motion.div>
      </AnimatePresence>
    </div>
  )
}

export function ProjectDashboard({ detail }: ProjectDashboardProps) {
    const { project, githubIntegration, team, projectId, currentUserId, t, handleSyncIntegration, handleDisconnectGitHub, dialogs } = detail
    const [activeTab, setActiveTab] = useState<TabValue>("overview")
    const [repoSelectorOpen, setRepoSelectorOpen] = useState(false)
    const searchParams = useSearchParams()
    const toggleBoost = useToggleProjectBoost()
    const { toast } = useToast()

    const handleToggleBoost = useCallback(async () => {
        if (!projectId) return
        if (!currentUserId) {
            toast({ title: t("common.signInRequired") || "Sign in required", description: t("projects.signInToBoost") || "Sign in to boost this project", variant: "destructive" })
            return
        }
        try { await toggleBoost.mutateAsync(projectId) }
        catch (error) { toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("projects.boostFailed") || "Failed to boost project", variant: "destructive" }) }
    }, [toggleBoost, projectId, currentUserId, toast, t])

    useEffect(() => {
        if (searchParams.get("integration") === "success" && githubIntegration && !githubIntegration.config?.repository) {
            setRepoSelectorOpen(true)
        }
    }, [searchParams, githubIntegration])

    const needsRepoConfig = Boolean(githubIntegration && !githubIntegration.config?.repository)
    const connectedRepo = githubIntegration?.config?.repository as string | undefined
    const handleSyncGithub = useCallback(() => { if (handleSyncIntegration && githubIntegration) handleSyncIntegration(githubIntegration.id) }, [handleSyncIntegration, githubIntegration])
    const handleOpenRepoSelector = useCallback(() => setRepoSelectorOpen(true), [])
    const handleDisconnect = useCallback(() => { if (handleDisconnectGitHub) handleDisconnectGitHub() }, [handleDisconnectGitHub])
    const handleJoinTeam = useCallback(() => dialogs.setIsRequestDialogOpen(true), [dialogs])

    if (!project) return null

    const getStatusTranslationKey = (status: string) => {
        switch (status) {
            case "in_progress": return "inProgress"
            case "cancelled": return "cancelled"
            case "archived": return "archived"
            default: return status
        }
    }

    const canJoinTeam = Boolean(currentUserId) && !team.isCurrentUserInTeam && !detail.canManageSettings
    const navItems: ProjectNavItemConfig[] = [
        { value: "overview", label: t("projects.tabs.overview") },
        ...(detail.canViewTasks ? [{ value: "tasks", label: t("projects.tabs.board"), badge: detail.tasks.projectTasks.length || undefined }] : []),
        { value: "team", label: t("projects.tabs.team"), badge: team.projectTeam.length },
        { value: "chat", label: t("projects.tabs.chat") },
        { value: "activity", label: t("projects.tabs.activity") },
        { value: "media", label: t("projects.tabs.media") },
        { value: "showcase", label: t("projects.tabs.showcase") },
        { value: "docs", label: t("projects.tabs.docs") },
        { value: "issues", label: t("projects.tabs.issues") || "Issues" },
    ]

    return (
        <div className="min-h-[calc(100vh-8rem)] -mx-5 lg:-mx-7 -mt-5 lg:-mt-7 isolate">
            <div className="placeholder-stripe relative h-[140px] z-0">
                <div className="absolute inset-0 pointer-events-none" style={{ background: "linear-gradient(180deg, transparent 40%, oklch(var(--background)) 100%)" }} />
            </div>
            <div className={cn("relative z-10 mx-auto px-5 lg:px-7 pb-7 transition-[max-width] duration-300", activeTab === "tasks" ? "max-w-[1600px]" : "max-w-[1200px]")}>
                <ProjectHeaderArea detail={detail} canJoinTeam={canJoinTeam} needsRepoConfig={needsRepoConfig} toggleBoostPending={toggleBoost.isPending}
                    onToggleBoost={handleToggleBoost} onSyncGithub={handleSyncGithub} onOpenRepoSelector={handleOpenRepoSelector}
                    onDisconnect={handleDisconnect} onJoinTeam={handleJoinTeam} getStatusTranslationKey={getStatusTranslationKey} />
                <div className="mt-7 flex gap-1 overflow-x-auto border-b border-border">
                    {navItems.map((item) => (
                        <ProjectNavItem key={item.value} item={item} isActive={activeTab === item.value} onTabChange={setActiveTab} />
                    ))}
                </div>
                <ProjectTabContent detail={detail} activeTab={activeTab} />
            </div>
            {githubIntegration && (
                <GitHubRepoSelector open={repoSelectorOpen} onOpenChange={setRepoSelectorOpen} integrationId={githubIntegration.id} projectId={projectId} currentRepo={connectedRepo} />
            )}
        </div>
    )
}

function ActivityPreview({
    activities,
    t,
}: {
    readonly activities: UserActivityFeedItem[]
    readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
    const recent = activities.slice(0, 4)
    if (recent.length === 0) return null

    return (
        <section className="rounded-[14px] border border-border bg-bg-elevated">
            <div className="border-b border-border px-5 py-4">
                <span className="caption">[{t("projects.recentActivity")}]</span>
            </div>
            <div className="divide-y divide-border">
                {recent.map((item) => {
                    const initials = item.actorName.split(" ").map((n) => n[0]).join("").slice(0, 2).toUpperCase()
                    const hue = [...item.actorName].reduce((a, c) => a + c.charCodeAt(0), 0) % 360
                    const timeAgo = (() => {
                        const diff = Date.now() - new Date(item.createdAt).getTime()
                        const h = Math.floor(diff / 3600000)
                        const d = Math.floor(diff / 86400000)
                        if (d > 0) return `${d}d`
                        if (h > 0) return `${h}h`
                        return "just now"
                    })()
                    return (
                        <div key={item.id} className="flex items-start gap-3 px-5 py-3 text-[12px]">
                            {item.actorAvatarUrl ? (
                                <Image src={item.actorAvatarUrl} alt={item.actorName} width={24} height={24} className="mt-0.5 h-6 w-6 flex-shrink-0 rounded-full object-cover" />
                            ) : (
                                <div
                                    className="mt-0.5 flex h-6 w-6 flex-shrink-0 items-center justify-center rounded-full font-mono text-[10px] font-semibold text-white"
                                    style={{ background: `oklch(0.65 0.14 ${hue})` }}
                                >
                                    {initials}
                                </div>
                            )}
                            <p className="min-w-0 flex-1 leading-relaxed text-foreground">
                                <span className="font-medium">{item.actorName}</span>
                                {" "}
                                <span className="text-muted-foreground">{item.summary}</span>
                            </p>
                            <span className="flex-shrink-0 font-mono text-[11px] text-muted-foreground">{timeAgo}</span>
                        </div>
                    )
                })}
            </div>
        </section>
    )
}

type RailTeamMember = {
    id?: string
    Id?: string
    userId?: string
    UserId?: string
    fullName?: string
    FullName?: string
    role?: string
    Role?: string
    avatarUrl?: string
    AvatarUrl?: string
}

function MemberAvatar({ member }: { member: RailTeamMember }) {
    const name = member.fullName || member.FullName || "?"
    const avatar = member.avatarUrl || member.AvatarUrl
    const initials = name.split(" ").map((n) => n[0]).join("").slice(0, 2).toUpperCase()

    if (avatar) {
        return (
            <Image
                src={avatar}
                alt={name}
                width={28}
                height={28}
                className="h-7 w-7 rounded-full object-cover ring-1 ring-border"
            />
        )
    }
    return (
        <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 font-mono text-[11px] font-semibold text-primary ring-1 ring-border">
            {initials}
        </div>
    )
}

/**
 * Overlapping avatar stack — mirrors the mockup's board header "who's on this".
 * Shows first few members with a "+N" pill when more exist.
 */
function BoardTeamStack({ members }: { readonly members: readonly RailTeamMember[] }) {
    const visible = members.slice(0, 4)
    const overflow = members.length - visible.length
    return (
        <div className="flex items-center">
            <div className="flex items-center">
                {visible.map((member, i) => (
                    <div
                        key={member.id || member.Id || member.userId || member.UserId || i}
                        className="relative"
                        style={{ marginLeft: i === 0 ? 0 : -8, zIndex: visible.length - i }}
                        title={member.fullName || member.FullName}
                    >
                        <MemberAvatar member={member} />
                    </div>
                ))}
            </div>
            {overflow > 0 && (
                <span className="ml-1.5 font-mono text-[11px] text-muted-foreground">+{overflow}</span>
            )}
        </div>
    )
}

function ProjectRightRail({
    project,
    teamMembers,
    t,
}: {
    readonly project: NonNullable<ProjectDetailState["project"]>
    readonly teamMembers: RailTeamMember[]
    readonly t: (key: string, values?: Record<string, string | number>) => string
}) {
    const technologies = Array.isArray(project.technologies) ? project.technologies : []
    const lifecycle = ["draft", "recruiting", "active", "completed", "archived"] as const

    return (
        <aside className="space-y-3">
            <div className="rounded-[14px] border border-border bg-bg-elevated p-4">
                <div className="caption mb-3">[{t("projects.techStack")}]</div>
                <div className="flex flex-wrap gap-1.5">
                    {technologies.length > 0 ? (
                        technologies.map((technology) => (
                            <span key={technology} className="chip text-[10px]">{technology}</span>
                        ))
                    ) : (
                        <span className="text-[12px] text-muted-foreground">{t("common.notAvailable")}</span>
                    )}
                </div>
            </div>

            <div className="rounded-[14px] border border-border bg-bg-elevated p-4">
                <div className="caption mb-3">
                    [{t("projects.team")} · {teamMembers.length}{project.maxTeamSize ? `/${project.maxTeamSize}` : ""}]
                </div>
                {teamMembers.length > 0 ? (
                    <div className="flex flex-col gap-2">
                        {teamMembers.slice(0, 6).map((member) => {
                            const id = member.id || member.Id || member.userId || member.UserId || ""
                            const name = member.fullName || member.FullName || t("teams.unknownUser")
                            const role = member.role || member.Role || ""
                            return (
                                <div key={id} className="flex items-center gap-2.5">
                                    <MemberAvatar member={member} />
                                    <div className="min-w-0">
                                        <p className="truncate text-[12px] font-medium text-foreground">{name}</p>
                                        {role && (
                                            <p className="truncate font-mono text-[10px] capitalize text-muted-foreground">{role}</p>
                                        )}
                                    </div>
                                </div>
                            )
                        })}
                        {teamMembers.length > 6 && (
                            <p className="font-mono text-[11px] text-muted-foreground">+{teamMembers.length - 6} {t("teams.members")}</p>
                        )}
                    </div>
                ) : (
                    <p className="text-[12px] text-muted-foreground">{t("teams.noTeamMembersYet")}</p>
                )}
            </div>

            <div className="rounded-[14px] border border-border bg-bg-elevated p-4">
                <div className="caption mb-3">[{t("projects.lifecycle")}]</div>
                <div className="flex flex-col gap-0.5">
                    {lifecycle.map((status) => {
                        const active = status === project.status
                        return (
                            <div
                                key={status}
                                className={cn(
                                    "flex items-center gap-2 rounded-[8px] px-2 py-1.5 text-[12px]",
                                    active ? "bg-primary/10 text-primary font-medium" : "text-muted-foreground"
                                )}
                            >
                                <span className={cn("h-2 w-2 flex-shrink-0 rounded-full", active ? "bg-primary" : "bg-border-strong")} />
                                <span className="capitalize">{t(`projects.${status}`)}</span>
                            </div>
                        )
                    })}
                </div>
            </div>
        </aside>
    )
}
