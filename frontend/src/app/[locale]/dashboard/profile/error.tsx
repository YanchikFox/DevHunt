"use client"

import { useEffect, useCallback } from "react"

export default function ErrorPage({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    console.error("Profile Route Error:", error)

    void fetch("/api/client-error", {
      method: "POST",
      headers: { "content-type": "application/json" },
      keepalive: true,
      body: JSON.stringify({
        scope: "[locale]/dashboard/profile",
        message: error?.message,
        stack: error?.stack,
        digest: error?.digest,
        href: typeof window !== "undefined" ? window.location.href : undefined,
        userAgent: typeof navigator !== "undefined" ? navigator.userAgent : undefined,
        ts: new Date().toISOString(),
      }),
    }).catch(() => {
      // ignore
    })
  }, [error])

  const handleReload = useCallback(() => {
    window.location.reload()
  }, [])

  return (
    <div className="min-h-[50vh] flex flex-col items-center justify-center gap-4 p-6">
      <h1 className="text-xl font-semibold">Profile error</h1>
      <p className="text-sm text-muted-foreground text-center max-w-xl break-words">
        {error?.message || "An unexpected error occurred."}
      </p>
      <div className="flex gap-3">
        <button
          onClick={reset}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition"
        >
          Try again
        </button>
        <button
          onClick={handleReload}
          className="px-4 py-2 bg-secondary text-secondary-foreground rounded-md hover:bg-secondary/90 transition"
        >
          Reload
        </button>
      </div>
    </div>
  )
}
