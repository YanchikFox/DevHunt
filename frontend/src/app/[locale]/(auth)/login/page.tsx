"use client"

import { signIn } from "next-auth/react"
import { useRouter, Link } from "@/i18n/routing";
import { useTranslations } from "next-intl"
import { useForm } from "react-hook-form"
import { useCallback, useState } from "react"
import { useSearchParams } from "next/navigation"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { useToast } from "@/hooks/use-toast"
import { ArrowRight, Github, ShieldCheck, ArrowLeft, KeyRound, Check } from "lucide-react"
import { refreshCsrfToken } from "@/lib/api/csrf"
import { cn } from "@/lib/utils"
import { resolveSafeRedirectUrl } from "@/lib/security/safe-redirect"

const createLoginSchema = (t: (key: string) => string) => z.object({
  email: z.string().email(t("validation.email")),
  password: z.string().min(8, t("auth.passwordMinLength8")),
})

type LoginForm = z.infer<ReturnType<typeof createLoginSchema>>

function AuthLeftPanel() {
  return (
    <div className="hidden md:flex flex-col justify-between bg-muted/40 border-r border-border p-12 h-full">
      {/* Logo */}
      <div className="flex items-center gap-2.5">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="text-primary">
          <path d="M4 20L12 4l8 16" /><path d="M8 14h8" />
        </svg>
        <span className="text-[15px] font-semibold tracking-tight">DevHunt</span>
      </div>

      {/* Statement */}
      <div>
        <div className="caption mb-3">[What you&apos;re joining]</div>
        <p className="font-serif text-[34px] font-normal tracking-[-0.03em] leading-[1.05] text-foreground mb-8">
          A quiet place to build{" "}
          <em style={{ color: "oklch(var(--primary))", fontStyle: "italic" }}>loud</em> things.
        </p>
        <ul className="flex flex-col gap-3">
          {[
            "AI-matched collaborators",
            "Integrated Kanban & chat",
            "GitHub sync, zero config",
            "Public showcase on release",
          ].map(item => (
            <li key={item} className="flex items-center gap-3 text-[13px] text-muted-foreground">
              <Check className="h-3.5 w-3.5 text-primary shrink-0" strokeWidth={2} />
              {item}
            </li>
          ))}
        </ul>
      </div>

      <div className="font-mono text-[10px] text-muted-foreground/60 tracking-wider uppercase">
        EST. 2025 · BERLIN · WARSAW
      </div>
    </div>
  )
}

