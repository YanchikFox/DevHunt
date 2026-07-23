"use client"

import { useCallback, useMemo, useState } from "react"
import type { Invitation } from "@/lib/api/schema"
import type { ProjectMemberPermission } from "@/components/projects/ProjectPermissionsPanel"
import type { ApiError, ProjectWithExtras, TeamMember, ToastFn, TranslateFn } from "./types"
import type {
  useRespondToInvitation,
  useCancelInvitation,
  useSendInvitation,
} from "@/lib/api/queries/teams"

type InvitationsQuery = {
  data?: {
    items?: Invitation[]
  }
  isLoading: boolean
}

type RespondToInvitationMutation = ReturnType<typeof useRespondToInvitation>
type CancelInvitationMutation = ReturnType<typeof useCancelInvitation>
type SendInvitationMutation = ReturnType<typeof useSendInvitation>

interface UseProjectDetailTeamOptions {
  project: ProjectWithExtras | undefined
  teamMembers: TeamMember[] | undefined
  incomingInvitations: InvitationsQuery
  sentInvitations: InvitationsQuery
  respondToInvitation: RespondToInvitationMutation
  cancelInvitation: CancelInvitationMutation
  sendInvitation: SendInvitationMutation
  projectId: string
  currentUserId?: string
  refetchProject: () => Promise<unknown>
  t: TranslateFn
  toast: ToastFn
  setIsInviteDialogOpen: (open: boolean) => void
  setIsRequestDialogOpen: (open: boolean) => void
}

