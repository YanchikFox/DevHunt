import React from "react"

/**
 * Mock for @/i18n/routing used in Storybook.
 * Replaces next-intl navigation wrappers with plain HTML/React equivalents.
 */

export function Link({
  href,
  children,
  className,
  ...props
}: {
  href: string
  children: React.ReactNode
  className?: string
  [key: string]: unknown
}) {
  return (
    <a href={typeof href === "string" ? href : "#"} className={className} {...props}>
      {children}
    </a>
  )
}

export function usePathname() {
  return "/dashboard/profile/edit"
}

export function useRouter() {
  return {
    push: (url: string) => console.warn("[Storybook mock] router.push:", url),
    replace: (url: string) => console.warn("[Storybook mock] router.replace:", url),
    back: () => console.warn("[Storybook mock] router.back"),
  }
}

export function redirect(url: string) {
  console.warn("[Storybook mock] redirect:", url)
}
