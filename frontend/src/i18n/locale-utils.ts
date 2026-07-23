/**
 * Shared locale utilities.
 *
 * All locale-aware helpers derive from `routing.locales` and
 * `routing.defaultLocale` so that adding / removing a locale
 * only requires changes in `routing.ts` + a new messages/*.json file.
 */
import { routing } from "./routing"
import { pl, enUS, type Locale as DateFnsLocale } from "date-fns/locale"

// ---------------------------------------------------------------------------
// Core types & constants (re-exported from routing for convenience)
// ---------------------------------------------------------------------------

/** Union type of all supported locale codes. */
export type AppLocale = (typeof routing.locales)[number]

/** Read-only array of supported locale codes. */
export const locales = routing.locales

/** The fallback locale when nothing else matches. */
export const defaultLocale = routing.defaultLocale

// ---------------------------------------------------------------------------
// Type guard
// ---------------------------------------------------------------------------

/** Runtime check that a string is a supported locale. */
export function isSupportedLocale(value: string): value is AppLocale {
  return (routing.locales as readonly string[]).includes(value)
}

// ---------------------------------------------------------------------------
// date-fns locale mapping
// ---------------------------------------------------------------------------

/**
 * Map from app locale codes → date-fns Locale objects.
 *
 * When adding a new locale:
 * 1. Add it to `routing.locales`.
 * 2. Import the date-fns locale and add an entry here.
 */
const DATE_FNS_LOCALES: Record<AppLocale, DateFnsLocale> = {
  pl,
  en: enUS,
}

/** Return the date-fns `Locale` for the given app locale string. */
export function getDateFnsLocale(locale: string): DateFnsLocale {
  if (isSupportedLocale(locale)) {
    return DATE_FNS_LOCALES[locale]
  }
  return DATE_FNS_LOCALES[defaultLocale]
}

// ---------------------------------------------------------------------------
// Path helpers
// ---------------------------------------------------------------------------

/**
 * Strip the locale prefix from a pathname.
 *
 * "/en/dashboard" → "/dashboard"
 * "/pl"           → "/"
 * "/about"        → "/about"  (no locale prefix)
 */
export function stripLocalePrefix(pathname: string): string {
  for (const locale of routing.locales) {
    if (pathname === `/${locale}`) return "/"
    if (pathname.startsWith(`/${locale}/`)) {
      return pathname.substring(`/${locale}`.length)
    }
  }
  return pathname
}

/**
 * Extract the locale prefix from a pathname, if any.
 *
 * "/en/dashboard" → "/en"
 * "/pl"           → "/pl"
 * "/about"        → ""
 */
export function extractLocalePrefix(pathname: string): string {
  for (const locale of routing.locales) {
    if (pathname === `/${locale}` || pathname.startsWith(`/${locale}/`)) {
      return `/${locale}`
    }
  }
  return ""
}

/**
 * Detect locale from a URL path string.
 * Returns `defaultLocale` if no known locale prefix is found.
 */
export function detectLocaleFromPath(path: string): AppLocale {
  for (const locale of routing.locales) {
    if (path === `/${locale}` || path.startsWith(`/${locale}/`)) {
      return locale
    }
  }
  return defaultLocale
}

/**
 * Detect locale from an Accept-Language header value.
 * Returns `defaultLocale` if no known locale matches.
 */
export function detectLocaleFromAcceptLanguage(header: string | null | undefined): AppLocale {
  if (!header) return defaultLocale
  const lower = header.toLowerCase()
  for (const locale of routing.locales) {
    if (lower.startsWith(locale)) return locale
  }
  return defaultLocale
}
