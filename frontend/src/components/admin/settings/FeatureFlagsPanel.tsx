"use client"

import { useState, useCallback } from "react"
import { useFeatureFlags, useToggleFeatureFlag } from "@/lib/api/queries/superadmin"
import { Input } from "@/components/ui/input"
import { Loader2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"

export function FeatureFlagsPanel() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const { data: flags, isLoading } = useFeatureFlags()
  const toggleFlag = useToggleFeatureFlag()

  const [confirmTarget, setConfirmTarget] = useState<string | null>(null)
  const [password, setPassword] = useState("")

  const handleToggle = useCallback((key: string) => {
    setConfirmTarget(key)
    setPassword("")
  }, [])

  const handleConfirm = useCallback(async () => {
    if (!confirmTarget || !password) return
    try {
      const result = await toggleFlag.mutateAsync({ key: confirmTarget, confirmPassword: password })
      toast({ title: `${confirmTarget} → ${result.enabled ? "ON" : "OFF"}` })
      setConfirmTarget(null)
      setPassword("")
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [confirmTarget, password, toggleFlag, toast, t])

  const handlePasswordChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value)
  }, [])

  const handleCloseConfirmDialog = useCallback((open: boolean) => {
    if (!open) setConfirmTarget(null)
  }, [])

  const handleCancelConfirm = useCallback(() => {
    setConfirmTarget(null)
  }, [])

  if (isLoading) {
    return (
      <div className="flex justify-center py-8">
        <Loader2 className="h-6 w-6 animate-spin" />
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">{t("featureFlags")}</h3>

      <div className="space-y-2">
        {(flags ?? []).map((flag) => (
          <FlagRow key={flag.key} flagKey={flag.key} enabled={flag.enabled} description={flag.description} onToggle={handleToggle} />
        ))}
        {(flags ?? []).length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-4">{t("noFeatureFlags")}</p>
        )}
      </div>

      {/* Password Confirmation Dialog */}
      <Dialog open={confirmTarget !== null} onOpenChange={handleCloseConfirmDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("toggleFeatureFlag")}</DialogTitle>
            <DialogDescription>
              {t("toggleFeatureFlagConfirm", { flag: confirmTarget ?? "" })}
            </DialogDescription>
          </DialogHeader>
          <Input
            type="password"
            placeholder={t("confirmPassword")}
            value={password}
            onChange={handlePasswordChange}
          />
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelConfirm}>
              {t("cancel")}
            </Button>
            <Button onClick={handleConfirm} disabled={toggleFlag.isPending || !password}>
              {toggleFlag.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function FlagRow({ flagKey, enabled, description, onToggle }: {
  flagKey: string; enabled: boolean; description: string | null; onToggle: (key: string) => void
}) {
  const handleClick = useCallback(() => onToggle(flagKey), [flagKey, onToggle])

  return (
    <div className="flex items-center justify-between p-3 rounded-lg border">
      <div>
        <span className="text-sm font-medium">{flagKey}</span>
        {description && <p className="text-xs text-muted-foreground">{description}</p>}
      </div>
      <button
        type="button"
        onClick={handleClick}
        className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
          enabled ? "bg-green-500" : "bg-muted"
        }`}
      >
        <span
          className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
            enabled ? "translate-x-6" : "translate-x-1"
          }`}
        />
      </button>
    </div>
  )
}
