"use client"

import { useTranslations } from "next-intl"
import { usePathname, Link } from "@/i18n/routing"
import { cn } from "@/lib/utils"
import { User, Shield, Lock, KeyRound, type LucideIcon } from "lucide-react";

interface TabItem {
  href: string
  labelKey: string
  fallback: string
  icon: LucideIcon
}

const TABS: TabItem[] = [
  { href: "/dashboard/profile/edit", labelKey: "navigation.editProfile", fallback: "Edit Profile", icon: User },
  { href: "/dashboard/profile/security", labelKey: "security.title", fallback: "Security", icon: Shield },
  { href: "/dashboard/profile/privacy", labelKey: "privacy.privacySettings", fallback: "Privacy", icon: Lock },
  { href: "/dashboard/profile/ai-keys", labelKey: "ai.byok.navLabel", fallback: "AI keys", icon: KeyRound },
]

export function ProfileSettingsNav() {
  const t = useTranslations()
  const pathname = usePathname()

  return (
    <nav className="flex items-center gap-1 p-1 bg-muted/50 rounded-xl w-fit">
      {TABS.map((tab) => {
        const isActive = pathname === tab.href || pathname.startsWith(tab.href + "/")
        const Icon = tab.icon
        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={cn(
              "flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all duration-200",
              isActive
                ? "bg-background text-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground hover:bg-background/50"
            )}
          >
            <Icon className="h-4 w-4" />
            <span className="hidden sm:inline">{t(tab.labelKey) || tab.fallback}</span>
          </Link>
        )
      })}
    </nav>
  )
}