export default function LoginPage() {
  const t = useTranslations()
  const router = useRouter()
  const { toast } = useToast()
  const searchParams = useSearchParams()
  const callbackUrl = resolveSafeRedirectUrl(searchParams.get("callbackUrl"))

  const [twoFactorStep, setTwoFactorStep] = useState(false)
  const [totpCode, setTotpCode] = useState("")
  const [recoveryCode, setRecoveryCode] = useState("")
  const [useRecoveryCode, setUseRecoveryCode] = useState(false)
  const [savedCredentials, setSavedCredentials] = useState<LoginForm | null>(null)
  const [isSubmitting2fa, setIsSubmitting2fa] = useState(false)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(createLoginSchema(t)),
  })

  const startOAuth = useCallback((provider: "github" | "google") => {
    const authApi = process.env.NEXT_PUBLIC_AUTH_API_URL || process.env.AUTH_SERVICE_URL || "http://localhost:7001"
    const path = window.location.pathname.split("/")
    const localeSegment = path.length > 1 && path[1] ? `/${path[1]}` : ""
    const oauthCallbackUrl = `${window.location.origin}${localeSegment}/oauth-callback`
    const loginPath = authApi.startsWith("/") ? `${authApi}/auth/login/${provider}` : `${authApi}/api/auth/login/${provider}`
    window.location.href = `${loginPath}?redirectUri=${encodeURIComponent(oauthCallbackUrl)}`
  }, [])

  const handleOAuthGithub = useCallback(() => startOAuth("github"), [startOAuth])

  const completeLogin = useCallback(async () => {
    toast({ title: t("common.success"), description: t("auth.loggedInSuccessfully") })
    await refreshCsrfToken()
    router.push(callbackUrl)
    router.refresh()
  }, [toast, t, router, callbackUrl])

  const handleSignInError = useCallback((message: string, data: LoginForm) => {
    if (message?.includes("TwoFactorRequired")) {
      setSavedCredentials(data)
      setTwoFactorStep(true)
      return true
    }
    if (message?.startsWith("EmailNotVerified:")) {
      const email = message.split(":")[1]
      toast({ title: t("auth.emailNotVerified"), description: t("auth.pleaseVerifyEmail"), variant: "destructive" })
      router.push(`/verify-email?email=${encodeURIComponent(email || data.email)}`)
      return true
    }
    return false
  }, [toast, t, router])

  const onSubmit = useCallback(async (data: LoginForm) => {
    try {
      const result = await signIn("credentials", { email: data.email, password: data.password, redirect: false })
      if (result?.error) {
        if (handleSignInError(result.error, data)) return
        throw new Error(result.error)
      }
      await completeLogin()
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : String(error)
      if (handleSignInError(message, data)) return
      toast({ title: t("common.error"), description: message || t("auth.loginFailed"), variant: "destructive" })
    }
  }, [handleSignInError, completeLogin, toast, t])

  const onSubmit2fa = useCallback(async () => {
    if (!savedCredentials) return
    const code = useRecoveryCode ? recoveryCode.trim() : totpCode.trim()
    if (!code) return
    setIsSubmitting2fa(true)
    try {
      const result = await signIn("credentials", {
        email: savedCredentials.email,
        password: savedCredentials.password,
        ...(useRecoveryCode ? { recoveryCode: code } : { totpCode: code }),
        redirect: false,
      })
      if (result?.error) {
        toast({ title: t("common.error"), description: t("auth.invalidTotpCode"), variant: "destructive" })
        return
      }
      await completeLogin()
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : String(error)
      toast({ title: t("common.error"), description: message || t("auth.loginFailed"), variant: "destructive" })
    } finally {
      setIsSubmitting2fa(false)
    }
  }, [savedCredentials, useRecoveryCode, recoveryCode, totpCode, completeLogin, toast, t])

  const handleBack = useCallback(() => {
    setTwoFactorStep(false)
    setTotpCode("")
    setRecoveryCode("")
    setUseRecoveryCode(false)
    setSavedCredentials(null)
  }, [])

  const handleToggleRecovery = useCallback(() => {
    setUseRecoveryCode(prev => !prev)
    setTotpCode("")
    setRecoveryCode("")
  }, [])

  // ── 2FA Step ──────────────────────────────────────────────────────
  if (twoFactorStep) {
    return (
      <div className="min-h-screen grid md:grid-cols-2">
        <AuthLeftPanel />
        <div className="flex items-center justify-center p-10">
          <div className="w-full max-w-[400px]">
            <div className="flex items-center gap-3 mb-6">
              <div className="h-10 w-10 rounded-[10px] bg-primary/10 flex items-center justify-center">
                <ShieldCheck className="h-5 w-5 text-primary" />
              </div>
              <div>
                <h2 className="font-serif text-[28px] font-normal tracking-[-0.02em] leading-none">Two-factor auth.</h2>
              </div>
            </div>
            <p className="text-[14px] text-muted-foreground mb-6">
              {useRecoveryCode ? t("auth.enterRecoveryCode") : t("auth.enterTotpCode")}
            </p>
            <div className="space-y-4">
              {useRecoveryCode ? (
                <div className="space-y-1.5">
                  <label className="caption flex items-center gap-2">
                    <KeyRound className="h-3 w-3" />
                    {t("auth.recoveryCode")}
                  </label>
                  <Input
                    type="text"
                    placeholder="XXXX-XXXX"
                    autoComplete="off"
                    value={recoveryCode}
                    onChange={e => setRecoveryCode(e.target.value)}
                    className="font-mono text-lg tracking-widest text-center rounded-[10px]"
                  />
                </div>
              ) : (
                <div className="space-y-1.5">
                  <label className="caption flex items-center gap-2">
                    <ShieldCheck className="h-3 w-3" />
                    {t("auth.authenticatorCode")}
                  </label>
                  <Input
                    type="text"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    maxLength={6}
                    placeholder="000000"
                    autoComplete="one-time-code"
                    autoFocus
                    value={totpCode}
                    onChange={e => setTotpCode(e.target.value.replace(/\D/g, ""))}
                    className="font-mono text-2xl tracking-[0.5em] text-center rounded-[10px]"
                  />
                </div>
              )}
              <Button
                type="button"
                className="w-full gap-2 rounded-[10px]"
                disabled={isSubmitting2fa || (useRecoveryCode ? !recoveryCode.trim() : totpCode.length !== 6)}
                onClick={onSubmit2fa}
              >
                {isSubmitting2fa ? t("common.loading") : <>{t("auth.verify")} <ArrowRight className="h-4 w-4" /></>}
              </Button>
              <div className="flex items-center justify-between text-[12px]">
                <button type="button" onClick={handleBack} className="flex items-center gap-1 text-muted-foreground hover:text-foreground transition-colors">
                  <ArrowLeft className="h-3 w-3" /> {t("common.back")}
                </button>
                <button type="button" onClick={handleToggleRecovery} className="text-primary hover:underline">
                  {useRecoveryCode ? t("auth.useAuthenticator") : t("auth.useRecoveryCode")}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    )
  }

  // ── Normal Login Step ─────────────────────────────────────────────
  return (
    <div className="min-h-screen grid md:grid-cols-2">
      <AuthLeftPanel />

      {/* Form panel */}
      <div className="flex items-center justify-center p-10">
        <div className="w-full max-w-[400px]">
          {/* Tab switcher */}
          <div className="flex gap-1 mb-7 bg-muted/60 p-1 rounded-[10px] w-fit">
            <span className="px-4 py-1.5 rounded-[8px] bg-card shadow-sm text-[12px] font-medium">Log in</span>
            <Link href="/register" className="px-4 py-1.5 rounded-[8px] text-[12px] font-medium text-muted-foreground hover:text-foreground transition-colors">
              Sign up
            </Link>
          </div>

          <h2 className="font-serif text-[34px] font-normal tracking-[-0.03em] leading-none mb-2 text-foreground">
            Welcome back.
          </h2>
          <p className="text-[14px] text-muted-foreground mb-8">
            Sign in to continue shipping.
          </p>

          <div className="space-y-3">
            {/* GitHub OAuth */}
            <Button
              variant="outline"
              className="w-full h-11 gap-2 rounded-[10px] text-[13px] justify-center"
              type="button"
              onClick={handleOAuthGithub}
            >
              <Github className="h-4 w-4" />
              {t("auth.continueWithGitHub")}
            </Button>

            {/* Divider */}
            <div className="relative flex items-center gap-3 my-1">
              <div className="flex-1 h-px bg-border" />
              <span className="text-[11px] text-muted-foreground uppercase tracking-wider">or</span>
              <div className="flex-1 h-px bg-border" />
            </div>

            {/* Email form */}
            <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
              <div className="space-y-1.5">
                <label className="caption" htmlFor="email">Email</label>
                <Input
                  id="email"
                  type="email"
                  placeholder={t("auth.emailPlaceholder")}
                  autoComplete="email"
                  {...register("email")}
                  aria-invalid={errors.email ? "true" : "false"}
                  className={cn("h-10 rounded-[10px] text-[13px]", errors.email && "border-destructive")}
                />
                {errors.email && (
                  <p className="text-[12px] text-destructive" role="alert">{errors.email.message}</p>
                )}
              </div>

              <div className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <label className="caption" htmlFor="password">Password</label>
                  <Link href="/forgot-password" className="text-[11px] text-primary hover:underline">
                    {t("auth.forgotPassword")}
                  </Link>
                </div>
                <Input
                  id="password"
                  type="password"
                  placeholder={t("auth.passwordPlaceholder")}
                  autoComplete="current-password"
                  {...register("password")}
                  aria-invalid={errors.password ? "true" : "false"}
                  className={cn("h-10 rounded-[10px] text-[13px]", errors.password && "border-destructive")}
                />
                {errors.password && (
                  <p className="text-[12px] text-destructive" role="alert">{errors.password.message}</p>
                )}
              </div>

              <Button type="submit" className="w-full h-11 gap-2 rounded-[10px] text-[13px] mt-2" disabled={isSubmitting}>
                {isSubmitting ? t("common.loading") : <>{t("auth.signIn")} <ArrowRight className="h-4 w-4" /></>}
              </Button>
            </form>

            <p className="text-[12px] text-muted-foreground text-center">
              {t("auth.noAccount")}{" "}
              <Link href="/register" className="text-primary hover:underline font-medium">
                {t("auth.register")}
              </Link>
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
