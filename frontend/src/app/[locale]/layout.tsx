import { NextIntlClientProvider } from "next-intl"
import { getMessages } from "next-intl/server"
import { notFound } from "next/navigation"
import type { Metadata } from "next"
import { routing } from "@/i18n/routing"
import { Providers } from "@/components/providers"
import { ErrorBoundary } from "@/components/ErrorBoundary"
import { auth } from "@/auth"

const siteUrl = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000"

export const metadata: Metadata = {
  title: {
    default: "DevHunt",
    template: "%s | DevHunt",
  },
  description:
    "DevHunt helps engineering teams discover talent, manage collaborative projects, and ship products faster.",
  metadataBase: new URL(siteUrl),
}

export function generateStaticParams() {
  return routing.locales.map((locale) => ({ locale }))
}

export default async function LocaleLayout({
  children,
  params,
}: {
  children: React.ReactNode
  params: Promise<{ locale: string }>
}) {
  // In Next.js 16, params are async
  const { locale } = await params

  // Ensure that the incoming `locale` is valid
  if (!routing.locales.includes(locale as "pl" | "en")) {
    notFound()
  }

  // Providing all messages to the client side
  const messages = await getMessages()

  let session = null
  try {
    session = await auth()
  } catch (err) {
    console.error("[Layout] Failed to load session:", err)
  }

  return (
    <Providers session={session}>
      <NextIntlClientProvider messages={messages} locale={locale} timeZone="Europe/Warsaw">
        <ErrorBoundary>
          {children}
        </ErrorBoundary>
      </NextIntlClientProvider>
    </Providers>
  )
}
