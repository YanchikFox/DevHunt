"use client"

import { useCallback } from "react"
import { useTranslations } from "next-intl"
import { Settings } from "lucide-react"
import { NotificationsCenter } from "@/components/features/NotificationsCenter"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { useMySettings, useUpdateMySettings, type UserPrivacySettings } from "@/lib/api/queries/settings"
import { Skeleton } from "@/components/ui/skeleton"

export default function NotificationsPage() {
  const t = useTranslations()
  const { data: settings, isLoading } = useMySettings()
  const updateSettings = useUpdateMySettings()

  const handleToggle = useCallback((key: keyof UserPrivacySettings, value: boolean) => {
    updateSettings.mutate({ [key]: value })
  }, [updateSettings])

  const handleToggleMessages = useCallback((checked: boolean) => handleToggle("NotifyOnMessages", checked), [handleToggle])
  const handleToggleInvitations = useCallback((checked: boolean) => handleToggle("NotifyOnInvitations", checked), [handleToggle])
  const handleReload = useCallback(() => window.location.reload(), [])

  return (
    <div className="mx-auto max-w-[760px] space-y-5 fade-in">
      <div>
        <span className="caption mb-1.5 block">[Inbox]</span>
        <h1 className="font-serif text-[36px] font-normal tracking-[-0.6px] leading-none text-foreground">
          {t("notifications.title")}
        </h1>
      </div>

      <NotificationsCenter />

      <section className="rounded-[14px] border border-border bg-card">
        <div className="flex items-center gap-2 border-b border-border px-[18px] py-3">
          <Settings className="h-3.5 w-3.5 text-muted-foreground" />
          <span className="caption">[{t("notifications.settings")}]</span>
        </div>
        <div className="px-[18px] py-4">
          <p className="mb-4 text-[12px] text-muted-foreground">{t("notifications.customize")}</p>
          {isLoading ? (
            <SettingsSkeleton />
          ) : settings ? (
            <div className="flex flex-col gap-4">
              <SettingToggle
                id="messages"
                title={t("notifications.newMessages")}
                desc={t("notifications.newMessagesDesc")}
                checked={settings.NotifyOnMessages}
                onCheckedChange={handleToggleMessages}
              />
              <div className="h-px w-full bg-border" />
              <SettingToggle
                id="invitations"
                title={t("notifications.teamInvitations")}
                desc={t("notifications.teamInvitationsDesc")}
                checked={settings.NotifyOnInvitations}
                onCheckedChange={handleToggleInvitations}
              />
            </div>
          ) : (
            <div className="py-3 text-center">
              <p className="text-[12px] text-muted-foreground">{t("notifications.failedToLoadSettings")}</p>
              <button onClick={handleReload} className="mt-1.5 text-[12px] text-primary hover:underline">
                {t("common.retry")}
              </button>
            </div>
          )}
        </div>
      </section>
    </div>
  )
}

function SettingToggle({
  id,
  title,
  desc,
  checked,
  onCheckedChange,
}: {
  readonly id: string
  readonly title: string
  readonly desc: string
  readonly checked: boolean
  readonly onCheckedChange: (checked: boolean) => void
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <div className="space-y-0.5">
        <Label htmlFor={id} className="text-[13px] font-medium">{title}</Label>
        <p className="text-[11px] text-muted-foreground">{desc}</p>
      </div>
      <Switch id={id} checked={checked} onCheckedChange={onCheckedChange} />
    </div>
  )
}

function SettingsSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      {[1, 2].map((i) => (
        <div key={i} className="flex items-center justify-between">
          <div className="space-y-1.5">
            <Skeleton className="h-3.5 w-28" />
            <Skeleton className="h-3 w-44" />
          </div>
          <Skeleton className="h-5 w-9 rounded-full" />
        </div>
      ))}
    </div>
  )
}
