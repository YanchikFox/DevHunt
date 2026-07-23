"use client"

import { useTranslations } from "next-intl"
import { useCallback, useEffect, useMemo, useState } from "react"
import { Settings, Save } from "lucide-react"

import { ProfileSettingsNav } from "@/components/profile/ProfileSettingsNav"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useToast } from "@/hooks/use-toast"
import {
  PrivacySettingsState,
  PrivacySettingsPanel,
  PrivacyVisibility,
} from "@/components/profile/PrivacySettingsPanel"
import { usePrivacySettings, useUpdatePrivacySettings } from "@/lib/api/queries/profile"

const defaultState: PrivacySettingsState = {
  profileVisibility: "public",
  activityVisibility: "public",
  showEmail: false,
  showSocialLinks: true,
  showSkills: true,
  showExperience: true,
  showProjects: true,
  showAchievements: true,
}

export default function PrivacySettingsPage() {
  const t = useTranslations("privacy")
  const tCommon = useTranslations("common")
  const { data, isLoading } = usePrivacySettings()
  const updatePrivacy = useUpdatePrivacySettings()
  const { toast } = useToast()

  const [state, setState] = useState<PrivacySettingsState>(defaultState)

  const merged = useMemo(() => {
    return {
      profileVisibility: normalizeVisibility(data?.profileVisibility),
      activityVisibility: normalizeVisibility(data?.activityVisibility),
      showEmail: Boolean(data?.showEmail),
      showSocialLinks: Boolean(data?.showSocialLinks),
      showSkills: Boolean(data?.showSkills),
      showExperience: Boolean(data?.showExperience),
      showProjects: Boolean(data?.showProjects),
      showAchievements: Boolean(data?.showAchievements),
    } as PrivacySettingsState
  }, [data])

  useEffect(() => {
    if (data) {
      setState(merged)
    }
  }, [data, merged])

  const handleChange = useCallback((partial: Partial<PrivacySettingsState>) => {
    setState((prev) => ({ ...prev, ...partial }))
  }, [])

  const handleSave = useCallback(async () => {
    try {
      await updatePrivacy.mutateAsync({
        profileVisibility: state.profileVisibility,
        activityVisibility: state.activityVisibility,
        showEmail: state.showEmail,
        showSocialLinks: state.showSocialLinks,
        showSkills: state.showSkills,
        showExperience: state.showExperience,
        showProjects: state.showProjects,
        showAchievements: state.showAchievements,
      })
      toast({
        title: t("settingsSaved"),
        description: t("settingsSavedDesc"),
      })
    } catch (error) {
      toast({
        title: t("saveError"),
        description: error instanceof Error ? error.message : t("saveErrorDesc"),
        variant: "destructive",
      })
    }
  }, [
    state.activityVisibility,
    state.profileVisibility,
    state.showAchievements,
    state.showEmail,
    state.showExperience,
    state.showProjects,
    state.showSkills,
    state.showSocialLinks,
    toast,
    updatePrivacy,
    t,
  ])

  if (isLoading && !data) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-56" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{t("privacySettings")}</h1>
          <p className="text-sm text-muted-foreground">
            {t("manageVisibility")}
          </p>
        </div>
        <ProfileSettingsNav />
      </div>

      <div className="flex justify-end">
        <Button onClick={handleSave} disabled={updatePrivacy.isPending}>
          <Save className="h-4 w-4 mr-2" />
          {updatePrivacy.isPending ? t("saving") : tCommon("save")}
        </Button>
      </div>

      <PrivacySettingsPanel values={state} onChange={handleChange} />

      <Card className="border-2">
        <CardContent className="flex items-center gap-3 p-4 text-sm text-muted-foreground">
          <Settings className="h-4 w-4" />
          {t("changesAppliedImmediately")}
        </CardContent>
      </Card>

    </div>
  )
}

// A-07: Preserve all valid visibility values including friends_only
function normalizeVisibility(value?: string | null): PrivacyVisibility {
  const normalized = (value || "public").toLowerCase()
  const allowed: PrivacyVisibility[] = ["public", "followers", "private", "friends_only"]
  return allowed.includes(normalized as PrivacyVisibility)
    ? (normalized as PrivacyVisibility)
    : "public"
}
