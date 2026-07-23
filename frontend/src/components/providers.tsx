"use client"

import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { ReactQueryDevtools } from "@tanstack/react-query-devtools"
import { SessionProvider, useSession, signOut } from "next-auth/react"
import { ThemeProvider } from "next-themes"
import { useState, useEffect } from "react"
import { Toaster } from "@/components/ui/toaster"
import type { Session } from "next-auth"

function SessionErrorHandler() {
  const { data: session } = useSession()

  // Handle RefreshTokenError — session is stale and cannot be refreshed.
  useEffect(() => {
    if (session?.error === "RefreshTokenError") {
      signOut({ redirect: false })
    }
  }, [session?.error])

  return null
}

export function Providers({ children, session }: { children: React.ReactNode; session?: Session | null }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 60 * 1000, // 1 minute
            refetchOnWindowFocus: false,
          },
        },
      })
  )

  return (
    <SessionProvider session={session} refetchInterval={4 * 60} refetchOnWindowFocus={true}>
      <QueryClientProvider client={queryClient}>
        <SessionErrorHandler />
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
          {children}
          <Toaster />
        </ThemeProvider>
        {process.env.NODE_ENV === "development" && <ReactQueryDevtools initialIsOpen={false} />}
      </QueryClientProvider>
    </SessionProvider>
  )
}
