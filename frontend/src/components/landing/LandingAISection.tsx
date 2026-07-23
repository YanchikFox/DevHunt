"use client"

import { Sparkles, ArrowRight } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Link } from "@/i18n/routing"

const WEEKS = [
  { week: "Week 1-2", tasks: "CRDT sync layer · React canvas scaffold" },
  { week: "Week 3-4", tasks: "Voice + cursor presence · auth" },
  { week: "Week 5-6", tasks: "Template library · billing" },
]

export function LandingAISection() {

  return (
    <section className="pb-24">
      <div className="max-w-[1200px] mx-auto px-8">
        <div className="ai-border rounded-[14px]">
          <div className="ai-glow rounded-[14px] p-10 md:p-12">
            <div className="grid md:grid-cols-2 gap-12 items-center">
              {/* Copy */}
              <div>
                <span className="chip chip-accent inline-flex items-center gap-1.5 mb-5">
                  <Sparkles className="h-2.5 w-2.5" />
                  AI that actually moves the cursor
                </span>
                <h3 className="font-serif text-[40px] font-normal tracking-[-0.03em] leading-[1.05] mb-4 text-foreground">
                  A cofounder that never sleeps.
                </h3>
                <p className="text-[15px] text-muted-foreground leading-relaxed mb-6">
                  DevHunt&apos;s AI Plan turns &ldquo;I want to build X&rdquo; into a realistic 6-week roadmap,
                  a tech stack your team will actually enjoy, and a shortlist of collaborators
                  whose work matches the task.
                </p>
                <Link href="/register">
                  <Button className="gap-2 rounded-[10px]">
                    Try AI Plan
                    <ArrowRight className="h-3.5 w-3.5" />
                  </Button>
                </Link>
              </div>

              {/* Code preview card */}
              <div className="rounded-[14px] border border-border bg-card p-5 shadow-md">
                <div className="font-mono text-[11px] text-muted-foreground mb-3">
                  /ai/plan/generate
                </div>
                <div className="rounded-[8px] bg-muted/50 border border-border p-3 text-[13px] text-foreground mb-4">
                  &ldquo;Real-time collab whiteboard focused on system-design interviews.&rdquo;
                </div>
                <div className="flex flex-col gap-2">
                  {WEEKS.map(({ week, tasks }) => (
                    <div
                      key={week}
                      className="flex gap-3 items-start rounded-[8px] border border-border bg-background px-3 py-2.5"
                    >
                      <span className="font-mono text-[11px] text-primary shrink-0 w-14 pt-0.5">{week}</span>
                      <span className="text-[12px] text-foreground">{tasks}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
