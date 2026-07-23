"use client"

import { useCallback, useMemo } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { useTranslations } from "next-intl"
import { useProjectIntegrations, useSyncIntegration, useGetOAuthUrl, useDeleteIntegration } from "@/lib/api/queries/integrations"
import { useToast } from "@/hooks/use-toast"

export function useProjectDetailIntegrations(
  resolvedProjectId: string,
  hasResolvedId: boolean,
  toast: ReturnType<typeof useToast>["toast"],
  t: ReturnType<typeof useTranslations>,
) {
  const queryClient = useQueryClient()
  const { data: integrations, isLoading: integrationsLoading } = useProjectIntegrations(
    hasResolvedId ? resolvedProjectId : undefined
  )
  const syncIntegration = useSyncIntegration(resolvedProjectId)
  const getOAuthUrl = useGetOAuthUrl(resolvedProjectId)
  const deleteIntegration = useDeleteIntegration(resolvedProjectId)

  const githubIntegration = useMemo(
    () => integrations?.find(i => i.serviceType === "github" && i.isActive),
    [integrations]
  )

  const handleSyncIntegration = useCallback(async (integrationId: string) => {
    try {
      await syncIntegration.mutateAsync(integrationId)
      toast({ title: t("integrations.syncSuccess"), description: t("integrations.syncSuccessDescription") })
      queryClient.invalidateQueries({ queryKey: ["project-tasks", resolvedProjectId] })
    } catch (error) {
      toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("integrations.syncFailed"), variant: "destructive" })
    }
  }, [syncIntegration, toast, t, queryClient, resolvedProjectId])

  const handleConnectGitHub = useCallback(async () => {
    try {
      const { oauthUrl } = await getOAuthUrl.mutateAsync("github")
      window.location.href = oauthUrl
    } catch (error) {
      toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("integrations.connectFailed"), variant: "destructive" })
    }
  }, [getOAuthUrl, toast, t])

  const handleDisconnectGitHub = useCallback(async () => {
    if (!githubIntegration) return
    try {
      await deleteIntegration.mutateAsync(githubIntegration.id)
      toast({ title: t("integrations.disconnected"), description: t("integrations.disconnectedDescription") })
    } catch (error) {
      toast({ title: t("common.error"), description: error instanceof Error ? error.message : t("integrations.disconnectFailed"), variant: "destructive" })
    }
  }, [githubIntegration, deleteIntegration, toast, t])

  return {
    integrations,
    integrationsLoading,
    githubIntegration,
    handleSyncIntegration,
    syncIntegrationPending: syncIntegration.isPending,
    handleConnectGitHub,
    connectGitHubPending: getOAuthUrl.isPending,
    handleDisconnectGitHub,
    disconnectGitHubPending: deleteIntegration.isPending,
  }
}
