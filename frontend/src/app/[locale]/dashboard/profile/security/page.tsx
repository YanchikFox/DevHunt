"use client"

import { useState, useCallback } from "react"
import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { useToast } from "@/hooks/use-toast"
import { ShieldCheck, ShieldOff, Copy, Check, KeyRound, QrCode, AlertTriangle } from "lucide-react"
import { ProfileSettingsNav } from "@/components/profile/ProfileSettingsNav"
import { QRCodeSVG } from "qrcode.react"
import { useTotpStatus, useTotpSetup, useVerifyTotpSetup, useDisableTotp } from "@/lib/api/queries/auth"

type SetupStep = "idle" | "qr" | "verify" | "recovery" | "disable"

function TotpCodeInput({ id, value, onChange, autoFocus, className }: {
  id: string; value: string; onChange: (v: string) => void; autoFocus?: boolean; className?: string
}) {
  return (
    <Input
      id={id}
      type="text"
      inputMode="numeric"
      pattern="[0-9]*"
      maxLength={6}
      placeholder="000000"
      autoComplete="one-time-code"
      autoFocus={autoFocus}
      value={value}
      onChange={e => onChange(e.target.value.replace(/\D/g, ""))}
      className={className}
    />
  )
}

export default function SecuritySettingsPage() {
  const t = useTranslations()
  const { toast } = useToast()

  const showSuccess = useCallback((description: string) => toast({ title: t("common.success"), description }), [toast, t])
  const showError = useCallback((description: string) => toast({ title: t("common.error"), description, variant: "destructive" }), [toast, t])

  const { data: totpStatus, isLoading: statusLoading } = useTotpStatus()
  const setupMutation = useTotpSetup()
  const verifyMutation = useVerifyTotpSetup()
  const disableMutation = useDisableTotp()

  const [step, setStep] = useState<SetupStep>("idle")
  const [secret, setSecret] = useState("")
  const [qrUri, setQrUri] = useState("")
  const [verifyCode, setVerifyCode] = useState("")
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([])
  const [copiedCodes, setCopiedCodes] = useState(false)
  const [disablePassword, setDisablePassword] = useState("")
  const [disableTotpCode, setDisableTotpCode] = useState("")

  const isEnabled = totpStatus?.isEnabled ?? false

  const handleStartSetup = useCallback(async () => {
    try {
      const data = await setupMutation.mutateAsync()
      setSecret(data.secret)
      setQrUri(data.qrUri)
      setStep("qr")
    } catch {
      toast({
        title: t("common.error"),
        description: t("security.setupFailed") || "Failed to start 2FA setup",
        variant: "destructive",
      })
    }
  }, [setupMutation, toast, t])

  const handleVerify = useCallback(async () => {
    if (verifyCode.length !== 6) return
    try {
      const data = await verifyMutation.mutateAsync({ code: verifyCode, secret })
      setRecoveryCodes(data.recoveryCodes)
      setStep("recovery")
      showSuccess(t("security.twoFactorEnabled") || "Two-factor authentication enabled!")
    } catch {
      showError(t("security.invalidCode") || "Invalid code. Please try again.")
    }
  }, [verifyCode, secret, verifyMutation, t, showSuccess, showError])

  const handleCopyCodes = useCallback(async () => {
    const text = recoveryCodes.join("\n")
    await navigator.clipboard.writeText(text)
    setCopiedCodes(true)
    toast({ title: t("security.codesCopied") || "Recovery codes copied to clipboard" })
    setTimeout(() => setCopiedCodes(false), 2000)
  }, [recoveryCodes, toast, t])

  const handleDisable = useCallback(async () => {
    if (!disablePassword) return
    try {
      await disableMutation.mutateAsync({
        password: disablePassword,
        totpCode: disableTotpCode || undefined,
      })
      setStep("idle")
      setDisablePassword("")
      setDisableTotpCode("")
      showSuccess(t("security.twoFactorDisabled") || "Two-factor authentication disabled.")
    } catch {
      showError(t("security.disableFailed") || "Failed to disable 2FA. Check your password.")
    }
  }, [disablePassword, disableTotpCode, disableMutation, t, showSuccess, showError])

  const handleReset = useCallback(() => {
    setStep("idle")
    setSecret("")
    setQrUri("")
    setVerifyCode("")
    setRecoveryCodes([])
    setDisablePassword("")
    setDisableTotpCode("")
  }, [])

  if (statusLoading) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <div className="animate-pulse text-muted-foreground">{t("common.loading")}</div>
      </div>
    )
  }

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{t("security.title") || "Security Settings"}</h1>
          <p className="text-sm text-muted-foreground">
            {t("security.subtitle") || "Manage your account security"}
          </p>
        </div>
        <ProfileSettingsNav />
      </div>

      {/* Status Card */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className={`p-2 rounded-lg ${isEnabled ? "bg-green-500/10" : "bg-muted"}`}>
                {isEnabled
                  ? <ShieldCheck className="h-5 w-5 text-green-600 dark:text-green-400" />
                  : <ShieldOff className="h-5 w-5 text-muted-foreground" />}
              </div>
              <div>
                <CardTitle className="text-lg">
                  {t("security.twoFactorAuth") || "Two-Factor Authentication"}
                </CardTitle>
                <CardDescription>
                  {t("security.twoFactorDescription") || "Add an extra layer of security to your account"}
                </CardDescription>
              </div>
            </div>
            <Badge variant={isEnabled ? "default" : "secondary"} className={isEnabled ? "bg-green-600" : ""}>
              {isEnabled
                ? (t("security.enabled") || "Enabled")
                : (t("security.disabled") || "Disabled")}
            </Badge>
          </div>
        </CardHeader>
        <CardContent>
          {step === "idle" && (
            <>
              {isEnabled ? (
                <Button
                  variant="destructive"
                  onClick={() => setStep("disable")}
                  className="gap-2"
                >
                  <ShieldOff className="h-4 w-4" />
                  {t("security.disable2fa") || "Disable 2FA"}
                </Button>
              ) : (
                <Button onClick={handleStartSetup} disabled={setupMutation.isPending} className="gap-2">
                  <ShieldCheck className="h-4 w-4" />
                  {setupMutation.isPending
                    ? t("common.loading")
                    : (t("security.enable2fa") || "Enable 2FA")}
                </Button>
              )}
            </>
          )}

          {/* QR Code Step */}
          {step === "qr" && (
            <div className="space-y-4">
              <div className="rounded-lg border border-border p-4 bg-muted/20">
                <p className="text-sm text-muted-foreground mb-4">
                  {t("security.scanQrCode") || "Scan this QR code with your authenticator app (Google Authenticator, Authy, etc.)"}
                </p>
                <div className="flex justify-center p-4 bg-white rounded-lg">
                  <QRCodeSVG value={qrUri} size={200} />
                </div>
                <div className="mt-4">
                  <p className="text-xs text-muted-foreground mb-1">
                    {t("security.manualEntry") || "Or enter this key manually:"}
                  </p>
                  <code className="block p-2 bg-muted rounded text-xs font-mono break-all select-all">
                    {secret}
                  </code>
                </div>
              </div>
              <Button onClick={() => setStep("verify")} className="w-full">
                {t("common.next") || "Next"}
              </Button>
              <Button variant="ghost" onClick={handleReset} className="w-full">
                {t("common.cancel") || "Cancel"}
              </Button>
            </div>
          )}

          {/* Verify Step */}
          {step === "verify" && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="verifyCode" className="flex items-center gap-2">
                  <QrCode className="h-4 w-4" />
                  {t("security.enterVerificationCode") || "Enter the 6-digit code from your app"}
                </Label>
                <TotpCodeInput id="verifyCode" value={verifyCode} onChange={setVerifyCode} autoFocus className="text-center font-mono text-2xl tracking-[0.5em]" />
              </div>
              <Button
                onClick={handleVerify}
                disabled={verifyCode.length !== 6 || verifyMutation.isPending}
                className="w-full"
              >
                {verifyMutation.isPending ? t("common.loading") : (t("security.verifyAndEnable") || "Verify & Enable")}
              </Button>
              <Button variant="ghost" onClick={() => setStep("qr")} className="w-full">
                {t("common.back") || "Back"}
              </Button>
            </div>
          )}

          {/* Recovery Codes Step */}
          {step === "recovery" && (
            <div className="space-y-4">
              <div className="rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/30 p-4">
                <div className="flex items-start gap-2 mb-3">
                  <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0 mt-0.5" />
                  <div>
                    <p className="text-sm font-medium text-amber-800 dark:text-amber-200">
                      {t("security.saveRecoveryCodes") || "Save your recovery codes"}
                    </p>
                    <p className="text-xs text-amber-700 dark:text-amber-300 mt-1">
                      {t("security.recoveryCodesWarning") || "Store these codes in a safe place. Each code can only be used once. If you lose your authenticator, these codes are the only way to access your account."}
                    </p>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-2 p-3 bg-white dark:bg-background rounded-md border border-amber-200 dark:border-amber-800">
                  {recoveryCodes.map((code) => (
                    <code key={code} className="text-sm font-mono text-center py-1">
                      {code}
                    </code>
                  ))}
                </div>
              </div>
              <Button onClick={handleCopyCodes} variant="outline" className="w-full gap-2">
                {copiedCodes ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                {copiedCodes
                  ? (t("security.copied") || "Copied!")
                  : (t("security.copyCodes") || "Copy recovery codes")}
              </Button>
              <Button onClick={handleReset} className="w-full">
                {t("common.done") || "Done"}
              </Button>
            </div>
          )}

          {/* Disable Step */}
          {step === "disable" && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="disablePassword" className="flex items-center gap-2">
                  <KeyRound className="h-4 w-4" />
                  {t("security.confirmPassword") || "Confirm your password"}
                </Label>
                <Input
                  id="disablePassword"
                  type="password"
                  autoComplete="current-password"
                  value={disablePassword}
                  onChange={e => setDisablePassword(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="disableTotpCode" className="flex items-center gap-2">
                  <ShieldCheck className="h-4 w-4" />
                  {t("security.totpCodeOptional") || "Authenticator code (optional)"}
                </Label>
                <TotpCodeInput id="disableTotpCode" value={disableTotpCode} onChange={setDisableTotpCode}
                  className="font-mono tracking-widest"
                />
              </div>
              <Button
                variant="destructive"
                onClick={handleDisable}
                disabled={!disablePassword || disableMutation.isPending}
                className="w-full"
              >
                {disableMutation.isPending ? t("common.loading") : (t("security.confirmDisable") || "Disable Two-Factor Authentication")}
              </Button>
              <Button variant="ghost" onClick={handleReset} className="w-full">
                {t("common.cancel") || "Cancel"}
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
