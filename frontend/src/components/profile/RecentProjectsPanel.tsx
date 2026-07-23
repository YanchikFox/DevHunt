"use client"

import { Link } from "@/i18n/routing"
import { ArrowRight, FolderOpen, Loader2 } from "lucide-react"
import { useTranslations } from "next-intl"
import { useProjectsList } from "@/lib/api/queries/projects"

export function RecentProjectsPanel() {
  const t = useTranslations("recentProjects")
  const { data: projects, isLoading } = useProjectsList({ myProjects: true, sortBy: "updatedAt", sortOrder: "desc" })

  const items = projects ?? []

  return (
    <section className="rounded-[14px] border border-border bg-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <span className="caption">[{t("title")}]</span>
        <Link href="/dashboard/projects" className="text-muted-foreground transition-colors hover:text-primary">
          <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </div>
      <div className="space-y-2 p-4">
        {isLoading ? (
          <div className="flex items-center justify-center p-8 text-muted-foreground">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-[12px] border border-dashed border-border bg-bg-subtle p-8 text-center">
            <FolderOpen className="h-8 w-8 text-muted-foreground/40 mb-2" />
            <p className="text-sm text-muted-foreground">{t("empty")}</p>
          </div>
        ) : (
          items.map((project) => (
            <Link
              key={project.id}
              href={`/dashboard/projects/${project.id}`}
              className="group flex items-center gap-3 rounded-[10px] border border-border px-3 py-2.5 transition-all hover:border-primary/20 hover:bg-bg-subtle"
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[9px] bg-primary/10 font-mono text-[13px] font-semibold text-primary">
                {project.title?.[0] ?? "?"}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-semibold text-foreground truncate group-hover:text-primary transition-colors">
                  {project.title || t("untitled")}
                </p>
                {project.description && (
                  <p className="text-xs text-muted-foreground mt-0.5 line-clamp-1">
                    {project.description}
                  </p>
                )}
              </div>
              <span className="chip shrink-0 text-[10px] capitalize">
                {project.status}
              </span>
              <ArrowRight className="h-4 w-4 text-muted-foreground shrink-0 group-hover:text-primary transition-colors" />
            </Link>
          ))
        )}
      </div>
    </section>
  )
}
