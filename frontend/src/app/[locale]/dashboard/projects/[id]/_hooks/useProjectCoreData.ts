"use client"

import { useMemo } from "react"
import { useProject, useProjectPermissions } from "@/lib/api/queries/projects"
import { useProjectBySlug } from "@/lib/api/queries/project-extras"
import { useTasks } from "@/lib/api/queries/tasks"
import {
  useCancelInvitation,
  useIncomingInvitations,
  useRespondToInvitation,
  useSendInvitation,
  useSentInvitations,
  useTeamMembers,
} from "@/lib/api/queries/teams"
import type { ProjectPermissions, ProjectWithExtras } from "./types"

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export function useProjectCoreData(finalProjectId: string, hasProjectId: boolean) {
  const isUuidParam = UUID_PATTERN.test(finalProjectId)
  const slugForLookup = hasProjectId && !isUuidParam ? finalProjectId : undefined
  const { data: projectFromSlug, isLoading: slugLookupLoading, error: slugLookupError } = useProjectBySlug(slugForLookup)

  const resolvedProjectId = isUuidParam ? finalProjectId : projectFromSlug?.id ?? ""
  const hasResolvedId = resolvedProjectId.length > 0

  const { data: project, isLoading: projectLoadingRaw, error: projectErrorRaw, refetch: refetchProject } =
    useProject(hasResolvedId ? resolvedProjectId : "")

  const projectLoading = (hasProjectId && !isUuidParam && slugLookupLoading) || projectLoadingRaw
  const projectError = projectErrorRaw
    ?? slugLookupError
    ?? (hasProjectId && !isUuidParam && !slugLookupLoading && !projectFromSlug ? new Error("Project not found") : null)

  const { data: standaloneTasks, isLoading: standaloneTasksLoading, error: standaloneTasksError } =
    useTasks(hasResolvedId ? resolvedProjectId : "")

  const projectTasksFallback = useMemo(() => {
    const projectTasksData = (project as ProjectWithExtras | undefined)?.tasks
    return Array.isArray(projectTasksData) ? projectTasksData : []
  }, [project])

  const tasks = useMemo(
    () => (Array.isArray(standaloneTasks) ? standaloneTasks : projectTasksFallback),
    [standaloneTasks, projectTasksFallback]
  )

  const { data: permissions } = useProjectPermissions(hasResolvedId ? resolvedProjectId : "")
  const castPermissions = permissions as ProjectPermissions | undefined
  const shouldFetchTeamPermissions = Boolean(permissions?.canManageTeam)

  const { data: teamMembers, isLoading: teamLoading, error: teamError } = useTeamMembers(
    hasResolvedId ? resolvedProjectId : "",
    { includePermissions: shouldFetchTeamPermissions }
  )

  const incomingInvitations = useIncomingInvitations()
  const sentInvitations = useSentInvitations()
  const respondToInvitation = useRespondToInvitation()
  const cancelInvitation = useCancelInvitation()
  const sendInvitation = useSendInvitation()

  return {
    isUuidParam,
    resolvedProjectId,
    hasResolvedId,
    slugLookupLoading,
    project,
    projectLoading,
    projectError,
    refetchProject,
    tasks,
    tasksLoading: projectLoading || standaloneTasksLoading,
    tasksError: standaloneTasksError || projectError,
    permissions: castPermissions,
    teamMembers,
    teamLoading,
    teamError,
    incomingInvitations,
    sentInvitations,
    respondToInvitation,
    cancelInvitation,
    sendInvitation,
  }
}
