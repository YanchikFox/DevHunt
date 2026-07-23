"use client"

import { useState, useCallback } from "react"
import { usePlatformSettings, useToggleMaintenance } from "@/lib/api/queries/superadmin"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Loader2, AlertTriangle, Shield } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

export function MaintenanceToggle() {
  const t = useTranslations("superAdmin")
  const { toast } = useToast()
  const { data: settings } = usePlatformSettings()
  const toggleMaintenance = useToggleMaintenance()

  const [showConfirm, setShowConfirm] = useState(false)
  const [password, setPassword] = useState("")

  const isMaintenanceOn = settings?.find((s) => s.key === "maintenance_mode")?.value === "true"

  const handleToggleClick = useCallback(() => {
    setShowConfirm(true)
    setPassword("")
  }, [])

  const handleConfirm = useCallback(async () => {
    if (!password) return
    try {
      await toggleMaintenance.mutateAsync({ enabled: !isMaintenanceOn, confirmPassword: password })
      toast({ title: isMaintenanceOn ? t("maintenanceDisabled") : t("maintenanceEnabled") })
      setShowConfirm(false)
      setPassword("")
    } catch (err) {
      const msg = (err as { userMessage?: string })?.userMessage ?? (err as Error)?.message ?? t("error")
      toast({ title: msg, variant: "destructive" })
    }
  }, [isMaintenanceOn, password, toggleMaintenance, toast, t])

  const handlePasswordChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value)
  }, [])

  const handleCancelConfirm = useCallback(() => {
    setShowConfirm(false)
  }, [])

  return (
    <div className="space-y-4">
      <h3 className="text-lg font-semibold">{t("maintenanceMode")}</h3>

      <div className={`rounded-lg border-2 p-6 text-center ${isMaintenanceOn ? "border-red-300 bg-red-50" : "border-green-300 bg-green-50"}`}>
        <div className="flex justify-center mb-3">
          {isMaintenanceOn ? (
            <AlertTriangle className="h-12 w-12 text-red-500" />
          ) : (
            <Shield className="h-12 w-12 text-green-500" />
          )}
        </div>
        <p className="text-lg font-bold mb-1">
          {isMaintenanceOn ? t("maintenanceIsOn") : t("systemOperational")}
        </p>
        <p className="text-sm text-muted-foreground mb-4">
          {isMaintenanceOn ? t("maintenanceOnDesc") : t("maintenanceOffDesc")}
        </p>
        <Button
          variant={isMaintenanceOn ? "outline" : "destructive"}
          size="lg"
          onClick={handleToggleClick}
        >
          {isMaintenanceOn ? t("disableMaintenance") : t("enableMaintenance")}
        </Button>
      </div>

      {/* Password Confirmation */}
      <Dialog open={showConfirm} onOpenChange={setShowConfirm}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {isMaintenanceOn ? t("disableMaintenance") : t("enableMaintenance")}
            </DialogTitle>
            <DialogDescription>
              {isMaintenanceOn ? t("disableMaintenanceConfirm") : t("enableMaintenanceConfirm")}
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
            <Button
              variant={isMaintenanceOn ? "default" : "destructive"}
              onClick={handleConfirm}
              disabled={toggleMaintenance.isPending || !password}
            >
              {toggleMaintenance.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("confirm")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
