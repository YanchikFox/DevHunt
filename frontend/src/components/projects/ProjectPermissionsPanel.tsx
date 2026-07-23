"use client"

import { useMemo } from "react"
import { Badge } from "@/components/ui/badge"
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card"
import { Label } from "@/components/ui/label"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Skeleton } from "@/components/ui/skeleton"
import { Switch } from "@/components/ui/switch"
import { useTranslations } from "next-intl"

export type ProjectPermissionKey =
  | "canPublishNews"
  | "canManageTasks"
  | "canManageFiles"
  | "canManageGallery"

/**
 * Represents a project member's permissions.
 */
export interface ProjectMemberPermission {
  id: string
  userId: string
  name: string
  email?: string
  role?: string
  isLeader?: boolean
  canPublishNews: boolean
  canManageTasks: boolean
  canManageFiles: boolean
  canManageGallery: boolean
}

/**
 * Props for the ProjectPermissionsPanel component.
 */
export interface ProjectPermissionsPanelProps {
  /** List of members with their permissions */
  members: ProjectMemberPermission[]
  /** Whether data is loading */
  isLoading: boolean
  /** Whether the current user can edit permissions */
  canEdit: boolean
  /** ID of the member currently being updated */
  pendingMemberId?: string | null
  /** Callback function to toggle a permission */
  onToggle: (memberId: string, key: ProjectPermissionKey, value: boolean) => void
  /** ID of the project owner */
  ownerId?: string
}

const permissionKeys: ProjectPermissionKey[] = [
  "canPublishNews",
  "canManageTasks",
  "canManageFiles",
  "canManageGallery",
]

const permissionTranslationKeys: Record<ProjectPermissionKey, string> = {
  canPublishNews: "publishNews",
  canManageTasks: "manageTasks",
  canManageFiles: "manageFiles",
  canManageGallery: "manageGallery",
}

/**
 * A panel for managing project member permissions.
 * Allows toggling specific permissions for each member.
 *
 * @example
 * ```tsx
 * <ProjectPermissionsPanel
 *   members={members}
 *   isLoading={false}
 *   canEdit={true}
 *   onToggle={handleToggle}
 * />
 * ```
 */
export function ProjectPermissionsPanel({
  members,
  isLoading,
  canEdit,
  pendingMemberId,
  onToggle,
  ownerId,
}: ProjectPermissionsPanelProps) {
  const t = useTranslations("teams")
  const projectT = useTranslations("projects")
  const adminT = useTranslations("admin")

  const permissionToggleHandlers = useMemo(() => {
    const handlers: Record<string, (checked: boolean) => void> = {}

    members.forEach((member) => {
      permissionKeys.forEach((key) => {
        handlers[`${member.id}-${key}`] = (checked: boolean) => onToggle(member.id, key, checked)
      })
    })

    return handlers
  }, [members, onToggle])

  return (
    <Card className="border-2">
      <CardHeader>
        <CardTitle className="text-sm font-semibold uppercase">{t("delegatedRoles")}</CardTitle>
        <CardDescription>{t("delegatedRolesDesc")}</CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-3">
            {[1, 2, 3].map((i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        ) : members.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {t("noMembersToConfigure")}
          </p>
        ) : (
          <ScrollArea className="max-h-[420px]">
            <div className="grid grid-cols-[minmax(220px,1fr)_repeat(4,minmax(140px,1fr))] items-center gap-3 text-sm font-medium text-muted-foreground pb-2 border-b">
              <span className="text-muted-foreground">{t("member")}</span>
              {permissionKeys.map((key) => (
                <Label key={key} className="text-xs uppercase tracking-wide">
                  {t(permissionTranslationKeys[key])}
                </Label>
              ))}
            </div>
            <div className="divide-y">
              {members.map((member) => {
                const isOwner = ownerId ? member.userId === ownerId : false
                const isLocked = isOwner || member.role === "owner"

                return (
                  <div
                    key={member.id}
                    className="grid grid-cols-[minmax(220px,1fr)_repeat(4,minmax(140px,1fr))] items-center gap-3 py-3"
                  >
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-foreground">{member.name || t("member")}</span>
                        {isOwner && <Badge variant="secondary">{projectT("owner")}</Badge>}
                        {member.isLeader && !isOwner && <Badge variant="outline">{adminT("admin")}</Badge>}
                      </div>
                      <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                        {member.email && <span>{member.email}</span>}
                        {member.role && <Badge variant="outline">{member.role}</Badge>}
                      </div>
                    </div>

                    {permissionKeys.map((key) => (
                      <div key={`${member.id}-${key}`} className="flex items-center">
                        <Switch
                          checked={Boolean(member[key])}
                          disabled={!canEdit || isLocked || pendingMemberId === member.id}
                          onCheckedChange={permissionToggleHandlers[`${member.id}-${key}`]}
                          aria-label={`${t(permissionTranslationKeys[key])} for ${member.name || "member"}`}
                        />
                      </div>
                    ))}
                  </div>
                )
              })}
            </div>
          </ScrollArea>
        )}
      </CardContent>
    </Card>
  )
}
