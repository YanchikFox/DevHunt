"use client"

import { useMemo } from "react"
import { useTranslations } from "next-intl"
import { Link } from "@/i18n/routing"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { useSearchUsers } from "@/lib/api/queries/teams"

type SearchUser = {
  id: string
  name: string
  avatar?: string
  skills: string[]
}

function splitTechForSearch(techStack: string): string[] {
  return techStack
    .split(/[+,·/|;\n]/)
    .map((s) => s.trim())
    .filter((s) => s.length > 0 && s.length < 50)
}

function buildWantedSkillSet(techStack: string, teamRoles: readonly string[]): Set<string> {
  const s = new Set<string>()
  const add = (x: string) => {
    const t = x.trim().toLowerCase()
    if (t.length > 0 && t.length < 80) s.add(t)
  }
  for (const p of splitTechForSearch(techStack)) add(p)
  for (const r of teamRoles) add(r)
  return s
}

/** Heuristic overlap between profile skills and plan stack / roles (0–100). */
function matchPercent(userSkills: readonly string[] | undefined, wanted: Set<string>): number {
  if (wanted.size === 0) return 0
  const norm = (x: string) => x.toLowerCase().trim()
  const u = (userSkills ?? []).map(norm).filter(Boolean)
  if (u.length === 0) return 0
  let hits = 0
  for (const w of wanted) {
    if (u.some((us) => us === w || us.includes(w) || w.includes(us))) hits += 1
  }
  return Math.min(100, Math.round((hits / wanted.size) * 100))
}

export function AiMatchedCollaboratorsCard({
  techStack,
  teamRoles,
}: {
  techStack: string
  teamRoles: readonly string[]
}) {
  const t = useTranslations("ai.plan")
  const skillsParam = useMemo(() => {
    const skills = [...new Set([...splitTechForSearch(techStack), ...teamRoles.map((r) => r.trim())])].filter(
      Boolean,
    )
    return skills.slice(0, 12)
  }, [techStack, teamRoles])

  const wanted = useMemo(() => buildWantedSkillSet(techStack, teamRoles), [techStack, teamRoles])

  const { data: rawUsers, isLoading, isError } = useSearchUsers(
    skillsParam.length > 0 ? { skills: skillsParam } : undefined,
    { enabled: skillsParam.length > 0 },
  )

  const users = useMemo(() => {
    const list = (rawUsers ?? []) as SearchUser[]
    return list
      .map((u) => ({ ...u, pct: matchPercent(u.skills, wanted) }))
      .sort((a, b) => b.pct - a.pct)
      .slice(0, 8) as Array<SearchUser & { pct: number }>
  }, [rawUsers, wanted])

  return (
    <section className="fade-in">
      <div className="caption mb-2.5">[{t("sectionMatches")}]</div>
      <p className="mb-3 text-[12px] text-muted-foreground">{t("matchesSuggestedLead")}</p>

      {skillsParam.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border/80 bg-bg-subtle/50 px-4 py-5 text-center text-[12px] text-muted-foreground">
          {t("matchesNeedSkills")}
        </div>
      ) : isLoading ? (
        <div className="rounded-lg border border-border/60 bg-bg-subtle/40 px-4 py-8 text-center text-[12px] text-muted-foreground">
          {t("matchesLoading")}
        </div>
      ) : isError ? (
        <div className="rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-4 text-[12px] text-muted-foreground">
          {t("matchesError")}
        </div>
      ) : users.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border/80 bg-bg-subtle/50 px-4 py-5 text-center text-[12px] text-muted-foreground">
          {t("matchesNoneFound")}
        </div>
      ) : (
        <ul className="space-y-2">
          {users.map((u) => (
            <li
              key={u.id}
              className="flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2.5"
            >
              <Avatar className="size-9 shrink-0">
                {u.avatar ? <AvatarImage src={u.avatar} alt="" /> : null}
                <AvatarFallback className="text-[11px]">
                  {(u.name || "?").slice(0, 2).toUpperCase()}
                </AvatarFallback>
              </Avatar>
              <div className="min-w-0 flex-1">
                <div className="truncate text-[13px] font-medium text-foreground">{u.name}</div>
                {u.skills?.length ? (
                  <p className="truncate text-[10px] text-muted-foreground">{u.skills.slice(0, 5).join(" · ")}</p>
                ) : null}
              </div>
              <div className="shrink-0 text-right">
                <div className="font-mono text-[12px] font-semibold text-primary">{u.pct}%</div>
                <div className="text-[9px] text-muted-foreground">{t("matchesFitLabel")}</div>
              </div>
              <Button variant="outline" size="sm" className="shrink-0" asChild>
                <Link href={`/dashboard/profile/${u.id}`}>{t("matchesViewProfile")}</Link>
              </Button>
            </li>
          ))}
        </ul>
      )}

      <p className="mt-3 text-[10.5px] text-muted-foreground">{t("matchesPostProjectHint")}</p>
    </section>
  )
}
