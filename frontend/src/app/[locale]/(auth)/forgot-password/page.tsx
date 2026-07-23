"use client"

import { useTranslations } from "next-intl"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { useState, useCallback, useEffect, useRef, FormEvent, ChangeEvent } from "react"
import { authClient } from "@/lib/api/client"
import { AlertCircle, CheckCircle2, ArrowLeft } from "lucide-react"
import Link from "next/link"

interface ApiErrorResponse {
  response?: {
    status?: number
  }
}

export default function ForgotPasswordPage() {
  const t = useTranslations()
  const [email, setEmail] = useState("")
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [cooldown, setCooldown] = useState(0)
  const cooldownRef = useRef<NodeJS.Timeout | null>(null)

  // Cooldown timer effect
  useEffect(() => {
    if (cooldown > 0) {
      cooldownRef.current = setTimeout(() => {
        setCooldown((prev) => prev - 1)
      }, 1000)
    }
    return () => {
      if (cooldownRef.current) clearTimeout(cooldownRef.current)
    }
  }, [cooldown])

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault()
      setError(null)

      if (!email.trim()) {
        setError("Please enter your email address.")
        return
      }

      if (cooldown > 0) {
        setError(`Please wait ${cooldown} seconds before trying again.`)
        return
      }

      setIsSubmitting(true)

      try {
        await authClient.post("/auth/forgot-password", { email: email.trim() })
        setSubmitted(true)
        setCooldown(60) // 60 second cooldown
      } catch (err: unknown) {
        const apiErr = err as ApiErrorResponse
        if (apiErr.response?.status === 429) {
          setError("Too many requests. Please wait a minute before trying again.")
          setCooldown(60)
        } else {
          // Always show success to prevent email enumeration
          setSubmitted(true)
          setCooldown(60)
        }
      } finally {
        setIsSubmitting(false)
      }
    },
    [email, cooldown]
  )

  const handleTryAgain = useCallback(() => {
    if (cooldown > 0) {
      setError(`Please wait ${cooldown} seconds before trying again.`)
      return
    }
    setSubmitted(false)
    setError(null)
  }, [cooldown])

  const handleEmailChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    setEmail(event.target.value)
  }, [])

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
      <Card className="w-full max-w-md shadow-xl">
        <CardHeader className="space-y-2 text-center">
          {submitted ? (
            <CheckCircle2 className="mx-auto h-12 w-12 text-green-500" />
          ) : null}
          <CardTitle className="text-3xl font-bold">
            {t("auth.forgotPassword", { defaultValue: "Forgot password" })}
          </CardTitle>
          <CardDescription>
            {submitted
              ? "If this email exists in our system, you will receive a password reset link shortly. Please check your inbox and spam folder."
              : "Enter your email address and we'll send you a link to reset your password."}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {submitted ? (
            <div className="space-y-4">
              <Button asChild variant="outline" className="w-full">
                <Link href="/login">
                  <ArrowLeft className="mr-2 h-4 w-4" />
                  Back to Login
                </Link>
              </Button>
              <p className="text-center text-sm text-muted-foreground">
                Didn&apos;t receive the email?{" "}
                <button
                  type="button"
                  onClick={handleTryAgain}
                  disabled={cooldown > 0}
                  className={`text-primary hover:underline ${cooldown > 0 ? 'opacity-50 cursor-not-allowed' : ''}`}
                >
                  {cooldown > 0 ? `Try again in ${cooldown}s` : "Try again"}
                </button>
              </p>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {error && (
                <div className="flex items-center gap-2 rounded-md bg-destructive/10 p-3 text-sm text-destructive">
                  <AlertCircle className="h-4 w-4" />
                  {error}
                </div>
              )}
              <div className="space-y-2">
                <Label htmlFor="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  placeholder="you@example.com"
                  value={email}
                  onChange={handleEmailChange}
                  required
                  disabled={isSubmitting}
                />
              </div>
              <Button type="submit" className="w-full" disabled={isSubmitting}>
                {isSubmitting ? "Sending..." : "Send Reset Link"}
              </Button>
              <p className="text-center text-sm text-muted-foreground">
                Remember your password?{" "}
                <Link href="/login" className="text-primary hover:underline">
                  Back to Login
                </Link>
              </p>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
