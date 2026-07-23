"use client"

import { useTranslations } from "next-intl"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useState, useCallback, FormEvent, ChangeEvent } from "react"
import { useSearchParams, useRouter } from "next/navigation"
import { authClient } from "@/lib/api/client"
import { AlertCircle, CheckCircle2, Eye, EyeOff } from "lucide-react"
import Link from "next/link"
import { getPasswordRequirements, isValidPassword } from "@/lib/validation/password"

interface ApiErrorResponse {
  response?: {
    data?: {
      message?: string
    } | string
  }
}

export default function ResetPasswordPage() {
  const t = useTranslations()
  const searchParams = useSearchParams()
  const router = useRouter()
  const token = searchParams.get("token") || ""

  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [showPassword, setShowPassword] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState(false)

  const passwordRequirements = getPasswordRequirements(t)
  const isPasswordValid = isValidPassword(password)
  const doPasswordsMatch = password === confirmPassword && confirmPassword.length > 0

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault()
      setError(null)

      if (!token) {
        setError("Invalid reset link. Please request a new password reset.")
        return
      }

      if (!isPasswordValid) {
        setError("Please meet all password requirements.")
        return
      }

      if (!doPasswordsMatch) {
        setError("Passwords do not match.")
        return
      }

      setIsSubmitting(true)

      try {
        await authClient.post("/auth/reset-password", {
          token,
          newPassword: password,
        })
        setSuccess(true)
        // Redirect to login after 3 seconds
        setTimeout(() => {
          router.push("/login")
        }, 3000)
      } catch (err: unknown) {
        const apiErr = err as ApiErrorResponse
        const data = apiErr.response?.data
        const message = typeof data === "object" ? data?.message : data
        setError(typeof message === "string" ? message : "Failed to reset password. The link may have expired.")
      } finally {
        setIsSubmitting(false)
      }
    },
    [token, password, isPasswordValid, doPasswordsMatch, router]
  )

  const handlePasswordChange = useCallback((e: ChangeEvent<HTMLInputElement>) => setPassword(e.target.value), [])
  const handleConfirmPasswordChange = useCallback((e: ChangeEvent<HTMLInputElement>) => setConfirmPassword(e.target.value), [])
  const handleToggleShowPassword = useCallback(() => setShowPassword(prev => !prev), [])

  if (!token) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
        <Card className="w-full max-w-md shadow-xl">
          <CardHeader className="space-y-2 text-center">
            <AlertCircle className="mx-auto h-12 w-12 text-destructive" />
            <CardTitle className="text-2xl font-bold">Invalid Reset Link</CardTitle>
            <CardDescription>
              This password reset link is invalid or has expired. Please request a new one.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild className="w-full">
              <Link href="/forgot-password">Request New Link</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  if (success) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
        <Card className="w-full max-w-md shadow-xl">
          <CardHeader className="space-y-2 text-center">
            <CheckCircle2 className="mx-auto h-12 w-12 text-green-500" />
            <CardTitle className="text-2xl font-bold">Password Reset Successfully!</CardTitle>
            <CardDescription>
              Your password has been updated. Redirecting you to login...
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button asChild className="w-full">
              <Link href="/login">Go to Login</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
      <Card className="w-full max-w-md shadow-xl">
        <CardHeader className="space-y-2 text-center">
          <CardTitle className="text-3xl font-bold">
            {t("auth.resetPassword", { defaultValue: "Reset Password" })}
          </CardTitle>
          <CardDescription>Enter your new password below.</CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="flex items-center gap-2 rounded-md bg-destructive/10 p-3 text-sm text-destructive">
                <AlertCircle className="h-4 w-4" />
                {error}
              </div>
            )}

            <div className="space-y-2">
              <Label htmlFor="password">New Password</Label>
              <div className="relative">
                <Input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={handlePasswordChange}
                  required
                  className="pr-10"
                />
                <button
                  type="button"
                  onClick={handleToggleShowPassword}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="confirmPassword">Confirm Password</Label>
              <Input
                id="confirmPassword"
                type={showPassword ? "text" : "password"}
                value={confirmPassword}
                onChange={handleConfirmPasswordChange}
                required
              />
              {confirmPassword && !doPasswordsMatch && (
                <p className="text-xs text-destructive">Passwords do not match</p>
              )}
            </div>

            <div className="space-y-1 rounded-md bg-muted/50 p-3">
              <p className="text-xs font-medium text-muted-foreground">Password requirements:</p>
              <ul className="space-y-1">
                {passwordRequirements.map((req) => (
                  <li
                    key={req.name}
                    className={`flex items-center gap-2 text-xs ${req.test(password) ? "text-green-600" : "text-muted-foreground"
                      }`}
                  >
                    {req.test(password) ? (
                      <CheckCircle2 className="h-3 w-3" />
                    ) : (
                      <div className="h-3 w-3 rounded-full border border-current" />
                    )}
                    {req.label}
                  </li>
                ))}
              </ul>
            </div>

            <Button
              type="submit"
              className="w-full"
              disabled={isSubmitting || !isPasswordValid || !doPasswordsMatch}
            >
              {isSubmitting ? "Resetting..." : "Reset Password"}
            </Button>

            <p className="text-center text-sm text-muted-foreground">
              Remember your password?{" "}
              <Link href="/login" className="text-primary hover:underline">
                Back to Login
              </Link>
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}
