"use client"

import { useEffect } from "react"
import { useTranslations } from "next-intl"

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  const t = useTranslations("common")

  useEffect(() => {
    console.error("User Profile Error:", error)
  }, [error])

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="max-w-md w-full space-y-4">
        <h2 className="text-2xl font-bold text-destructive">{t("somethingWentWrong")}</h2>
        <div className="bg-destructive/10 border border-destructive rounded-lg p-4">
          <p className="font-mono text-sm break-all">{error.message}</p>
        </div>
        <details className="text-sm">
          <summary className="cursor-pointer">{t("stackTrace")}</summary>
          <pre className="mt-2 text-xs overflow-auto max-h-64">{error.stack}</pre>
        </details>
        <button
          onClick={reset}
          className="w-full px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90"
        >
          {t("tryAgain")}
        </button>
      </div>
    </div>
  )
}
