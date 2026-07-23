"use client"

import { useAdminExtendedStats } from "@/lib/api/queries/admin-issues"
import { StatsCard } from "@/components/ui/stats-card"
import { Loader2, Users, FolderKanban, Shield, Ticket, AlertTriangle, MessageSquare } from "lucide-react"
import { useTranslations } from "next-intl"

export function AdminStats() {
  const t = useTranslations("admin")
  const { data, isLoading } = useAdminExtendedStats()

  if (isLoading) {
    return (
      <div className="flex justify-center p-8">
        <Loader2 className="h-8 w-8 animate-spin" />
      </div>
    )
  }

  if (!data) {
    return <div className="p-8 text-center text-muted-foreground">{t("noStatsAvailable")}</div>
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatsCard
          title={t("totalUsers")}
          value={data.totalUsers}
          icon={Users}
          colorScheme="blue"
          description={`${data.activeUsers} ${t("activeLabel")}`}
        />
        <StatsCard
          title={t("totalProjects")}
          value={data.totalProjects}
          icon={FolderKanban}
          colorScheme="green"
          description={`${data.activeProjects} ${t("activeLabel")}, ${data.completedProjects} ${t("completedLabel")}`}
        />
        <StatsCard
          title={t("pendingModeration")}
          value={data.pendingModeration}
          icon={Shield}
          colorScheme="orange"
        />
        <StatsCard
          title={t("showcaseProjects")}
          value={data.showcaseProjects}
          icon={FolderKanban}
          colorScheme="purple"
        />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <StatsCard
          title={t("supportTickets")}
          value={data.openSupportTickets}
          icon={Ticket}
          colorScheme="blue"
          description={`${data.inProgressTickets} ${t("inProgressLabel")}, ${data.resolvedTickets} ${t("resolvedLabel")}`}
        />
        <StatsCard
          title={t("projectIssues")}
          value={data.openIssues}
          icon={AlertTriangle}
          colorScheme="orange"
          description={`${data.investigatingIssues} ${t("investigatingLabel")}, ${data.resolvedIssues} ${t("resolvedLabel")}`}
        />
        <StatsCard
          title={t("communityFeedback")}
          value={data.openFeedback}
          icon={MessageSquare}
          colorScheme="green"
          description={`${data.plannedFeedback} ${t("plannedLabel")}, ${data.completedFeedback} ${t("completedLabel")}`}
        />
      </div>
    </div>
  )
}
