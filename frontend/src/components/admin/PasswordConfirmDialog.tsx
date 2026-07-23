"use client"

import { useState, useCallback } from "react"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { AlertTriangle, Loader2, ShieldAlert } from "lucide-react"
import { useTranslations } from "next-intl"

interface PasswordConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  severity?: "warning" | "critical"
  isLoading?: boolean
  onConfirm: (password: string) => void
}

export function PasswordConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  severity = "warning",
  isLoading = false,
  onConfirm,
}: Readonly<PasswordConfirmDialogProps>) {
  const t = useTranslations("superAdmin")
  const [password, setPassword] = useState("")

  const handleConfirm = useCallback(() => {
    if (password.trim()) {
      onConfirm(password)
      setPassword("")
    }
  }, [password, onConfirm])

  const handleOpenChange = useCallback(
    (value: boolean) => {
      if (!value) setPassword("")
      onOpenChange(value)
    },
    [onOpenChange]
  )

  const handlePasswordChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setPassword(e.target.value)
  }, [])

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") handleConfirm()
  }, [handleConfirm])

  const handleCancelClick = useCallback(() => {
    handleOpenChange(false)
  }, [handleOpenChange])

  const isCritical = severity === "critical"

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            {isCritical ? (
              <ShieldAlert className="h-5 w-5 text-destructive" />
            ) : (
              <AlertTriangle className="h-5 w-5 text-yellow-500" />
            )}
            {title}
          </DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>

        <div className={`rounded-lg border p-3 ${isCritical ? "border-destructive/50 bg-destructive/5" : "border-yellow-500/50 bg-yellow-500/5"}`}>
          <p className={`text-xs font-medium ${isCritical ? "text-destructive" : "text-yellow-600 dark:text-yellow-400"}`}>
            {t("passwordRequired")}
          </p>
        </div>

        <div className="space-y-2">
          <Label htmlFor="confirm-password">{t("yourPassword")}</Label>
          <Input
            id="confirm-password"
            type="password"
            value={password}
            onChange={handlePasswordChange}
            placeholder={t("enterPassword")}
            onKeyDown={handleKeyDown}
            autoFocus
          />
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleCancelClick} disabled={isLoading}>
            {t("cancel")}
          </Button>
          <Button
            variant={isCritical ? "destructive" : "default"}
            onClick={handleConfirm}
            disabled={!password.trim() || isLoading}
          >
            {isLoading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : null}
            {t("confirmAction")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
