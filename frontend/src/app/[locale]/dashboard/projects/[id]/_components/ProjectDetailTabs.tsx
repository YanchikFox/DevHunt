"use client"

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { OverviewTabContent, ActivityTabContent, TeamTabContent } from "@/components/projects/tabs"
import { ProjectDocs } from "@/components/projects/ProjectDocs"
import { ShowcaseEditor } from "@/components/projects/ShowcaseEditor"
import { ProjectTaskBoard } from "@/components/projects/ProjectTaskBoard"
import type { ProjectDetailState } from "../_hooks/useProjectDetail"

interface ProjectDetailTabsProps {
  detail: ProjectDetailState
}

export function ProjectDetailTabs({ detail }: ProjectDetailTabsProps) {
  const {
    t,
    project,
    projectId,
    permissions,
    canEdit,
    canManageTeam,
    canViewTasks,
    canEditTasks,
    team,
    tasks,
    media,
    activity,
    dialogs,
    teamLoading,
    currentUserId,
    tasksLoading,
  } = detail

  if (!project) return null

  const tabsClassName = `grid w-full rounded-xl bg-muted/40 p-1 shadow-inner ${canViewTasks ? "grid-cols-7" : "grid-cols-6"
    }`

  return (
    <Tabs defaultValue="overview" className="space-y-4">
      <TabsList className={tabsClassName}>
        <TabsTrigger
          value="overview"
          className="rounded-lg data-[state=active]:bg-background data-[state=active]:shadow-sm"
        >
          {t("projects.tabs.overview")}
        </TabsTrigger>
        <TabsTrigger
          value="activity"
          className="rounded-lg data-[state=active]:bg-background data-[state=active]:shadow-sm"
        >
          {t("projects.tabs.activity")}
        </TabsTrigger>
        <TabsTrigger
          value="team"
          className="rounded-lg data-[state=active]:bg-background data-[state=active]:shadow-sm"
        >
          {t("projects.tabs.team", { count: team.projectTeam.length })}
        </TabsTrigger>
        <TabsTrigger
          value="docs"
          className="rounded-lg data-[state=active]:bg-background data-[state=active]:shadow-sm"
        >
          {t("projects.tabs.docs")}
        </TabsTrigger>
        <TabsTrigger
          value="showcase"
          className="rounded-lg data-[state=active]:bg-background data-[state=active]:shadow-sm"
        >
          {t("projects.tabs.showcase")}
        </TabsTrigger>
        {canViewTasks && (
          <TabsTrigger
            value="tasks"
            className="rounded-lg data-[state=active]:bg-background data-[state=active]:shadow-sm"
          >
            {t("projects.tabs.tasks", { count: tasks.projectTasks.length })}
          </TabsTrigger>
        )}
      </TabsList>

      <TabsContent value="overview" className="space-y-4">
        <OverviewTabContent
          project={project}
          teamSize={team.projectTeam.length}
          projectMedia={media.projectMedia}
          projectNewsData={media.projectNewsData ?? null}
          newsLoading={media.newsLoading}
          newsRefetching={media.newsRefetching}
          permissions={permissions ?? null}
          onOpenGalleryImage={media.handleOpenGalleryImage}
          onOpenUploadMediaDialog={media.handleOpenUploadMediaDialog}
          onOpenAddNewsDialog={media.handleOpenAddNewsDialog}
          onRefreshNews={media.handleRefreshNews}
          onDeleteNews={media.handleRequestDeleteNews}
          onTogglePinNews={media.handleTogglePinNews}
          onChangeNewsVisibility={media.handleChangeNewsVisibility}
        />
      </TabsContent>

      <TabsContent value="activity" className="space-y-4" id="activity-section">
        <ActivityTabContent
          activities={activity.projectActivities}
          groupedActivities={activity.groupedProjectActivities}
          isFetching={activity.projectActivity.isFetching}
          hasNextPage={activity.projectActivity.hasNextPage}
          isFetchingNextPage={activity.projectActivity.isFetchingNextPage}
          onLoadMore={activity.handleLoadMoreActivity}
        />
      </TabsContent>

      <TabsContent value="team" className="space-y-4" id="team-section">
        <TeamTabContent
          projectTeam={team.projectTeam}
          teamLoading={teamLoading}
          projectId={projectId}
          ownerId={project.ownerId}
          canManageTeam={canManageTeam}
          isCurrentUserInTeam={team.isCurrentUserInTeam}
          currentUserId={currentUserId}
          pendingJoinRequests={team.pendingJoinRequests}
          incomingInvitationsLoading={team.incomingInvitationsLoading}
          respondingInvitationId={team.respondingInvitationId}
          onRespondToJoinRequest={team.handleRespondToJoinRequest}
          pendingSentInvites={team.pendingSentInvites}
          sentInvitationsLoading={team.sentInvitationsLoading}
          cancellingInvitationId={team.cancellingInvitationId}
          onCancelInvitation={team.handleCancelInvitation}
          isInviteDialogOpen={dialogs.isInviteDialogOpen}
          setIsInviteDialogOpen={dialogs.setIsInviteDialogOpen}
          inviteEmail={team.inviteEmail}
          setInviteEmail={team.setInviteEmail}
          inviteRole={team.inviteRole}
          setInviteRole={team.setInviteRole}
          inviteMessage={team.inviteMessage}
          setInviteMessage={team.setInviteMessage}
          isInviting={team.isInviting}
          onSubmitInvite={team.onSubmitInvite}
          onCloseInviteDialog={dialogs.handleCloseInviteDialog}
          pendingMyJoinRequest={team.pendingMyJoinRequest}
          onOpenRequestDialog={team.openRequestDialog}
        />
      </TabsContent>

      <TabsContent value="docs" className="space-y-4">
        <ProjectDocs projectId={projectId} canManage={canEdit} />
      </TabsContent>

      <TabsContent value="showcase" className="space-y-4">
        <ShowcaseEditor projectId={projectId} canManage={canEdit} />
      </TabsContent>

      {canViewTasks && (
        <TabsContent value="tasks" className="space-y-4" id="tasks-section">
          <Card className="border border-border/70 bg-card/80 shadow-sm">
            <CardHeader className="pb-2">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <CardTitle>{t("tasks.title")}</CardTitle>
                  <CardDescription>{t("tasks.planPrioritizeAndTrack")}</CardDescription>
                </div>
              </div>
            </CardHeader>
            <CardContent>
              <ProjectTaskBoard
                columns={tasks.columns}
                tasksByColumn={tasks.tasksByColumn}
                isLoading={tasksLoading || tasks.columnsLoading}
                canEdit={canEditTasks}
                onAddColumn={tasks.handleOpenAddColumnDialog}
                onEditColumn={tasks.handleEditColumn}
                onDeleteColumn={tasks.handleRequestDeleteColumn}
                onCreateTask={tasks.handleCreateTaskInColumn}
                onEditTask={tasks.handleOpenTaskPanel}
                onDeleteTask={tasks.handleDeleteTaskClick}
                onMoveTask={tasks.handleMoveTask}
                onReorderTasks={tasks.handleReorderTasks}
                onReorderColumns={tasks.handleReorderColumns}
              />
            </CardContent>
          </Card>
        </TabsContent>
      )}
    </Tabs>
  )
}
