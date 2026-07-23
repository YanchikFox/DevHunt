"use client"

import { useCallback, useEffect, useState } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { useRegister, useCheckUsername } from "@/lib/api/queries/auth"
import { useRouter } from "@/i18n/routing"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import {
  MIN_PASSWORD_LENGTH,
  MAX_PASSWORD_LENGTH,
  PASSWORD_COMPLEXITY_REGEX,
  getPasswordRequirements,
} from "@/lib/validation/password"

// ── Constants & Schema ───────────────────────────────────────────

export const USERNAME_FORMAT = /^[a-zA-Z0-9_\-.]+$/

const createPasswordSchema = (t: (key: string) => string) =>
  z
    .string()
    .min(MIN_PASSWORD_LENGTH, t("auth.passwordMinLength8"))
    .max(MAX_PASSWORD_LENGTH, t("auth.passwordMaxLength"))
    .regex(PASSWORD_COMPLEXITY_REGEX, t("auth.passwordComplexity"))

const createRegisterSchema = (t: (key: string) => string) =>
  z
    .object({
      email: z.string().email(t("auth.invalidEmail")),
      password: createPasswordSchema(t),
      confirmPassword: createPasswordSchema(t),
      fullName: z.string().min(2, t("auth.fullNameMinLength")),
      username: z
        .string()
        .regex(USERNAME_FORMAT, t("auth.usernameInvalidChars"))
        .min(3, t("auth.usernameMinLength"))
        .max(50)
        .optional()
        .or(z.literal("")),
    })
    .refine((data) => data.password === data.confirmPassword, {
      message: t("auth.passwordsDontMatch"),
      path: ["confirmPassword"],
    })

export type RegisterForm = z.infer<ReturnType<typeof createRegisterSchema>>

// ── Error type guard (F-05: no forced casts) ────────────────────

function hasUserMessage(err: unknown): err is { userMessage: string } {
  if (typeof err !== "object" || err === null) return false
  if (!("userMessage" in err)) return false
  return typeof (err as { userMessage: unknown }).userMessage === "string"
}

function extractErrorMessage(error: unknown, fallback: string): string {
  if (hasUserMessage(error)) return error.userMessage
  if (error instanceof Error) return error.message
  return fallback
}

// ── Sub-hooks ────────────────────────────────────────────────────

function useUsernameDebounce() {
  const [rawUsername, setRawUsername] = useState("")
  const [debouncedUsername, setDebouncedUsername] = useState("")

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedUsername(rawUsername), 400)
    return () => clearTimeout(timer)
  }, [rawUsername])

  const { data: usernameCheck, isFetching: checkingUsername } = useCheckUsername(debouncedUsername)

  const handleUsernameChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setRawUsername(e.target.value),
    [],
  )

  return { debouncedUsername, usernameCheck, checkingUsername, handleUsernameChange }
}

function useOAuth() {
  const startOAuth = useCallback((provider: "github" | "google") => {
    const authApi =
      process.env.NEXT_PUBLIC_AUTH_API_URL ||
      process.env.AUTH_SERVICE_URL ||
      "http://localhost:7001"
    const callbackUrl = `${window.location.origin}/api/auth/callback/credentials`
    // When using proxy path (e.g. /api/proxy-auth), don't add /api prefix — proxy adds it
    const loginPath = authApi.startsWith("/")
      ? `${authApi}/auth/login/${provider}`
      : `${authApi}/api/auth/login/${provider}`
    const url = `${loginPath}?redirectUri=${encodeURIComponent(callbackUrl)}`
    window.location.href = url
  }, [])

  const handleOAuthGithub = useCallback(() => startOAuth("github"), [startOAuth])

  return { handleOAuthGithub }
}

// ── Main hook ────────────────────────────────────────────────────

export function useRegisterForm() {
  const t = useTranslations()
  const router = useRouter()
  const { toast } = useToast()
  const registerMutation = useRegister()
  const [password, setPassword] = useState("")
  const passwordRequirements = getPasswordRequirements(t)

  const {
    register: registerField,
    handleSubmit,
    formState: { errors },
  } = useForm<RegisterForm>({
    resolver: zodResolver(createRegisterSchema(t)),
  })

  const { debouncedUsername, usernameCheck, checkingUsername, handleUsernameChange } = useUsernameDebounce()
  const { handleOAuthGithub } = useOAuth()

  const handlePasswordChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setPassword(e.target.value),
    [],
  )

  const onSubmit = useCallback(
    async (data: RegisterForm) => {
      try {
        const result = await registerMutation.mutateAsync({
          email: data.email,
          password: data.password,
          fullName: data.fullName,
          username: data.username?.trim() || undefined,
        })
        toast({ title: t("common.success"), description: t("auth.accountCreatedSuccessfully") })
        router.push(`/verify-email?userId=${result.userId}&email=${encodeURIComponent(data.email)}`)
      } catch (error: unknown) {
        toast({
          title: t("common.error"),
          description: extractErrorMessage(error, t("auth.registrationFailed")),
          variant: "destructive",
        })
      }
    },
    [registerMutation, toast, t, router],
  )

  return {
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
    isPending: registerMutation.isPending,
  }
}
