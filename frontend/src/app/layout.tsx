import "./globals.css"

import type { Metadata } from "next"
import { headers } from "next/headers"

import { inter } from "@/lib/fonts"
import {
  detectLocaleFromPath,
  detectLocaleFromAcceptLanguage,
  type AppLocale,
} from "@/i18n/locale-utils"

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000"

export const metadata: Metadata = {
  metadataBase: new URL(siteUrl),
}

async function detectRootLang(): Promise<AppLocale> {
  const requestHeaders = await headers()

  const urlHeader =
    requestHeaders.get("x-next-url") ??
    requestHeaders.get("next-url") ??
    requestHeaders.get("x-url") ??
    requestHeaders.get("referer")

  if (urlHeader) {
    try {
      const asUrl = urlHeader.startsWith("http") ? new URL(urlHeader) : null
      const path = asUrl?.pathname ?? urlHeader
      return detectLocaleFromPath(path)
    } catch {
      // ignore and fall back
    }
  }

  return detectLocaleFromAcceptLanguage(requestHeaders.get("accept-language"))
}

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const lang = await detectRootLang()

  return (
    <html lang={lang} suppressHydrationWarning className="scroll-smooth" data-scroll-behavior="smooth">
      <body className={inter.className}>{children}</body>
    </html>
  )
}
