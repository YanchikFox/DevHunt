"use client"

import { Link } from "@/i18n/routing"
import { useTranslations } from "next-intl"

export function Footer() {
  const t = useTranslations()

  return (
    <footer className="border-t border-border/60">
      <div className="max-w-[1200px] mx-auto px-8 py-10 flex flex-col sm:flex-row items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-5 text-[12px] text-muted-foreground">
          <div className="flex items-center gap-2">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="text-primary">
              <path d="M4 20L12 4l8 16" /><path d="M8 14h8" />
            </svg>
            <span className="font-semibold text-foreground text-[13px]">DevHunt</span>
          </div>
          <span>© 2026 DevHunt</span>
        </div>
        <div className="flex items-center gap-5 text-[12px] text-muted-foreground">
          <Link href="/privacy" className="hover:text-foreground transition-colors">Privacy</Link>
          <Link href="/terms" className="hover:text-foreground transition-colors">Terms</Link>
          <Link href="/support" className="hover:text-foreground transition-colors">{t("footer.support")}</Link>
          <Link href="/changelog" className="hover:text-foreground transition-colors">Changelog</Link>
        </div>
      </div>
    </footer>
  )
}
