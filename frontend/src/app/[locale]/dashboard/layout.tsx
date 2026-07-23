"use client"

import { useState, useCallback, useEffect } from "react"
import { useTranslations } from "next-intl"
import { Link } from "@/i18n/routing"
import { Menu, X, Bell } from "lucide-react"
import { cn } from "@/lib/utils"
import { USE_MOCKS } from "@/lib/feature-flags"
import { FloatingChatWidget } from "@/components/chat/FloatingChatWidget"
import { useDashboardAuth } from "./useDashboardAuth"
import { useSidebar } from "./useSidebar"
import { getNavItems } from "./dashboardNav"
import { DashboardSidebar } from "./DashboardSidebar"
import { DashboardHeader } from "./DashboardHeader"

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const { user, isLoading, profile, mounted, handleLogout, displayName, isAuthLoading } = useDashboardAuth()
  const { collapsed, toggle } = useSidebar()
  const t = useTranslations()
  const navItems = getNavItems(profile?.role)

  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const handleMobileNavClose = useCallback(() => setMobileNavOpen(false), [])
  const handleMobileNavToggle = useCallback(() => setMobileNavOpen(prev => !prev), [])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setMobileNavOpen(false)
    }
    document.addEventListener("keydown", onKeyDown)
    return () => document.removeEventListener("keydown", onKeyDown)
  }, [])

  if (isAuthLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="text-center">
          <div className="inline-block h-7 w-7 animate-spin rounded-full border-[3px] border-solid border-primary border-r-transparent" />
          <p className="mt-3 text-[13px] text-muted-foreground">{t("common.loading")}</p>
        </div>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-background text-foreground">
      <DashboardSidebar
        navItems={navItems}
        collapsed={collapsed}
        onToggle={toggle}
        onLogout={handleLogout}
        mobileOpen={mobileNavOpen}
        onMobileClose={handleMobileNavClose}
      />

      <DashboardHeader
        sidebarCollapsed={collapsed}
        mounted={mounted}
        profile={profile}
        user={user ?? undefined}
        displayName={displayName}
        onLogout={handleLogout}
      />

      {/* Mobile nav bar */}
      <div className="lg:hidden fixed top-0 left-0 h-14 flex items-center px-3 z-40 bg-background border-b border-border w-full justify-between gap-2">
        <button
          type="button"
          aria-label={mobileNavOpen ? t("navigation.closeMenu") : t("navigation.openMenu")}
          aria-expanded={mobileNavOpen}
          aria-controls="mobile-nav-drawer"
          onClick={handleMobileNavToggle}
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] text-muted-foreground hover:text-foreground hover:bg-muted/60 transition-colors"
        >
          {mobileNavOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
        </button>

        <Link href="/dashboard" className="flex items-center gap-2">
          <div className="flex h-7 w-7 items-center justify-center rounded-md bg-primary text-primary-foreground shrink-0">
            <span className="font-mono font-bold text-xs">◢</span>
          </div>
          <span className="text-[15px] font-semibold tracking-[-0.3px] text-foreground">DevHunt</span>
        </Link>

        <Link
          href="/dashboard/notifications"
          aria-label={t("navigation.notifications")}
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] text-muted-foreground hover:text-foreground hover:bg-muted/60 transition-colors"
        >
          <Bell className="h-4 w-4" />
        </Link>
      </div>

      <main
        className={cn(
          "pt-14 min-h-screen bg-background transition-[margin] duration-300 ease-in-out",
          collapsed ? "lg:ml-[60px]" : "lg:ml-60"
        )}
      >
        <div className="p-5 lg:p-7 fade-in">
          <MainContent mounted={mounted} isLoading={isLoading} user={user} profile={profile}>
            {children}
          </MainContent>
        </div>
      </main>

      <FloatingChatWidget />
    </div>
  )
}

/* ── Content gate: replaces the 4-branch ternary ── */

function MainContent({
  mounted, isLoading, user, profile, children,
}: {
  mounted: boolean
  isLoading: boolean
  user: unknown
  profile: unknown
  children: React.ReactNode
}) {
  const t = useTranslations()

  if (!mounted) return <LoadingState t={t} />
  if (USE_MOCKS && isLoading) return <LoadingState t={t} />

  const hasAuth = USE_MOCKS ? Boolean(user) : Boolean(profile)
  if (!hasAuth) {
    return (
      <div className="flex min-h-[calc(100vh-8rem)] flex-col items-center justify-center gap-4">
        <p className="text-muted-foreground text-sm">{t("common.profileLoadError")}</p>
        <button
          type="button"
          onClick={() => window.location.reload()}
          className="text-sm text-primary hover:underline"
        >
          {t("common.retry")}
        </button>
      </div>
    )
  }

  return <>{children}</>
}

function LoadingState({ t }: { t: (key: string) => string }) {
  return (
    <div className="flex min-h-[calc(100vh-8rem)] items-center justify-center">
      <div className="text-center space-y-4">
        <div className="h-10 w-10 border-4 border-primary border-t-transparent rounded-full animate-spin mx-auto" />
        <p className="text-muted-foreground font-medium animate-pulse">{t("common.loading")}</p>
      </div>
    </div>
  )
}
