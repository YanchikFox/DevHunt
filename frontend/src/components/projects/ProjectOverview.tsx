"use client"

import { useCallback } from "react"
import { useTranslations } from "next-intl"
import type { Project, Invitation } from "@/lib/api/schema"
import { useProjectRoles } from "@/lib/api/queries/teams"
import { Button } from "@/components/ui/button"

type ViewProject = Omit<Project, "maxTeamSize"> & { maxTeamSize?: number | null }

interface Vacancy {
  role: string
  totalNeeded: number
  currentFilled: number
  hoursPerWeek?: number | null
  equityOptional?: boolean
}

interface OpenRolesSectionProps {
  vacancies?: Vacancy[]
  isLoading: boolean
  isCurrentUserInTeam?: boolean
  currentUserId?: string
  pendingMyJoinRequest?: Invitation | null
  cancellingInvitationId?: string | null
  onApplyToRole?: (roleName: string) => void
  onCancelInvitation?: (id: string) => Promise<void>
  canManage?: boolean
  onManage?: () => void
}

function OpenRolesSection({
  vacancies,
  isLoading,
  isCurrentUserInTeam,
  currentUserId,
  pendingMyJoinRequest,
  cancellingInvitationId,
  onApplyToRole,
  onCancelInvitation,
  canManage,
  onManage,
}: OpenRolesSectionProps) {
  const t = useTranslations()

  const handleCancelRequest = useCallback(() => {
    if (pendingMyJoinRequest?.id && onCancelInvitation) {
      onCancelInvitation(pendingMyJoinRequest.id)
    }
  }, [pendingMyJoinRequest?.id, onCancelInvitation])

  if (isLoading) {
    return (
      <div className="border-t border-border pt-5">
        <span className="caption">[{t("teams.openRoles")}]</span>
        <div className="mt-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
          {[1, 2].map((i) => (
            <div key={i} className="h-20 animate-pulse rounded-lg bg-muted" />
          ))}
        </div>
      </div>
    )
  }

  if (!vacancies || vacancies.length === 0) {
    if (!canManage) return null
    return (
      <div className="border-t border-border pt-5">
        <div className="mb-3 flex items-center justify-between">
          <span className="caption">[{t("teams.openRoles")}]</span>
          <Button size="sm" variant="outline" onClick={onManage} className="h-7 text-[12px]">
            {t("teams.manageRoles")}
          </Button>
        </div>
        <p className="text-[13px] text-muted-foreground">{t("teams.noOpenRoles")}</p>
      </div>
    )
  }

  const canApply = Boolean(currentUserId) && !isCurrentUserInTeam && onApplyToRole

  return (
    <div className="border-t border-border pt-5">
      <div className="mb-3 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="caption">[{t("teams.openRoles")}]</span>
          <span className="chip text-[10px]">{vacancies.length}</span>
        </div>
        {canManage && (
          <Button size="sm" variant="outline" onClick={onManage} className="h-7 text-[12px]">
            {t("teams.manageRoles")}
          </Button>
        )}
      </div>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {vacancies.map((v) => {
          const isPendingThisRole =
            pendingMyJoinRequest?.status === "pending" &&
            pendingMyJoinRequest.role.toLowerCase() === v.role.toLowerCase()
          const handleApply = onApplyToRole ? () => onApplyToRole(v.role) : undefined

          const metaParts: string[] = []
          if (v.hoursPerWeek) metaParts.push(`~${v.hoursPerWeek} ${t("projects.hrsWeek")}`)
          if (v.equityOptional) metaParts.push(t("projects.equityOptionalShort"))

          return (
            <div
              key={v.role}
              className="flex flex-col gap-2.5 rounded-[10px] border border-border bg-bg-elevated p-3"
            >
              <div className="flex items-start justify-between gap-2">
                <span className="text-[13px] font-semibold leading-tight">{v.role}</span>
                <span className="chip accent flex-shrink-0 text-[10px]">{t("teams.open")}</span>
              </div>
              {metaParts.length > 0 && (
                <p className="font-mono text-[11px] text-muted-foreground">
                  {metaParts.join(" · ")}
                </p>
              )}
              {metaParts.length === 0 && (
                <p className="font-mono text-[11px] text-muted-foreground">
                  {v.currentFilled}/{v.totalNeeded} {t("teams.slotsFilled")}
                </p>
              )}
              {canApply && (
                isPendingThisRole ? (
                  <div className="flex gap-2">
                    <Button size="sm" variant="outline" className="flex-1 text-xs" disabled>
                      {t("teams.applied")}
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-xs"
                      onClick={handleCancelRequest}
                      disabled={cancellingInvitationId === pendingMyJoinRequest?.id}
                    >
                      {t("common.cancel")}
                    </Button>
                  </div>
                ) : (
                  <Button size="sm" variant="outline" className="w-full text-xs" onClick={handleApply}>
                    {t("teams.apply")}
                  </Button>
                )
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}

export interface ProjectOverviewProps {
  project: ViewProject
  teamSize: number
  isCurrentUserInTeam?: boolean
  currentUserId?: string
  pendingMyJoinRequest?: Invitation | null
  cancellingInvitationId?: string | null
  onApplyToRole?: (roleName: string) => void
  onCancelInvitation?: (id: string) => Promise<void>
  canManageRoles?: boolean
  onManageRoles?: () => void
}

/**
 * Displays an overview of a project.
 * Shows description, technologies, visibility, team size, and dates.
 *
 * @example
 * ```tsx
 * <ProjectOverview project={project} teamSize={5} />
 * ```
 */
export function ProjectOverview({
  project,
  isCurrentUserInTeam,
  currentUserId,
  pendingMyJoinRequest,
  cancellingInvitationId,
  onApplyToRole,
  onCancelInvitation,
  canManageRoles,
  onManageRoles,
}: ProjectOverviewProps) {
  const t = useTranslations()
  const rolesQuery = useProjectRoles(project.id)

  return (
    <section className="rounded-[14px] border border-border bg-bg-elevated">
      <div className="border-b border-border px-5 py-4">
        <span className="caption">[{t("projects.about")}]</span>
      </div>
      <div className="space-y-6 p-5">
        <p className="whitespace-pre-line text-[14px] leading-relaxed text-foreground">
          {project.detailedDescription || project.description || t("projects.noDescription")}
        </p>

        <OpenRolesSection
          vacancies={rolesQuery.data}
          isLoading={rolesQuery.isLoading}
          isCurrentUserInTeam={isCurrentUserInTeam}
          currentUserId={currentUserId}
          pendingMyJoinRequest={pendingMyJoinRequest}
          cancellingInvitationId={cancellingInvitationId}
          onApplyToRole={onApplyToRole}
          onCancelInvitation={onCancelInvitation}
          canManage={canManageRoles}
          onManage={onManageRoles}
        />
      </div>
    </section>
  )
}
