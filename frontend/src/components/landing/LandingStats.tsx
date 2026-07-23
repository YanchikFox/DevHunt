"use client"

import { useTranslations } from "next-intl"

export function LandingStats() {
  const t = useTranslations("homepage")

  const stats = [
    { value: "14,200+", label: t("statsDevelopers"), context: t("statsDevelopersContext") },
    { value: "3,400+", label: t("statsActiveProjects"), context: t("statsActiveProjectsContext") },
    { value: "220+", label: t("statsCompanies"), context: t("statsCompaniesContext") },
  ]

  return (
    <section className="pb-24">
      <div className="max-w-[1200px] mx-auto px-8">
        <div className="rounded-[14px] border border-border bg-muted/30 grid sm:grid-cols-3 divide-y sm:divide-y-0 sm:divide-x divide-border">
          {stats.map(({ value, label, context }) => (
            <div key={label} className="px-8 py-8 text-center">
              <div className="font-serif text-[44px] font-normal tracking-[-0.03em] text-primary leading-none mb-2">
                {value}
              </div>
              <div className="text-[14px] font-semibold text-foreground mb-1">{label}</div>
              <div className="text-[12px] text-muted-foreground">{context}</div>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
