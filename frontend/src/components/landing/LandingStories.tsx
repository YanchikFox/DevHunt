"use client"

import { useTranslations } from "next-intl"

const TESTIMONIALS = [
  {
    quote: "Shipped my first side project in 3 weeks. The AI plan actually matched me with a Rust engineer who knew exactly what we needed.",
    name: "Mira C.",
    role: "ML Engineer",
  },
  {
    quote: "Replaced Trello, Slack, and half of Notion. Everything in one place, and the GitHub sync means no more manual status updates.",
    name: "Tomás V.",
    role: "Backend · Rust",
  },
  {
    quote: "Found two co-founders for my research tool here. DevHunt matched our skills better than any LinkedIn search I've ever done.",
    name: "Priya S.",
    role: "Product Designer",
  },
]

export function LandingStories() {
  const t = useTranslations("homepage")

  return (
    <section className="pb-24">
      <div className="max-w-[1200px] mx-auto px-8">
        <div className="caption mb-4">[{t("storiesTag")}]</div>
        <h2 className="font-serif text-[48px] font-normal tracking-[-0.03em] leading-none mb-12 text-foreground">
          {t("storiesTitle")}
        </h2>
        <div className="grid md:grid-cols-3 gap-4">
          {TESTIMONIALS.map(({ quote, name, role }) => (
            <div key={name} className="rounded-[14px] border border-border bg-card p-7 flex flex-col gap-6">
              <p className="text-[14px] text-foreground leading-relaxed flex-1">&ldquo;{quote}&rdquo;</p>
              <div className="border-t border-border pt-5 flex items-center gap-3">
                <div
                  className="h-8 w-8 rounded-full flex items-center justify-center font-mono font-semibold text-[12px] text-white shrink-0"
                  style={{ background: `oklch(0.72 0.12 ${name.charCodeAt(0) * 7 % 360})` }}
                >
                  {name[0]}
                </div>
                <div>
                  <div className="text-[13px] font-medium text-foreground">{name}</div>
                  <div className="text-[11px] text-muted-foreground">{role}</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
