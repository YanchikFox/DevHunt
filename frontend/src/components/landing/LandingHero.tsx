"use client"

import { useTranslations } from "next-intl"
import { ArrowRight, Github, Sparkles } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"


export function LandingHero() {
  const t = useTranslations("homepage")

  return (
    <section className="relative overflow-hidden pt-20 pb-16 md:pt-28 md:pb-20">
      <div className="max-w-[1200px] mx-auto px-8">
        {/* Badge */}
        <div className="flex items-center gap-2 mb-7">
          <span className="chip chip-accent inline-flex items-center gap-1.5">
            <Sparkles className="h-2.5 w-2.5" />
            v1.0 — AI team matching now live
          </span>
        </div>

        {/* Headline */}
        <h1 className="font-serif text-foreground" style={{ fontSize: "clamp(44px, 7vw, 88px)", lineHeight: 0.98, letterSpacing: "-0.03em", fontWeight: 400, margin: 0 }}>
          From idea to release,<br />
          <em style={{ color: "oklch(var(--primary))", fontStyle: "italic" }}>without leaving the room.</em>
        </h1>

        {/* Description */}
        <p className="mt-7 text-[18px] text-muted-foreground max-w-[620px] leading-relaxed">
          {t("heroDescription")}
        </p>

        {/* CTAs */}
        <div className="flex flex-wrap gap-3 mt-8">
          <Link href="/register">
            <Button size="lg" className="h-11 px-6 text-[14px] rounded-[10px] gap-2">
              Start building
              <ArrowRight className="h-3.5 w-3.5" />
            </Button>
          </Link>
          <Button variant="outline" size="lg" className="h-11 px-6 text-[14px] rounded-[10px] gap-2">
            <Github className="h-3.5 w-3.5" />
            Continue with GitHub
          </Button>
          <Link href="/projects">
            <Button variant="ghost" size="lg" className="h-11 px-6 text-[14px] rounded-[10px] text-muted-foreground hover:text-foreground">
              Enter demo →
            </Button>
          </Link>
        </div>

        {/* Stats */}
        <div className="flex flex-wrap gap-6 mt-12 text-[12px] text-muted-foreground">
          <span>◆ 14,200 builders</span>
          <span>◆ 3,400 projects shipped</span>
          <span>◆ 220 hackathons hosted</span>
        </div>

        {/* Product tile mockup */}
        <div className="mt-14 rounded-[14px] border border-border bg-card p-1.5 shadow-md overflow-hidden">
          <div
            className="placeholder-stripe rounded-[10px] flex items-center justify-center"
            style={{ aspectRatio: "16/9" }}
          >
            <div
              className="w-[94%] h-[94%] bg-background rounded-[10px] border border-border grid"
              style={{ gridTemplateColumns: "180px 1fr 1fr 1fr", gap: 10, padding: 16 }}
            >
              {/* Sidebar */}
              <div className="flex flex-col gap-1 p-2 border-r border-border">
                <div className="flex items-center gap-1.5 mb-3">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="text-primary">
                    <path d="M4 20L12 4l8 16" /><path d="M8 14h8" />
                  </svg>
                  <span className="text-[11px] font-semibold tracking-tight">DevHunt</span>
                </div>
                <div className="h-px bg-border mb-2" />
                {["Home", "Projects", "Board", "AI Plan"].map(item => (
                  <div key={item} className="text-[10px] text-muted-foreground px-1.5 py-1 rounded">{item}</div>
                ))}
              </div>
              {/* Kanban columns */}
              {[
                { name: "TO DO", tasks: ["Wire up pg_stat_statements ingest", "Write docs for self-hosted mode", "Design empty-state"] },
                { name: "IN PROGRESS", tasks: ["Build plan-diff visualisation", "Investigate regression"] },
                { name: "IN REVIEW", tasks: ["CLI flags for daemon mode"] },
              ].map(col => (
                <div key={col.name} className="flex flex-col gap-1.5">
                  <div className="caption mb-1">{col.name}</div>
                  {col.tasks.map(task => (
                    <div key={task} className="rounded-[6px] border border-border bg-card p-1.5">
                      <div className="text-[9px] font-medium text-foreground line-clamp-1">{task}</div>
                    </div>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
