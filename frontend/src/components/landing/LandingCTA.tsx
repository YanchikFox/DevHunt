"use client"

import { useTranslations } from "next-intl"
import { ArrowRight } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"

export function LandingCTA() {
  const t = useTranslations("homepage")

  return (
    <section className="pb-24">
      <div className="max-w-[1200px] mx-auto px-8">
        <div className="rounded-[14px] border border-border bg-muted/40 px-10 py-16 text-center">
          <div className="caption mb-5">[Ready to ship?]</div>
          <h2 className="font-serif text-[48px] font-normal tracking-[-0.03em] leading-none text-foreground mb-4">
            {t("ctaTitle")}
          </h2>
          <p className="text-[15px] text-muted-foreground max-w-[520px] mx-auto leading-relaxed mb-8">
            {t("ctaDescription")}
          </p>
          <div className="flex flex-wrap gap-3 justify-center">
            <Link href="/register">
              <Button size="lg" className="h-11 px-7 text-[14px] rounded-[10px] gap-2">
                {t("ctaCreateProfile")}
                <ArrowRight className="h-3.5 w-3.5" />
              </Button>
            </Link>
            <Link href="/projects">
              <Button variant="outline" size="lg" className="h-11 px-7 text-[14px] rounded-[10px]">
                {t("ctaViewProjects")}
              </Button>
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}
