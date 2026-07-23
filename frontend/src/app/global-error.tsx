"use client"

import { useEffect, useCallback } from "react"

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string }
  reset: () => void
}) {
  useEffect(() => {
    console.error("Global Error:", error)

    // Also report to server logs (useful when the browser stack is minified)
    void fetch("/api/client-error", {
      method: "POST",
      headers: { "content-type": "application/json" },
      keepalive: true,
      body: JSON.stringify({
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

  const handleGoHome = useCallback(() => {
    window.location.href = "/"
  }, [])

  return (
    <html>
      <body>
        <div className="min-h-screen flex items-center justify-center p-4 bg-background">
          <div className="max-w-2xl w-full space-y-6">
            <div className="space-y-2">
              <h1 className="text-3xl font-bold text-destructive">Application Error</h1>
              <p className="text-muted-foreground">
                An unexpected error occurred in the application.
              </p>
            </div>

            <div className="bg-destructive/10 border border-destructive rounded-lg p-6 space-y-4">
              <div>
                <h3 className="font-semibold mb-2">Error Message:</h3>
                <p className="font-mono text-sm break-all bg-background p-3 rounded">
                  {error.message}
                </p>
              </div>

              {error.digest && (
                <div>
                  <h3 className="font-semibold mb-2">Error ID:</h3>
                  <p className="font-mono text-sm text-muted-foreground">{error.digest}</p>
                </div>
              )}
            </div>

            <details className="border rounded-lg p-4">
              <summary className="cursor-pointer font-semibold">View Stack Trace</summary>
              <pre className="mt-4 text-xs overflow-auto max-h-96 bg-muted p-4 rounded">
                {error.stack}
              </pre>
            </details>

            <div className="flex gap-4">
              <button
                onClick={reset}
                className="flex-1 px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90 transition"
              >
                Try again
              </button>
              <button
                onClick={handleGoHome}
                className="flex-1 px-4 py-2 bg-secondary text-secondary-foreground rounded-md hover:bg-secondary/90 transition"
              >
                Go to Home
              </button>
            </div>
          </div>
        </div>
      </body>
    </html>
  )
}
