"use client"

import { useLocale, useTranslations } from "next-intl"
import { useRouter, usePathname } from "@/i18n/routing"
import { Languages } from "lucide-react"
import { Button } from "@/components/ui/button"
import { useCallback } from "react"
import { locales, type AppLocale } from "@/i18n/locale-utils"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

/** Map locale codes → translation keys in messages.languages.* */
const LOCALE_LABEL_KEYS: Record<AppLocale, string> = {
  pl: "languages.polish",
  en: "languages.english",
}

function LocaleMenuItem({ loc, isActive, label, onSwitch }: { loc: AppLocale; isActive: boolean; label: string; onSwitch: (loc: AppLocale) => void }) {
  const handleClick = useCallback(() => onSwitch(loc), [onSwitch, loc])
  return (
    <DropdownMenuItem
      onClick={handleClick}
      className={isActive ? "bg-bg-subtle text-foreground" : "text-muted-foreground focus:bg-bg-hover focus:text-foreground"}
    >
      {label}
    </DropdownMenuItem>
  )
}

export function LanguageSwitcher() {
  const locale = useLocale()
  const router = useRouter()
  const pathname = usePathname()
  const t = useTranslations()

  const handleSwitch = useCallback(
    (target: AppLocale) => {
      router.replace(pathname, { locale: target })
    },
    [router, pathname]
  )

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" className="h-8 w-8 rounded-[8px] text-muted-foreground hover:bg-bg-hover hover:text-foreground">
          <Languages className="h-4 w-4" />
          <span className="sr-only">
            {t("common.changeLanguage") || "Change language"}
          </span>
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44 rounded-[14px] border-border bg-bg-elevated p-1 shadow-lg">
        {locales.map((loc) => (
          <LocaleMenuItem
            key={loc}
            loc={loc}
            isActive={locale === loc}
            label={t(LOCALE_LABEL_KEYS[loc])}
            onSwitch={handleSwitch}
          />
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
