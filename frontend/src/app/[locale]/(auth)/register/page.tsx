"use client"

import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"
import { ArrowRight, Github, Check } from "lucide-react"
import { useRegisterForm } from "./useRegisterForm"
import {
  FullNameField,
  UsernameField,
  EmailField,
  PasswordField,
  ConfirmPasswordField,
} from "./registerFields"

function AuthLeftPanel() {
  return (
    <div className="hidden md:flex flex-col justify-between bg-muted/40 border-r border-border p-12 h-full">
      <div className="flex items-center gap-2.5">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="text-primary">
          <path d="M4 20L12 4l8 16" /><path d="M8 14h8" />
        </svg>
        <span className="text-[15px] font-semibold tracking-tight">DevHunt</span>
      </div>

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

export default function RegisterPage() {
  const t = useTranslations()
  const {
    registerField,
    handleSubmit,
    errors,
    password,
    passwordRequirements,
    debouncedUsername,
    usernameCheck,
    checkingUsername,
    handleUsernameChange,
    handlePasswordChange,
    handleOAuthGithub,
    onSubmit,
    isPending,
  } = useRegisterForm()

  return (
    <div className="min-h-screen grid md:grid-cols-2">
      <AuthLeftPanel />

      {/* Form panel */}
      <div className="flex items-center justify-center p-10 overflow-y-auto">
        <div className="w-full max-w-[400px] py-8">
          {/* Tab switcher */}
          <div className="flex gap-1 mb-7 bg-muted/60 p-1 rounded-[10px] w-fit">
            <Link href="/login" className="px-4 py-1.5 rounded-[8px] text-[12px] font-medium text-muted-foreground hover:text-foreground transition-colors">
              Log in
            </Link>
            <span className="px-4 py-1.5 rounded-[8px] bg-card shadow-sm text-[12px] font-medium">
              Sign up
            </span>
          </div>

          <h2 className="font-serif text-[34px] font-normal tracking-[-0.03em] leading-none mb-2 text-foreground">
            Create your account.
          </h2>
          <p className="text-[14px] text-muted-foreground mb-8">
            Start your first project in under a minute.
          </p>

          <div className="space-y-3">
            <Button
              variant="outline"
              className="w-full h-11 gap-2 rounded-[10px] text-[13px] justify-center"
              type="button"
              onClick={handleOAuthGithub}
            >
              <Github className="h-4 w-4" />
              {t("auth.continueWithGitHub")}
            </Button>

            <div className="relative flex items-center gap-3 my-1">
              <div className="flex-1 h-px bg-border" />
              <span className="text-[11px] text-muted-foreground uppercase tracking-wider">or</span>
              <div className="flex-1 h-px bg-border" />
            </div>

            <form onSubmit={handleSubmit(onSubmit)} className="space-y-3">
              <FullNameField
                registration={registerField("fullName")}
                error={errors.fullName?.message}
              />
              <UsernameField
                registration={registerField("username", { onChange: handleUsernameChange })}
                error={errors.username?.message}
                debouncedUsername={debouncedUsername}
                checkingUsername={checkingUsername}
                usernameAvailable={usernameCheck?.available}
              />
              <EmailField
                registration={registerField("email")}
                error={errors.email?.message}
              />
              <PasswordField
                registration={registerField("password", { onChange: handlePasswordChange })}
                error={errors.password?.message}
                password={password}
                requirements={passwordRequirements}
              />
              <ConfirmPasswordField
                registration={registerField("confirmPassword")}
                error={errors.confirmPassword?.message}
              />

              <Button type="submit" className="w-full h-11 gap-2 rounded-[10px] text-[13px] mt-2" disabled={isPending}>
                {isPending ? t("common.loading") : <>{t("auth.signUp")} <ArrowRight className="h-4 w-4" /></>}
              </Button>
            </form>

            <p className="text-[12px] text-muted-foreground text-center">
              By continuing you accept our{" "}
              <Link href="/terms" className="text-primary hover:underline">Terms</Link>{" "}
              and{" "}
              <Link href="/privacy" className="text-primary hover:underline">Privacy Policy</Link>.
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}
