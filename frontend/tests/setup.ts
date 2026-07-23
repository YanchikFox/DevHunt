import "@testing-library/jest-dom"
import { afterEach } from "vitest"
import { cleanup } from "@testing-library/react"

import React from "react"
import { vi } from "vitest"

vi.mock("next-intl", () => {
  return {
    useTranslations: () => (key: string) => key,
  }
})

vi.mock("@/hooks/use-current-user", () => {
  return {
    useCurrentUser: () => ({ user: null }),
  }
})

vi.mock("@/i18n/routing", () => {
  return {
    Link: ({ href, children, ...props }: { href: string | object; children: React.ReactNode; [key: string]: unknown }) =>
      React.createElement(
        "a",
        { href: typeof href === "string" ? href : String(href), ...props },
        children
      ),
    redirect: vi.fn(),
    usePathname: () => "/",
    useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn() }),
  }
})

vi.mock("@/components/theme-toggle", () => {
  return {
    ThemeToggle: () => React.createElement("button", { type: "button", "aria-label": "Toggle theme" }),
  }
})

vi.mock("@/components/language-switcher", () => {
  return {
    LanguageSwitcher: () =>
      React.createElement("button", { type: "button", "aria-label": "Switch language" }),
  }
})

afterEach(() => {
  cleanup()
})
