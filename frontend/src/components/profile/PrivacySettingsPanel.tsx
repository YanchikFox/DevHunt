"use client"

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { useTranslations } from "next-intl"

export type PrivacyVisibility = "public" | "followers" | "private" | "friends_only"

/**
 * Represents the state of privacy settings.
 */
export interface PrivacySettingsState {
  profileVisibility: PrivacyVisibility
  showEmail: boolean
  showSocialLinks: boolean
  showSkills: boolean
  showExperience: boolean
  showProjects: boolean
  showAchievements: boolean
  activityVisibility: PrivacyVisibility
}

/**
 * Props for the PrivacySettingsPanel component.
 */
export interface PrivacySettingsPanelProps {
  /** Current privacy settings values */
  values: PrivacySettingsState
  /** Callback function triggered when settings change */
  onChange: (next: Partial<PrivacySettingsState>) => void
}

/**
 * A panel for managing user privacy settings.
 * Allows configuring visibility of profile sections and activity.
 *
 * @example
 * ```tsx
 * <PrivacySettingsPanel
 *   values={currentSettings}
 *   onChange={(newSettings) => updateSettings(newSettings)}
 * />
 * ```
 */
function VisibilitySelect({ label, value, onChange, placeholder, options }: {
  label: string
  value: string
  onChange: (val: string) => void
  placeholder: string
  options: { value: string; label: string }[]
}) {
  return (
    <div className="space-y-2">
      <Label className="text-xs uppercase tracking-wide">{label}</Label>
      <Select value={value} onValueChange={onChange}>
        <SelectTrigger>
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {options.map((opt) => (
            <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  )
}

export function PrivacySettingsPanel({ values, onChange }: PrivacySettingsPanelProps) {
  const t = useTranslations("privacy")
  const tPlaceholders = useTranslations("placeholders")
  const tProjects = useTranslations("projects")

  const handleSelectChange = (key: keyof PrivacySettingsState) => (val: string) => {
    onChange({ [key]: val as PrivacyVisibility })
  }

  const visibilityOptions = [
    { value: "public", label: tProjects("public") },
    { value: "followers", label: t("followers") },
    { value: "private", label: tProjects("private") },
  ]

  const handleToggle = (key: keyof PrivacySettingsState) => (checked: boolean) => {
    onChange({ [key]: checked })
  }

  return (
    <Card className="border-2">
      <CardHeader>
        <CardTitle className="text-sm font-semibold uppercase">{t("privacySettings")}</CardTitle>
        <CardDescription>{t("controlWhoCanSee")}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="grid gap-4 sm:grid-cols-2">
          <VisibilitySelect
            label={t("profileVisibility")}
            value={values.profileVisibility}
            onChange={handleSelectChange("profileVisibility")}
            placeholder={tPlaceholders("chooseVisibility")}
            options={visibilityOptions}
          />
          <VisibilitySelect
            label={t("activityVisibility")}
            value={values.activityVisibility}
            onChange={handleSelectChange("activityVisibility")}
            placeholder={tPlaceholders("chooseVisibility")}
            options={visibilityOptions}
          />
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <ToggleRow
            label={t("showEmail")}
            description={t("showEmailDesc")}
            checked={values.showEmail}
            onCheckedChange={handleToggle("showEmail")}
          />
          <ToggleRow
            label={t("showSocialLinks")}
            description={t("showSocialLinksDesc")}
            checked={values.showSocialLinks}
            onCheckedChange={handleToggle("showSocialLinks")}
          />
          <ToggleRow
            label={t("showSkills")}
            description={t("showSkillsDesc")}
            checked={values.showSkills}
            onCheckedChange={handleToggle("showSkills")}
          />
          <ToggleRow
            label={t("showExperience")}
            description={t("showExperienceDesc")}
            checked={values.showExperience}
            onCheckedChange={handleToggle("showExperience")}
          />
          <ToggleRow
            label={t("showProjects")}
            description={t("showProjectsDesc")}
            checked={values.showProjects}
            onCheckedChange={handleToggle("showProjects")}
          />
          <ToggleRow
            label={t("showAchievements")}
            description={t("showAchievementsDesc")}
            checked={values.showAchievements}
            onCheckedChange={handleToggle("showAchievements")}
          />
        </div>
      </CardContent>
    </Card>
  )
}

function ToggleRow({
  label,
  description,
  checked,
  onCheckedChange,
}: {
  label: string
  description: string
  checked: boolean
  onCheckedChange: (checked: boolean) => void
}) {
  return (
    <div className="flex items-start justify-between gap-4 rounded-lg border p-3">
      <div className="space-y-1">
        <p className="text-sm font-medium">{label}</p>
        <p className="text-xs text-muted-foreground">{description}</p>
      </div>
      <Switch checked={checked} onCheckedChange={onCheckedChange} aria-label={label} />
    </div>
  )
}