export function useProjectDetailTeam({
  project,
  teamMembers,
  incomingInvitations,
  sentInvitations,
  respondToInvitation,
  cancelInvitation,
  sendInvitation,
  projectId,
  currentUserId,
  refetchProject,
  t,
  toast,
  setIsInviteDialogOpen,
  setIsRequestDialogOpen,
}: UseProjectDetailTeamOptions) {
  const handleApiError = useCallback((error: unknown, fallbackKey: string) => {
    console.error(error)
    const apiError = error as ApiError
    toast({
      title: t("common.error"),
      description: apiError.userMessage ?? (error instanceof Error ? error.message : t(fallbackKey)),
      variant: "destructive",
    })
  }, [toast, t])

  const [inviteEmail, setInviteEmail] = useState("")
  const [inviteRole, setInviteRole] = useState("developer")
  const [inviteMessage, setInviteMessage] = useState("")
  const [isInviting, setIsInviting] = useState(false)
  const [requestRole, setRequestRole] = useState("developer")
  const [requestMessage, setRequestMessage] = useState("")
  const [isRequesting, setIsRequesting] = useState(false)
  const [respondingInvitationId, setRespondingInvitationId] = useState<string | null>(null)
  const [cancellingInvitationId, setCancellingInvitationId] = useState<string | null>(null)

  const projectTeam: TeamMember[] = useMemo(
    () =>
      Array.isArray(project?.team)
        ? project.team
        : Array.isArray(teamMembers)
          ? teamMembers
          : [],
    [project, teamMembers]
  )

  const isCurrentUserInTeam = useMemo(() => {
    if (!currentUserId) return false
    return projectTeam.some((member) => {
      const memberUserId = member.userId || member.UserId || member.id || member.Id
      return memberUserId === currentUserId
    })
  }, [currentUserId, projectTeam])

  const pendingJoinRequests = useMemo(() => {
    const items = incomingInvitations.data?.items
    if (!Array.isArray(items)) return []
    return items.filter((inv) => inv.projectId === projectId && inv.type === "request")
  }, [projectId, incomingInvitations.data])

  const pendingSentInvites = useMemo(() => {
    const items = sentInvitations.data?.items
    if (!Array.isArray(items)) return []
    return items.filter((inv) => inv.projectId === projectId && inv.type === "invite")
  }, [projectId, sentInvitations.data])

  const pendingMyJoinRequest = useMemo(() => {
    const items = sentInvitations.data?.items
    if (!Array.isArray(items)) return null
    const mine = items.filter((inv) => inv.projectId === projectId && inv.type === "request")
    return mine.length > 0 ? mine[0] : null
  }, [projectId, sentInvitations.data])

  const handleRespondToJoinRequest = useCallback(
    async (invitationId: string, action: "accept" | "decline") => {
      setRespondingInvitationId(invitationId)
      try {
        await respondToInvitation.mutateAsync({ invitationId, action })

        toast({
          title: t("common.success"),
          description: action === "accept"
            ? t("projects.requestAccepted")
            : t("projects.requestDeclined"),
        })

        if (action === "accept") {
          await refetchProject()
        }
      } catch (error) {
        handleApiError(error, "projects.failedToRespond")
      } finally {
        setRespondingInvitationId(null)
      }
    },
    [refetchProject, respondToInvitation, toast, t, handleApiError]
  )

  const handleCancelInvitation = useCallback(
    async (invitationId: string) => {
      setCancellingInvitationId(invitationId)
      try {
        await cancelInvitation.mutateAsync({ invitationId })
        toast({
          title: t("common.success"),
          description: "Cancelled",
        })
      } catch (error) {
        handleApiError(error, "projects.failedToCancel")
      } finally {
        setCancellingInvitationId(null)
      }
    },
    [cancelInvitation, toast, t, handleApiError]
  )

  const onSubmitInvite = useCallback(async () => {
    if (!inviteEmail.trim()) {
      toast({
        title: t("common.error"),
        description: t("projects.enterEmailOrUsername"),
        variant: "destructive",
      })
      return
    }

    setIsInviting(true)
    try {
      await sendInvitation.mutateAsync({
        projectId,
        username: inviteEmail.trim(),
        role: inviteRole,
        type: "invite",
        message: inviteMessage?.trim() || undefined,
      })

      toast({
        title: t("common.success"),
        description: t("projects.invitationSentSuccess"),
      })

      setIsInviteDialogOpen(false)
      setInviteEmail("")
      setInviteRole("developer")
      setInviteMessage("")
    } catch (error) {
      handleApiError(error, "projects.invitationSendFailed")
    } finally {
      setIsInviting(false)
    }
  }, [inviteEmail, inviteRole, inviteMessage, projectId, sendInvitation, toast, t, setIsInviteDialogOpen, handleApiError])

  const onApplyToRole = useCallback((roleName: string) => {
    setRequestRole(roleName)
    setIsRequestDialogOpen(true)
  }, [setIsRequestDialogOpen])

  const openRequestDialog = useCallback(() => {
    setIsRequestDialogOpen(true)
  }, [setIsRequestDialogOpen])

  const onSubmitJoinRequest = useCallback(async () => {
    if (!currentUserId) {
      toast({
        title: t("common.error"),
        description: t("projects.authenticationRequired"),
        variant: "destructive",
      })
      return
    }

    setIsRequesting(true)
    try {
      await sendInvitation.mutateAsync({
        projectId,
        role: requestRole,
        type: "request",
        message: requestMessage?.trim() || undefined,
      })

      toast({
        title: t("common.success"),
        description: t("projects.requestSentSuccess"),
      })

      setIsRequestDialogOpen(false)
      setRequestRole("developer")
      setRequestMessage("")
    } catch (error) {
      handleApiError(error, "projects.requestSendFailed")
    } finally {
      setIsRequesting(false)
    }
  }, [currentUserId, projectId, requestMessage, requestRole, sendInvitation, toast, t, setIsRequestDialogOpen, handleApiError])

  const projectMembersForPermissions: ProjectMemberPermission[] = useMemo(() => {
    return projectTeam.map((member) => ({
      id: member.id || member.Id || "",
      userId: member.userId || member.UserId || member.id || member.Id || "",
      name: member.fullName || member.FullName || "Unknown",
      email: member.email || member.Email,
      role: member.role || member.Role,
      canPublishNews: Boolean(member.canPublishNews ?? member.CanPublishNews) || false,
      canManageTasks: Boolean(member.canManageTasks ?? member.CanManageTasks) || false,
      canManageFiles: Boolean(member.canManageFiles ?? member.CanManageFiles) || false,
      canManageGallery: Boolean(member.canManageGallery ?? member.CanManageGallery) || false,
    }))
  }, [projectTeam])

  return {
    projectTeam,
    projectMembersForPermissions,
    isCurrentUserInTeam,
    pendingJoinRequests,
    pendingSentInvites,
    pendingMyJoinRequest,
    incomingInvitationsLoading: incomingInvitations.isLoading,
    sentInvitationsLoading: sentInvitations.isLoading,
    inviteEmail,
    setInviteEmail,
    inviteRole,
    setInviteRole,
    inviteMessage,
    setInviteMessage,
    isInviting,
    requestRole,
    setRequestRole,
    requestMessage,
    setRequestMessage,
    isRequesting,
    respondingInvitationId,
    cancellingInvitationId,
    onSubmitInvite,
    onSubmitJoinRequest,
    onApplyToRole,
    openRequestDialog,
    handleRespondToJoinRequest,
    handleCancelInvitation,
  }
}
