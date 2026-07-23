"use client"

import { useState, useCallback } from "react";
import { useTranslations } from "next-intl"
import { useSearchParams } from "next/navigation"
import { useProjectsList } from "@/lib/api/queries/projects"
import { Link } from "@/i18n/routing"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Search, Plus, LayoutGrid, List, Users, Filter } from "lucide-react"
import { Skeleton } from "@/components/ui/skeleton"
import { Header } from "@/components/layout/header"
import { Footer } from "@/components/layout/footer"
import { cn } from "@/lib/utils"

const STAGE_FILTERS = ["all", "idea", "recruiting", "active", "beta"] as const
type StageFilter = (typeof STAGE_FILTERS)[number]

const STAGE_STYLE: Record<string, { bg: string; text: string }> = {
  idea:       { bg: "bg-info/15",       text: "text-info" },
  recruiting: { bg: "bg-warning/15",    text: "text-warning" },
  active:     { bg: "bg-success/15",    text: "text-success" },
  beta:       { bg: "bg-primary/10",    text: "text-primary" },
  release:    { bg: "bg-primary/10",    text: "text-primary" },
  completed:  { bg: "bg-muted",         text: "text-muted-foreground" },
}
function getStageStyle(s: string) {
  return STAGE_STYLE[s.toLowerCase()] ?? { bg: "bg-muted", text: "text-muted-foreground" }
}

