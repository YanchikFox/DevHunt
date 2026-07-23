"use client"

import { Moon, Sun } from "lucide-react"
import { useTheme } from "next-themes"
import { useEffect, useState, useCallback } from "react"
import { useTranslations } from "next-intl"
import { Button } from "@/components/ui/button"

export function ThemeToggle() {
  const { theme, setTheme } = useTheme()
  const [mounted, setMounted] = useState(false)
  const t = useTranslations()

  useEffect(() => {
    setMounted(true)
  }, [])

  const label = t("common.toggleTheme")

  const toggleTheme = useCallback(() => {
    setTheme(theme === "dark" ? "light" : "dark")
  }, [theme, setTheme])

  const buttonClassName = "h-8 w-8 rounded-[8px] text-muted-foreground hover:bg-bg-hover hover:text-foreground"

  if (!mounted) {
    return (
      <Button variant="ghost" size="icon" aria-label={label} className={buttonClassName}>
        <Sun className="h-4 w-4" aria-hidden="true" />
        <span className="sr-only">{label}</span>
      </Button>
    )
  }

  return (
    <Button variant="ghost" size="icon" aria-label={label} onClick={toggleTheme} className={buttonClassName}>
      {theme === "dark" ? (
        <Sun className="h-4 w-4" aria-hidden="true" />
      ) : (
        <Moon className="h-4 w-4" aria-hidden="true" />
      )}
      <span className="sr-only">{label}</span>
    </Button>
  )
}
