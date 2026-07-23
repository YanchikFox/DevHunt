"use client"

import { useEffect, useState, useRef, useCallback } from "react"
import { useSearchParams } from "next/navigation"
import { useRouter } from "@/i18n/routing"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Label } from "@/components/ui/label"
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { useToast } from "@/hooks/use-toast"
import { MailCheck, RefreshCcw, CheckCircle2 } from "lucide-react"

const authApi =
  process.env.NEXT_PUBLIC_AUTH_API_URL ||
  process.env.AUTH_SERVICE_URL ||
  "http://localhost:7001"

function buildAuthUrl(path: string): string {
  const normalized = path.startsWith("/") ? path : `/${path}`
  if (authApi.startsWith("/")) {
    return `${authApi}${normalized}`
  }
  return `${authApi}/api${normalized}`
}

export default function VerifyEmailPage() {
  const searchParams = useSearchParams()
  const router = useRouter()
  const { toast } = useToast()

  const [code, setCode] = useState("")
  const [userId, setUserId] = useState("")
  const [email, setEmail] = useState("")
  const [isVerifying, setIsVerifying] = useState(false)
  const [isResending, setIsResending] = useState(false)
  const [resendCooldown, setResendCooldown] = useState(0)
  const [isAutoVerifying, setIsAutoVerifying] = useState(false)
  const [verified, setVerified] = useState(false)
  const cooldownRef = useRef<NodeJS.Timeout | null>(null)

  const handleVerify = useCallback(async (verificationCode: string, targetUserId: string) => {
    if (!verificationCode || !targetUserId) {
      toast({
        title: "Missing data",
        description: "Please enter the verification code from your email.",
        variant: "destructive",
      })
      setIsAutoVerifying(false)
      return
    }

    setIsVerifying(true)
    try {
      const response = await fetch(buildAuthUrl("/auth/verify-email"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userId: targetUserId, code: verificationCode }),
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || "Verification failed")
      }

      setVerified(true)
      toast({
        title: "Email verified!",
        description: "Your email has been verified. Redirecting to login...",
      })

      // Redirect after short delay
      setTimeout(() => {
        router.push("/login?callbackUrl=/register/complete-profile")
      }, 2000)
    } catch (err) {
      const message = err instanceof Error ? err.message : "Verification failed"
      toast({
        title: "Verification failed",
        description: message,
        variant: "destructive",
      })
      setIsAutoVerifying(false)
    } finally {
      setIsVerifying(false)
    }
  }, [toast, router])

  // Cooldown timer effect
  useEffect(() => {
    if (resendCooldown > 0) {
      cooldownRef.current = setTimeout(() => {
        setResendCooldown((prev) => prev - 1)
      }, 1000)
    }
    return () => {
      if (cooldownRef.current) clearTimeout(cooldownRef.current)
    }
  }, [resendCooldown])

  useEffect(() => {
    const codeParam = searchParams.get("code")
    const userIdParam = searchParams.get("userId")
    const emailParam = searchParams.get("email")

    if (codeParam) setCode(codeParam)
    if (userIdParam) setUserId(userIdParam)
    if (emailParam) setEmail(decodeURIComponent(emailParam))

    if (codeParam && userIdParam) {
      setIsAutoVerifying(true)
      void handleVerify(codeParam, userIdParam)
    }
  }, [searchParams, handleVerify])

  const handleGoToLogin = useCallback(() => router.push("/login"), [router])
  const handleCodeChange = useCallback((e: { target: { value: string } }) => setCode(e.target.value), [])
  const handleVerifyCode = useCallback(() => { void handleVerify(code, userId) }, [code, userId, handleVerify])
  const handleEmailChange = useCallback((e: { target: { value: string } }) => setEmail(e.target.value), [])

  const handleResend = async () => {
    if (!email) {
      toast({
        title: "Email required",
        description: "Enter your email to resend the verification code.",
        variant: "destructive",
      })
      return
    }

    if (resendCooldown > 0) {
      toast({
        title: "Please wait",
        description: `You can resend in ${resendCooldown} seconds.`,
        variant: "destructive",
      })
      return
    }

    setIsResending(true)
    try {
      const response = await fetch(buildAuthUrl("/auth/resend-verification"), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      })

      if (!response.ok) {
        const message = await response.text()
        throw new Error(message || "Unable to resend code")
      }

      const data = await response.json()
      if (data.userId) {
        setUserId(data.userId)
      }

      setResendCooldown(60)

      toast({
        title: "Verification sent!",
        description: "Check your inbox for the new verification code.",
      })
    } catch (err) {
      const message = err instanceof Error ? err.message : "Unable to resend code"
      toast({
        title: "Resend failed",
        description: message,
        variant: "destructive",
      })
    } finally {
      setIsResending(false)
    }
  }

  // Success state
  if (verified) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
        <Card className="w-full max-w-md shadow-xl">
          <CardHeader className="space-y-2 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-green-100">
              <CheckCircle2 className="h-6 w-6 text-green-600" />
            </div>
            <CardTitle className="text-3xl font-bold">Email Verified!</CardTitle>
            <CardDescription>
              Your email has been verified successfully. Redirecting you to login...
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button className="w-full" onClick={handleGoToLogin}>
              Go to Login
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  // Auto-verifying state (came from email link)
  if (isAutoVerifying) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
        <Card className="w-full max-w-md shadow-xl">
          <CardHeader className="space-y-2 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
              <MailCheck className="h-6 w-6 text-primary animate-pulse" />
            </div>
            <CardTitle className="text-3xl font-bold">Verifying...</CardTitle>
            <CardDescription>
              Please wait while we verify your email address.
            </CardDescription>
          </CardHeader>
        </Card>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-background via-background to-muted/20 p-4">
      <Card className="w-full max-w-md shadow-xl">
        <CardHeader className="space-y-2 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
            <MailCheck className="h-6 w-6 text-primary" />
          </div>
          <CardTitle className="text-3xl font-bold">Check your email</CardTitle>
          <CardDescription>
            We&apos;ve sent a verification link to{" "}
            {email ? <strong>{email}</strong> : "your email address"}.
            Click the link in the email to verify your account.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {/* Manual code entry - show when we have userId to verify */}
          {userId && (
            <>
              <div className="space-y-2">
                <Label htmlFor="code">Enter the 6-digit verification code</Label>
                <Input
                  id="code"
                  value={code}
                  onChange={handleCodeChange}
                  placeholder="Enter the 6-digit code"
                  maxLength={6}
                />
              </div>
              <Button
                className="w-full"
                disabled={isVerifying || !code || code.length < 6}
                onClick={handleVerifyCode}
              >
                {isVerifying ? "Verifying..." : "Verify email"}
              </Button>
              <div className="h-px bg-border" />
            </>
          )}

          {/* Resend section */}
          <div className="space-y-2">
            <Label htmlFor="email">Didn&apos;t receive the email?</Label>
            <Input
              id="email"
              type="email"
              value={email}
              onChange={handleEmailChange}
              placeholder="you@example.com"
              disabled={!!email}
            />
          </div>
          <Button
            variant="outline"
            disabled={isResending || resendCooldown > 0}
            onClick={handleResend}
            className="w-full"
          >
            <RefreshCcw className={`mr-2 h-4 w-4 ${isResending ? 'animate-spin' : ''}`} />
            {resendCooldown > 0
              ? `Resend in ${resendCooldown}s`
              : isResending
                ? "Sending..."
                : "Resend verification email"}
          </Button>

          <p className="text-center text-sm text-muted-foreground">
            Check your spam folder if you don&apos;t see the email.
          </p>
        </CardContent>
      </Card>
    </div>
  )
}
