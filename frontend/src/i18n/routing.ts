import { defineRouting } from "next-intl/routing"
import { createNavigation } from "next-intl/navigation"

/**
 * Routing configuration for internationalization (i18n).
 * Defines supported locales and the default locale.
 */
export const routing = defineRouting({
  /** A list of all locales that are supported */
  locales: ["pl", "en"],

  /** Used when no locale matches */
  defaultLocale: "pl",
})

/**
 * Lightweight wrappers around Next.js' navigation APIs
 * that will consider the routing configuration.
 *
 * Use these instead of `next/link` or `next/navigation` to ensure
 * locale prefixes are handled correctly.
 */
export const { Link, redirect, usePathname, useRouter } = createNavigation(routing)
