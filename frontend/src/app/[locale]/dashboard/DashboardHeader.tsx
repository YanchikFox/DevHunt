"use client"

import { useTranslations } from "next-intl"
import { Link, usePathname } from "@/i18n/routing"
import { ThemeToggle } from "@/components/theme-toggle"
import { LanguageSwitcher } from "@/components/language-switcher"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { NotificationBellDropdown } from "@/components/notifications/NotificationBellDropdown"
import { cn } from "@/lib/utils"
import { GlobalSearch } from "@/components/search/GlobalSearch"
import { USE_MOCKS } from "@/lib/feature-flags"
import { Button } from "@/components/ui/button"
import { LogOut, Plus, Settings, User } from "lucide-react"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import type { UserProfile } from "@/lib/api/queries/profile"

interface DashboardHeaderProps {
  sidebarCollapsed: boolean
  mounted: boolean
  profile: UserProfile | undefined
  user: { email: string } | undefined
  displayName: string
  onLogout: () => void
}

const HEADER_NAV = [
  { href: "/dashboard", labelKey: "navigation.dashboard", index: "01" },
  { href: "/dashboard/projects", labelKey: "navigation.projects", index: "02" },
  { href: "/dashboard/teams", labelKey: "navigation.teams", index: "03" },
  { href: "/dashboard/chats", labelKey: "navigation.chats", index: "04" },
  { href: "/dashboard/invitations", labelKey: "navigation.invitations", index: "05" },
  { href: "/dashboard/support", labelKey: "navigation.support", index: "06" },
  { href: "/dashboard/notifications", labelKey: "navigation.notifications", index: "07" },
] as const

function getHeaderContext(pathname: string) {
  const match = HEADER_NAV
    .filter((item) => pathname === item.href || pathname.startsWith(`${item.href}/`))
    .sort((a, b) => b.href.length - a.href.length)[0]

  return match ?? HEADER_NAV[0]
}

export function DashboardHeader({ sidebarCollapsed, mounted, profile, user, displayName, onLogout }: DashboardHeaderProps) {
  const t = useTranslations()
  const pathname = usePathname()
  const current = getHeaderContext(pathname)

  return (
    <header
      className={cn(
        "h-14 fixed top-0 right-0 z-20 hidden lg:flex items-center justify-between gap-3 px-[18px] transition-[left] duration-300 ease-in-out",
        "border-b border-border bg-background",
        sidebarCollapsed ? "left-[60px]" : "left-0 lg:left-60"
      )}
    >
      <div className="flex min-w-0 items-center gap-2">
        <span className="caption">[{current.index}]</span>
        <span className="text-[14px] font-medium text-foreground">{t(current.labelKey)}</span>
      </div>

      <div className="flex flex-1 items-center justify-end gap-2">
        <GlobalSearch className="w-[280px]" />
        <ThemeToggle />
        <LanguageSwitcher />
        <NotificationBellDropdown />

        <Link href="/dashboard/projects">
          <Button size="sm" className="h-8 rounded-[8px] gap-1.5 text-[12px] shadow-sm">
            <Plus className="h-3.5 w-3.5" />
            {t("common.newProject")}
          </Button>
        </Link>

        {mounted && (USE_MOCKS ? user : profile) && (
          <ProfileMenu profile={profile} displayName={displayName} onLogout={onLogout} />
        )}
      </div>
    </header>
  )
}

function ProfileMenu({
  profile,
  displayName,
  onLogout,
}: {
  profile: UserProfile | undefined
  displayName: string
  onLogout: () => void
}) {
  const t = useTranslations()
  const username = profile?.email?.split("@")[0] ?? "user"

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-full border border-transparent p-[2px] transition-colors hover:bg-bg-hover data-[state=open]:bg-bg-hover"
          aria-label={t("navigation.profile")}
        >
          <Avatar className="h-[30px] w-[30px] border border-border">
            <AvatarImage src={profile?.avatarUrl} alt={displayName} />
            <AvatarFallback className="bg-primary text-primary-foreground font-bold text-[11px]">
              {displayName.charAt(0).toUpperCase()}
            </AvatarFallback>
          </Avatar>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60 rounded-[14px] border-border bg-bg-elevated p-0 shadow-lg">
        <DropdownMenuItem asChild className="cursor-pointer rounded-none border-b border-border p-3 focus:bg-bg-hover">
          <Link href="/dashboard/profile" className="flex items-center gap-3">
            <Avatar className="h-9 w-9 border border-border">
              <AvatarImage src={profile?.avatarUrl} alt={displayName} />
              <AvatarFallback className="bg-primary text-primary-foreground font-bold text-[12px]">
                {displayName.charAt(0).toUpperCase()}
              </AvatarFallback>
            </Avatar>
            <span className="min-w-0">
              <span className="block truncate text-[13px] font-semibold text-foreground">{displayName}</span>
              <span className="block truncate font-mono text-[11px] text-muted-foreground">@{username}</span>
            </span>
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild className="cursor-pointer gap-2 px-3 py-2 text-[13px] focus:bg-bg-hover">
          <Link href="/dashboard/profile">
            <User className="h-4 w-4" />
            {t("navigation.profile")}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild className="cursor-pointer gap-2 px-3 py-2 text-[13px] focus:bg-bg-hover">
          <Link href="/dashboard/profile/privacy">
            <Settings className="h-4 w-4" />
            {t("privacy.privacySettings")}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator className="bg-border" />
        <DropdownMenuItem
          onClick={onLogout}
          className="cursor-pointer gap-2 px-3 py-2 text-[13px] text-destructive focus:bg-destructive/10 focus:text-destructive"
        >
          <LogOut className="h-4 w-4" />
          {t("auth.logout")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
