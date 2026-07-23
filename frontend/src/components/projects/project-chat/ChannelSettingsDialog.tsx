"use client"

import { useEffect, useState } from "react"
import { useTranslations } from "next-intl"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Hash, Lock } from "lucide-react"
import type { ProjectChannel } from "@/lib/api/queries/project-channels"
import { ChannelOverviewTab } from "./ChannelOverviewTab"
import { ChannelMembersTab } from "./ChannelMembersTab"
import { ChannelBansTab } from "./ChannelBansTab"

interface ChannelSettingsDialogProps {
  readonly open: boolean
  readonly onOpenChange: (open: boolean) => void
  readonly projectId: string
  /** If present → edit mode with tabs. If absent → create mode (overview only). */
  readonly channel?: ProjectChannel | null
  readonly onCreated?: (channelId: string) => void
}

/**
 * Full channel settings — overview / members / bans tabs in edit mode, or a
 * single-panel "create" form in create mode. Tabs that require admin rights
 * are hidden for non-admin viewers.
 */
export function ChannelSettingsDialog({
  open,
  onOpenChange,
  projectId,
  channel,
  onCreated,
}: ChannelSettingsDialogProps) {
  const t = useTranslations("chat.channels")

  const isEdit = Boolean(channel)
  const canManageMembers = channel?.canManageMembers ?? false
  const [tab, setTab] = useState<"overview" | "members" | "bans">("overview")

  useEffect(() => {
    if (!open) return
    setTab("overview")
  }, [open, channel?.id])

  const TitleIcon = channel?.isPrivate ? Lock : Hash

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[min(640px,90vh)] overflow-hidden sm:max-w-[520px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <TitleIcon className="size-4 text-muted-foreground" aria-hidden />
            {isEdit ? t("editChannel") : t("createChannel")}
          </DialogTitle>
          {channel?.topic && (
            <DialogDescription className="line-clamp-2">
              {channel.topic}
            </DialogDescription>
          )}
        </DialogHeader>

        {isEdit && channel ? (
          <Tabs value={tab} onValueChange={(v) => setTab(v as typeof tab)}>
            <TabsList className="w-full">
              <TabsTrigger value="overview" className="flex-1">
                {t("tabOverview")}
              </TabsTrigger>
              {canManageMembers && (
                <TabsTrigger value="members" className="flex-1">
                  {t("tabMembers")}
                </TabsTrigger>
              )}
              {canManageMembers && (
                <TabsTrigger value="bans" className="flex-1">
                  {t("tabBans")}
                </TabsTrigger>
              )}
            </TabsList>
            <TabsContent value="overview" className="mt-3">
              <ChannelOverviewTab
                projectId={projectId}
                channel={channel}
                onSaved={() => onOpenChange(false)}
                onCancel={() => onOpenChange(false)}
              />
            </TabsContent>
            {canManageMembers && (
              <TabsContent value="members" className="mt-3">
                <ChannelMembersTab projectId={projectId} channel={channel} />
              </TabsContent>
            )}
            {canManageMembers && (
              <TabsContent value="bans" className="mt-3">
                <ChannelBansTab projectId={projectId} channel={channel} />
              </TabsContent>
            )}
          </Tabs>
        ) : (
          <ChannelOverviewTab
            projectId={projectId}
            channel={null}
            onSaved={(created) => {
              onCreated?.(created.id)
              onOpenChange(false)
            }}
            onCancel={() => onOpenChange(false)}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