export default function OpenProjectsPage() {
  const t = useTranslations()
  const searchParams = useSearchParams()
  const urlTech = searchParams.get("tech") || undefined

  const [searchQuery, setSearchQuery] = useState("")
  const [filter, setFilter] = useState<StageFilter>("all")
  const [view, setView] = useState<"grid" | "list">("grid")

  const { data: projects, isLoading } = useProjectsList({
    status: filter === "all" ? "recruiting" : filter,
    query: searchQuery || undefined,
    tech: urlTech,
    sortBy: "createdAt",
    sortOrder: "desc",
    excludeDrafts: true,
  })

  const handleSearchQueryChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
  }, [])

  const handleFilterChange = useCallback((f: StageFilter) => {
    setFilter(f)
  }, [])

  const handleViewChange = useCallback((v: "grid" | "list") => {
    setView(v)
  }, [])

  const filteredCount = projects?.length ?? 0

  return (
    <div className="flex min-h-screen flex-col">
      <Header />
      <main className="flex-1">
        <div className="max-w-[1280px] mx-auto px-5 py-7 space-y-5">
          {/* Page header */}
          <div className="flex items-end justify-between gap-3 flex-wrap">
            <div>
              <span className="caption mb-1.5 block">[Explore · {filteredCount} results]</span>
              <h1 className="font-serif text-[36px] font-normal tracking-[-0.6px] leading-none text-foreground">
                Projects
              </h1>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
                <Filter className="h-3 w-3" />
                Filters
              </Button>
              <Link href="/login">
                <Button size="sm" className="h-8 text-[12px] gap-1.5 rounded-[8px]">
                  <Plus className="h-3 w-3" />
                  New
                </Button>
              </Link>
            </div>
          </div>

          {/* Search + filter bar */}
          <div className="flex gap-2.5 flex-wrap">
            {/* Search input */}
            <div className="relative flex-1 min-w-[280px]">
              <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-muted-foreground" />
              <Input
                placeholder={t("openProjects.searchPlaceholder")}
                value={searchQuery}
                onChange={handleSearchQueryChange}
                className="pl-8 h-9 text-[13px] rounded-[10px]"
              />
            </div>

            {/* Stage filter pills */}
            <div className="flex gap-1 bg-secondary p-[3px] rounded-[10px]">
              {STAGE_FILTERS.map((s) => (
                <button
                  key={s}
                  type="button"
                  onClick={() => handleFilterChange(s)}
                  className={cn(
                    "px-3 py-[5px] rounded-[6px] text-[12px] font-medium capitalize transition-all duration-150",
                    filter === s
                      ? "bg-card text-foreground shadow-sm"
                      : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  {s}
                </button>
              ))}
            </div>

            {/* View toggle */}
            <div className="flex gap-0.5 bg-secondary p-[3px] rounded-[10px]">
              {([["grid", LayoutGrid], ["list", List]] as const).map(([v, Icon]) => (
                <button
                  key={v}
                  type="button"
                  onClick={() => handleViewChange(v as "grid" | "list")}
                  className={cn(
                    "p-[7px] rounded-[6px] transition-all duration-150",
                    view === v
                      ? "bg-card text-foreground shadow-sm"
                      : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  <Icon className="h-3.5 w-3.5" />
                </button>
              ))}
            </div>
          </div>

          {/* Content */}
          {isLoading ? (
            <div className="grid gap-3.5 md:grid-cols-2 lg:grid-cols-3">
              {[1, 2, 3, 4, 5, 6].map((i) => (
                <div key={i} className="rounded-[14px] border border-border bg-card p-4 space-y-3">
                  <div className="flex items-center gap-3">
                    <Skeleton className="h-9 w-9 rounded-[8px]" />
                    <div className="flex-1 space-y-1.5">
                      <Skeleton className="h-4 w-3/4" />
                      <Skeleton className="h-3 w-full" />
                    </div>
                  </div>
                  <div className="flex gap-1">
                    <Skeleton className="h-5 w-16 rounded-full" />
                    <Skeleton className="h-5 w-12 rounded-full" />
                  </div>
                </div>
              ))}
            </div>
          ) : projects && projects.length > 0 ? (
            view === "grid" ? (
              <div className="grid gap-3.5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {projects.map((project) => {
                  const hue = (project.title?.charCodeAt(0) ?? 65) * 7 % 360
                  const s = getStageStyle(project.status)
                  return (
                    <Link
                      key={project.id}
                      href="/login"
                      className="group rounded-[14px] border border-border bg-card p-4 flex flex-col gap-3 hover:border-primary/30 hover:-translate-y-0.5 hover:shadow-md transition-all duration-150"
                    >
                      <div className="flex items-center gap-3">
                        <div
                          className="h-9 w-9 rounded-[8px] flex items-center justify-center text-white font-mono font-semibold text-[14px] shrink-0"
                          style={{ background: `oklch(0.72 0.12 ${hue})` }}
                        >
                          {project.title?.[0] ?? "?"}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="text-[13px] font-medium text-foreground truncate group-hover:text-primary transition-colors">
                            {project.title}
                          </p>
                          <p className="text-[11px] text-muted-foreground truncate">
                            {project.description}
                          </p>
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5 flex-wrap">
                        <span className={cn(
                          "chip text-[10px] capitalize border-transparent",
                          s.bg, s.text
                        )}>
                          {project.status}
                        </span>
                        {project.technologies?.slice(0, 2).map((tech) => (
                          <span key={tech} className="chip text-[10px]">{tech}</span>
                        ))}
                      </div>
                      <div className="flex items-center gap-3 text-[11px] text-muted-foreground pt-1 border-t border-border">
                        <span className="flex items-center gap-1">
                          <Users className="h-3 w-3" />
                          {project.teamSize ?? 0} members
                        </span>
                      </div>
                    </Link>
                  )
                })}
              </div>
            ) : (
              /* List view */
              <div className="rounded-[14px] border border-border bg-card overflow-hidden">
                {projects.map((project, i) => {
                  const hue = (project.title?.charCodeAt(0) ?? 65) * 7 % 360
                  const s = getStageStyle(project.status)
                  return (
                    <Link
                      key={project.id}
                      href="/login"
                      className={cn(
                        "grid grid-cols-[2fr_1fr_1fr_1fr_80px] gap-4 items-center px-4 py-3.5 hover:bg-muted/30 transition-colors cursor-pointer",
                        i < projects.length - 1 && "border-b border-border"
                      )}
                    >
                      <div className="flex gap-3 items-center min-w-0">
                        <div
                          className="h-8 w-8 rounded-[8px] flex items-center justify-center text-white font-mono font-semibold text-[13px] shrink-0"
                          style={{ background: `oklch(0.72 0.12 ${hue})` }}
                        >
                          {project.title?.[0] ?? "?"}
                        </div>
                        <div className="min-w-0">
                          <p className="text-[13px] font-medium truncate">{project.title}</p>
                          <p className="text-[11px] text-muted-foreground truncate">{project.description}</p>
                        </div>
                      </div>
                      <div>
                        <span className={cn("chip text-[10px] capitalize border-transparent", s.bg, s.text)}>
                          {project.status}
                        </span>
                      </div>
                      <div className="text-[12px] text-muted-foreground">
                        {project.technologies?.slice(0, 2).join(" · ")}
                      </div>
                      <div className="text-[12px] text-muted-foreground flex items-center gap-2">
                        <Users className="h-3 w-3" /> {project.teamSize ?? 0}
                      </div>
                      <div className="font-mono text-[11px] text-muted-foreground text-right">
                        {project.createdAt ? new Date(project.createdAt).toLocaleDateString("en-US", { month: "short", day: "numeric" }) : "—"}
                      </div>
                    </Link>
                  )
                })}
              </div>
            )
          ) : (
            <div className="rounded-[14px] border border-dashed border-border bg-card p-12 text-center">
              <div className="text-muted-foreground/30 mb-3">
                <Search className="h-10 w-10 mx-auto" />
              </div>
              <h3 className="text-[15px] font-semibold text-foreground mb-1">
                {t("openProjects.noProjectsFound")}
              </h3>
              <p className="text-[13px] text-muted-foreground max-w-md mx-auto">
                {searchQuery
                  ? t("openProjects.tryAdjustingSearch")
                  : t("openProjects.noProjectsAvailable")}
              </p>
            </div>
          )}
        </div>
      </main>
      <Footer />
    </div>
  )
}
