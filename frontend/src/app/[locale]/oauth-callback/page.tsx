"use client"

import { useTranslations } from "next-intl"
import { useEffect } from "react"
import { useSearchParams } from "next/navigation"
import { signIn } from "next-auth/react"
import { useRouter } from "@/i18n/routing"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Loader2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { refreshCsrfToken } from "@/lib/api/csrf"

interface OAuthExchangeResponse {
  accessToken?: string
  refreshToken?: string
  userId?: string
  email?: string
}

function isOAuthExchangeResponse(value: unknown): value is Required<OAuthExchangeResponse> {
  if (!value || typeof value !== "object") return false
  const record = value as Record<string, unknown>
  return (
    typeof record.accessToken === "string" &&
    typeof record.refreshToken === "string" &&
    typeof record.userId === "string"
  )
}

function buildAuthExchangeUrl(authApi: string): string {
  if (authApi.startsWith("/")) {
    return `${authApi}/auth/oauth/exchange`
  }
  return `${authApi}/api/auth/oauth/exchange`
}

/**
 * Completes OAuth sign-in by redeeming a one-time server-issued code (no tokens in URL hash).
 */
export default function OAuthCallbackPage() {
  const t = useTranslations()
  const searchParams = useSearchParams()
  const router = useRouter()
  const { toast } = useToast()

  useEffect(() => {
    const code = searchParams.get("code")
    const error = searchParams.get("error")

    if (window.location.search) {
      window.history.replaceState(null, document.title, window.location.pathname)
    }

    if (error) {
      toast({
        title: t("auth.authenticationFailed"),
        description: error,
        variant: "destructive",
      })
      router.push("/login")
      return
    }

    if (!code) {
      toast({
        title: t("auth.authenticationFailed"),
        description: t("auth.missingTokensFromProvider"),
        variant: "destructive",
      })
      router.push("/login")
      return
    }

    const authApi =
      process.env.NEXT_PUBLIC_AUTH_API_URL ||
      process.env.AUTH_SERVICE_URL ||
      "/api/proxy-auth"

    const persistSession = async () => {
      try {
        const response = await fetch(buildAuthExchangeUrl(authApi), {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ code }),
          credentials: "include",
        })

        if (!response.ok) {
          toast({
            title: t("auth.authenticationFailed"),
            description: t("auth.missingTokensFromProvider"),
            variant: "destructive",
          })
          router.push("/login")
          return
        }

        const payload: unknown = await response.json()
        if (!isOAuthExchangeResponse(payload)) {
          toast({
            title: t("auth.authenticationFailed"),
            description: t("auth.missingTokensFromProvider"),
            variant: "destructive",
          })
          router.push("/login")
          return
        }

        const result = await signIn("credentials", {
          redirect: false,
          accessToken: payload.accessToken,
          refreshToken: payload.refreshToken,
          userId: payload.userId,
          email: payload.email ?? "",
        })

        if (result?.error) {
          toast({
            title: t("auth.authenticationFailed"),
            description: result.error,
            variant: "destructive",
          })
          router.push("/login")
          return
        }

        await refreshCsrfToken()
        router.push("/dashboard")
        router.refresh()
      } catch {
        toast({
          title: t("auth.authenticationFailed"),
          description: t("auth.missingTokensFromProvider"),
          variant: "destructive",
        })
        router.push("/login")
      }
    }

    void persistSession()
  }, [router, searchParams, t, toast])

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/40 p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="text-center">{t("auth.completingSignIn")}</CardTitle>
        </CardHeader>
        <CardContent className="flex items-center justify-center gap-3 text-muted-foreground">
          <Loader2 className="h-5 w-5 animate-spin" />
          {t("auth.pleaseWaitLoggingIn")}
        </CardContent>
      </Card>
    </div>
  )
}
