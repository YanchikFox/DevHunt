"use client"

import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"
import { ThemeToggle } from "@/components/theme-toggle"
import { LanguageSwitcher } from "@/components/language-switcher"
import { Link } from "@/i18n/routing"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { useCurrentUser } from "@/hooks/use-current-user"
import { GlobalSearch } from "@/components/search/GlobalSearch"
import { User } from "lucide-react"

function DevHuntLogo() {
  return (
    <Link href="/" className="flex items-center gap-2.5 shrink-0">
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="text-primary">
        <path d="M4 20L12 4l8 16" />
        <path d="M8 14h8" />
      </svg>
      <span className="text-[15px] font-semibold tracking-tight text-foreground">DevHunt</span>
    </Link>
  )
}

export function Header() {
  const t = useTranslations()
  const { user } = useCurrentUser()

  return (
    <header className="sticky top-0 z-50 w-full border-b border-border/60 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
      <div className="flex h-[60px] items-center justify-between px-8 max-w-[1200px] mx-auto">
        <div className="flex items-center gap-8">
          <DevHuntLogo />

          {!user && (
            <nav className="hidden md:flex items-center gap-6" aria-label="Main navigation">
              <Link href="/projects" className="text-[13px] text-muted-foreground transition-colors hover:text-foreground">
                Product
              </Link>
              <Link href="/dashboard/projects" className="text-[13px] text-muted-foreground transition-colors hover:text-foreground">
                AI
              </Link>
              <Link href="/showcase" className="text-[13px] text-muted-foreground transition-colors hover:text-foreground">
                Showcase
              </Link>
              <Link href="/projects" className="text-[13px] text-muted-foreground transition-colors hover:text-foreground">
                {t("navigation.openProjects")}
              </Link>
            </nav>
          )}
        </div>

        {user && (
          <GlobalSearch className="hidden md:block flex-1 max-w-sm mx-6" />
        )}

        <div className="flex items-center gap-2 ml-auto">
          {user ? (
            <>
              <nav className="hidden md:flex items-center gap-5 mr-2">
                <Link href="/projects" className="text-[13px] text-muted-foreground transition-colors hover:text-foreground">
                  {t("navigation.openProjects")}
                </Link>
              </nav>
              <ThemeToggle />
              <LanguageSwitcher />
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    type="button"
                    className="h-8 w-8 rounded-full"
                    aria-label={t("navigation.accountMenu")}
                  >
                    <User className="h-4 w-4" aria-hidden="true" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem asChild>
                    <Link href="/dashboard">{t("navigation.dashboard")}</Link>
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </>
          ) : (
            <>
              <ThemeToggle />
              <LanguageSwitcher />
              <Link href="/login">
                <Button variant="outline" size="sm" className="h-8 text-[13px] rounded-[10px]">
                  {t("auth.login")}
                </Button>
              </Link>
              <Link href="/register">
                <Button size="sm" className="h-8 text-[13px] rounded-[10px]">
                  Start for free
                </Button>
              </Link>
            </>
          )}
        </div>
      </div>
    </header>
  )
}
