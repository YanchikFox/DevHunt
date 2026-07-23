"use client"

import { useState, useCallback, useEffect } from "react"
import { usePlatformSettings, useUpdatePlatformSettings } from "@/lib/api/queries/superadmin"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Loader2, Save } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

const KNOWN_SETTINGS: Record<string, { label: string; type: "text" | "number" | "toggle" }> = {
  registration_enabled: { label: "registrationEnabled", type: "toggle" },
  max_projects_per_user: { label: "maxProjectsPerUser", type: "number" },
  maintenance_mode: { label: "maintenanceMode", type: "toggle" },
}

export function PlatformSettingsForm() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const { data: settings, isLoading } = usePlatformSettings()
  const updateSettings = useUpdatePlatformSettings()

  const [values, setValues] = useState<Record<string, string>>({})
  const [password, setPassword] = useState("")

  useEffect(() => {
    if (settings) {
      const map: Record<string, string> = {}
      for (const s of settings) {
        if (!s.key.startsWith("ip_whitelist_")) {
          map[s.key] = s.value
        }
      }
      setValues(map)
    }
  }, [settings])

  const handleValueChange = useCallback((key: string, value: string) => {
    setValues((prev) => ({ ...prev, [key]: value }))
  }, [])

  const handlePasswordChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value)
  }, [])

  const handleSave = useCallback(async () => {
    if (!password) return
    try {
      await updateSettings.mutateAsync({ settings: values, confirmPassword: password })
      toast({ title: t("settingsUpdated") })
      setPassword("")
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [values, password, updateSettings, toast, t])

  if (isLoading) {
    return (
      <div className="flex justify-center py-8">
        <Loader2 className="h-6 w-6 animate-spin" />
      </div>
    )
  }

  const settingKeys = Object.keys(values).filter((k) => !k.startsWith("ip_whitelist_"))

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">{t("platformSettings")}</h3>

      <div className="space-y-3">
        {settingKeys.map((key) => {
          const meta = KNOWN_SETTINGS[key]
          const label = meta ? t(meta.label) : key

          if (meta?.type === "toggle") {
            return (
              <ToggleSetting
                key={key}
                settingKey={key}
                label={label}
                value={values[key]}
                onChange={handleValueChange}
              />
            )
          }

          return (
            <TextSetting
              key={key}
              settingKey={key}
              label={label}
              value={values[key]}
              type={meta?.type === "number" ? "number" : "text"}
              onChange={handleValueChange}
            />
          )
        })}
      </div>

      <div className="flex items-center gap-3 pt-4 border-t">
        <Input
          type="password"
          placeholder={t("confirmPassword")}
          value={password}
          onChange={handlePasswordChange}
          className="w-64"
        />
        <Button onClick={handleSave} disabled={updateSettings.isPending || !password}>
          {updateSettings.isPending ? <Loader2 className="h-4 w-4 animate-spin mr-1" /> : <Save className="h-4 w-4 mr-1" />}
          {t("saveSettings")}
        </Button>
      </div>
    </div>
  )
}

function ToggleSetting({ settingKey, label, value, onChange }: {
  settingKey: string; label: string; value: string; onChange: (key: string, value: string) => void
}) {
  const handleToggle = useCallback(() => {
    onChange(settingKey, value === "true" ? "false" : "true")
  }, [settingKey, value, onChange])

  return (
    <div className="flex items-center justify-between p-3 rounded-lg border">
      <span className="text-sm font-medium">{label}</span>
      <button
        type="button"
        onClick={handleToggle}
        className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
          value === "true" ? "bg-primary" : "bg-muted"
        }`}
      >
        <span
          className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
            value === "true" ? "translate-x-6" : "translate-x-1"
          }`}
        />
      </button>
    </div>
  )
}

function TextSetting({ settingKey, label, value, type, onChange }: {
  settingKey: string; label: string; value: string; type: string; onChange: (key: string, value: string) => void
}) {
  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    onChange(settingKey, e.target.value)
  }, [settingKey, onChange])

  return (
    <div className="flex items-center justify-between gap-4 p-3 rounded-lg border">
      <span className="text-sm font-medium shrink-0">{label}</span>
      <Input type={type} value={value} onChange={handleChange} className="w-48" />
    </div>
  )
}
