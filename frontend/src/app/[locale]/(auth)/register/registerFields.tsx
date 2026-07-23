"use client"

import { type ReactNode } from "react"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { User, AtSign, Mail, Lock, CheckCircle2, Check, X, Loader2 } from "lucide-react"
import { useTranslations } from "next-intl"
import type { UseFormRegisterReturn } from "react-hook-form"
import { USERNAME_FORMAT } from "./useRegisterForm"
import type { PasswordRequirement } from "@/lib/validation/password"

// ── Shared base ──────────────────────────────────────────────────

interface FormFieldProps {
  readonly id: string
  readonly label: string
  readonly icon: ReactNode
  readonly registration: UseFormRegisterReturn
  readonly error?: string
  readonly type?: string
  readonly placeholder?: string
  readonly autoComplete?: string
  readonly className?: string
  readonly trailing?: ReactNode
  readonly children?: ReactNode
}

function FormField({
  id, label, icon, registration, error,
  type = "text", placeholder, autoComplete, className,
  trailing, children,
}: FormFieldProps) {
  return (
    <div className="space-y-2">
      <Label htmlFor={id} className="flex items-center gap-2">
        {icon}
        {label}
        {trailing}
      </Label>
      <Input
        id={id}
        type={type}
        placeholder={placeholder}
        autoComplete={autoComplete}
        {...registration}
        aria-invalid={error ? "true" : "false"}
        className={`${className ?? ""} ${error ? "border-destructive" : ""}`.trim()}
      />
      {children}
      {error && (
        <p className="text-sm text-destructive flex items-center gap-1" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

// ── Simple fields (config-driven to avoid duplication) ───────────

interface SimpleFieldConfig {
  readonly id: string
  readonly labelKey: string
  readonly placeholderKey: string
  readonly autoComplete: string
  readonly icon: ReactNode
  readonly type?: string
}

const SIMPLE_FIELDS: Record<"fullName" | "email" | "confirmPassword", SimpleFieldConfig> = {
  fullName: { id: "fullName", labelKey: "auth.name", placeholderKey: "auth.namePlaceholder", autoComplete: "name", icon: <User className="h-4 w-4" /> },
  email: { id: "email", labelKey: "auth.email", placeholderKey: "auth.emailPlaceholder", autoComplete: "email", type: "email", icon: <Mail className="h-4 w-4" /> },
  confirmPassword: { id: "confirmPassword", labelKey: "auth.confirmPassword", placeholderKey: "auth.passwordPlaceholder", autoComplete: "new-password", type: "password", icon: <Lock className="h-4 w-4" /> },
}

interface SimpleFieldProps {
  readonly registration: UseFormRegisterReturn
  readonly error?: string
}

function SimpleField({ config, registration, error }: SimpleFieldProps & { readonly config: SimpleFieldConfig }) {
  const t = useTranslations()
  return (
    <FormField
      id={config.id}
      label={t(config.labelKey)}
      icon={config.icon}
      type={config.type}
      placeholder={t(config.placeholderKey)}
      autoComplete={config.autoComplete}
      registration={registration}
      error={error}
    />
  )
}

export function FullNameField(props: SimpleFieldProps) {
  return <SimpleField config={SIMPLE_FIELDS.fullName} {...props} />
}

export function EmailField(props: SimpleFieldProps) {
  return <SimpleField config={SIMPLE_FIELDS.email} {...props} />
}

export function ConfirmPasswordField(props: SimpleFieldProps) {
  return <SimpleField config={SIMPLE_FIELDS.confirmPassword} {...props} />
}

// ── Username Field ───────────────────────────────────────────────

interface UsernameFieldProps {
  readonly registration: UseFormRegisterReturn
  readonly error?: string
  readonly debouncedUsername: string
  readonly checkingUsername: boolean
  readonly usernameAvailable?: boolean
}

export function UsernameField({
  registration,
  error,
  debouncedUsername,
  checkingUsername,
  usernameAvailable,
}: UsernameFieldProps) {
  const t = useTranslations()
  return (
    <div className="space-y-2">
      <Label htmlFor="username" className="flex items-center gap-2">
        <AtSign className="h-4 w-4" />
        {t("auth.usernamePlaceholder")}
        <span className="ml-auto text-xs text-muted-foreground">{t("common.optional")}</span>
      </Label>
      <div className="relative">
        <Input
          id="username"
          placeholder="cool_dev42"
          autoComplete="username"
          {...registration}
          aria-invalid={error ? "true" : "false"}
          className={`pr-9 ${error ? "border-destructive" : ""}`}
        />
        <div className="absolute right-3 top-1/2 -translate-y-1/2">
          <UsernameStatusIcon
            debouncedUsername={debouncedUsername}
            checkingUsername={checkingUsername}
            usernameAvailable={usernameAvailable}
          />
        </div>
      </div>
      {error ? (
        <p className="text-sm text-destructive" role="alert">{error}</p>
      ) : (
        <UsernameHint
          debouncedUsername={debouncedUsername}
          checkingUsername={checkingUsername}
          usernameAvailable={usernameAvailable}
        />
      )}
    </div>
  )
}

function UsernameStatusIcon({
  debouncedUsername,
  checkingUsername,
  usernameAvailable,
}: {
  readonly debouncedUsername: string
  readonly checkingUsername: boolean
  readonly usernameAvailable?: boolean
}) {
  const isActive = debouncedUsername.length >= 3 && USERNAME_FORMAT.test(debouncedUsername)
  if (!isActive) return null
  if (checkingUsername) return <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
  if (usernameAvailable === true) return <Check className="h-4 w-4 text-green-500" />
  if (usernameAvailable === false) return <X className="h-4 w-4 text-destructive" />
  return null
}

function UsernameHint({
  debouncedUsername,
  checkingUsername,
  usernameAvailable,
}: {
  readonly debouncedUsername: string
  readonly checkingUsername: boolean
  readonly usernameAvailable?: boolean
}) {
  const t = useTranslations()
  const isActive = debouncedUsername.length >= 3 && USERNAME_FORMAT.test(debouncedUsername)
  if (!isActive) return null
  if (checkingUsername) return <span className="text-xs text-muted-foreground">{t("auth.usernameChecking")}</span>
  if (usernameAvailable === true) return <span className="text-xs text-green-600">{t("auth.usernameAvailable")}</span>
  if (usernameAvailable === false) return <span className="text-xs text-destructive">{t("auth.usernameTaken")}</span>
  return null
}

// ── Email Field ──────────────────────────────────────────────────
// (Re-exported via SimpleField above)

// ── Password Field ───────────────────────────────────────────────

interface PasswordFieldProps {
  readonly registration: UseFormRegisterReturn
  readonly error?: string
  readonly password: string
  readonly requirements: readonly PasswordRequirement[]
}

export function PasswordField({ registration, error, password, requirements }: PasswordFieldProps) {
  const t = useTranslations()
  return (
    <FormField
      id="password"
      label={t("auth.password")}
      icon={<Lock className="h-4 w-4" />}
      type="password"
      placeholder={t("auth.passwordPlaceholder")}
      autoComplete="new-password"
      registration={registration}
      error={error}
    >
      {password && <PasswordRequirementsList requirements={requirements} password={password} />}
    </FormField>
  )
}

function PasswordRequirementsList({
  requirements,
  password,
}: {
  readonly requirements: readonly PasswordRequirement[]
  readonly password: string
}) {
  const t = useTranslations()
  return (
    <div className="space-y-1 rounded-md bg-muted/50 p-3">
      <p className="text-xs font-medium text-muted-foreground">
        {t("auth.passwordRequirements", { defaultValue: "Password requirements:" })}
      </p>
      <ul className="space-y-1">
        {requirements.map((req) => {
          const satisfied = req.test(password)
          return (
            <li
              key={req.name}
              className={`flex items-center gap-2 text-xs ${satisfied ? "text-green-600" : "text-muted-foreground"}`}
            >
              {satisfied ? (
                <CheckCircle2 className="h-3 w-3" />
              ) : (
                <div className="h-3 w-3 rounded-full border border-current" />
              )}
              {req.label}
            </li>
          )
        })}
      </ul>
    </div>
  )
}

// ── Confirm Password Field ───────────────────────────────────────
// (Re-exported via SimpleField above)
