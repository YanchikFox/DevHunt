"use client"

import { Sparkles, Users, LayoutDashboard, MessageSquare, Flag, Github } from "lucide-react"

const FEATURES = [
  {
    icon: Sparkles,
    title: "AI Plan",
    body: "Describe a project in a paragraph. Get a staged roadmap, tech stack recommendation, and a list of roles you need to fill.",
  },
  {
    icon: Users,
    title: "Smart matching",
    body: "The ML service pairs your open roles with developers whose GitHub activity and stated skills fit — reverse-matching too.",
  },
  {
    icon: LayoutDashboard,
    title: "Built-in Kanban",
    body: "No more leaving for Trello. Soft-deletes, task links, WIP limits, drag-reorder. Works offline.",
  },
  {
    icon: MessageSquare,
    title: "Realtime chat",
    body: "Per-project channels and DMs over SignalR. Typing indicators, threads, file drops.",
  },
  {
    icon: Flag,
    title: "Project lifecycle",
    body: "Idea → Recruiting → Active → Beta → Release → Archived. Each stage changes what shows up in the feed.",
  },
  {
    icon: Github,
    title: "GitHub sync",
    body: "Link a repo. Commits light up the activity wall. Issues sync both directions with your board.",
  },
]

export function LandingFeatures() {

  return (
    <section className="pb-24">
      <div className="max-w-[1200px] mx-auto px-8">
        <div className="caption mb-4">[What&apos;s inside]</div>
        <h2 className="font-serif text-[48px] font-normal tracking-[-0.03em] leading-none mb-12 text-foreground">
          Six surfaces. One workflow.
        </h2>

        {/* Grid with 1px border dividers */}
        <div
          className="grid border border-border rounded-[14px] overflow-hidden bg-border"
          style={{ gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1px" }}
        >
          {FEATURES.map(({ icon: Icon, title, body }) => (
            <div key={title} className="bg-card p-7 flex flex-col gap-4">
              <div className="inline-flex p-2 rounded-lg bg-primary/10 text-primary w-fit">
                <Icon className="h-4 w-4" strokeWidth={1.75} />
              </div>
              <div>
                <div className="text-[16px] font-semibold text-foreground mb-1.5">{title}</div>
                <div className="text-[13px] text-muted-foreground leading-relaxed">{body}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
