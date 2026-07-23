"use client"

import { type KeyboardEvent, useCallback, useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { ChevronDown, MessageSquarePlus, Plus, Sparkles, X } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { useToast } from "@/hooks/use-toast"
import { useQueryClient } from "@tanstack/react-query"
import { Link, useRouter } from "@/i18n/routing"
import { apiClient } from "@/lib/api/client"
import { cn } from "@/lib/utils"
import type { QueryClient } from "@tanstack/react-query"
import {
  useGenerateAiPlanDraft,
  useRefineAiPlanDraft,
  type AiPlanDraftResponse,
} from "@/lib/api/queries/ai-plan-drafts"
import { useApplyAiPlan, useGenerateAiPlan } from "@/lib/api/queries/ai-plans"
import { useCreateProject } from "@/lib/api/queries/projects"
import { useUserApiKeys } from "@/lib/api/queries/llm"
import type { AiPlanDraft, AiPlanPhase, AiPlanTask } from "@/lib/api/ai-types"
import { AiAssistantPanel } from "./AiAssistantPanel"
import { AiMatchedCollaboratorsCard } from "./AiMatchedCollaboratorsCard"

const DEFAULT_PROMPT =
  "A real-time collaborative whiteboard focused on system-design interviews, with voice presence and architecture templates."

function mergeRoleLists(
  teamRoles: readonly string[],
  roleHints: readonly string[],
): string[] | undefined {
  const s = new Set<string>()
  for (const r of teamRoles) {
    const x = r.trim()
    if (x) s.add(x)
  }
  for (const r of roleHints) {
    const x = r.trim()
    if (x) s.add(x)
  }
  return s.size > 0 ? [...s] : undefined
}

/** Deduplicate by case-insensitive key; preserve first-seen ordering across lists. */
function mergeInOrder(...lists: ReadonlyArray<readonly string[]>): string[] {
  const seen = new Set<string>()
  const out: string[] = []
  for (const list of lists) {
    for (const raw of list) {
      const t = raw.trim()
      if (!t) continue
      const k = t.toLowerCase()
      if (seen.has(k)) continue
      seen.add(k)
      out.push(t)
    }
  }
  return out
}

const TAG_TO_OPEN_ROLE: Readonly<Record<string, string>> = {
  frontend: "Frontend",
  backend: "Backend",
  design: "Design / UX",
  devops: "DevOps",
  qa: "QA / testing",
  sysadmin: "System admin",
  mobile: "Mobile",
  data: "Data / ML",
  security: "Security",
  product: "Product",
  marketing: "Marketing",
  content: "Content",
  api: "API",
  ui: "UI",
  ux: "UX",
  ml: "Machine learning",
  dev: "Engineering",
}

function tagToOpenRoleLabel(tag: string): string {
  const t = tag.trim().toLowerCase()
  if (!t) return ""
  if (TAG_TO_OPEN_ROLE[t]) return TAG_TO_OPEN_ROLE[t]!
  return t.length <= 1 ? t.toUpperCase() : t[0]!.toUpperCase() + t.slice(1)
}

/** Suggested open roles from plan task `tags` (as produced by the ML service). */
function extractOpenRolesFromPlan(d: AiPlanDraft): string[] {
  const out: string[] = []
  const seen = new Set<string>()
  for (const ph of d.phases) {
    for (const task of ph.tasks) {
      const taskTags = task.tags
      if (!taskTags || taskTags.length === 0) continue
      for (const raw of taskTags) {
        const label = tagToOpenRoleLabel(raw)
        if (!label) continue
        const k = label.toLowerCase()
        if (seen.has(k)) continue
        seen.add(k)
        out.push(label)
      }
    }
  }
  return out
}

async function createProjectFromAiPlan(
  draft: AiPlanDraftResponse,
  teamRoles: readonly string[],
  customTags: readonly string[],
  combinedRoles: string[] | undefined,
  createProjectMutateAsync: ReturnType<typeof useCreateProject>["mutateAsync"],
  generatePlanMutateAsync: ReturnType<typeof useGenerateAiPlan>["mutateAsync"],
  applyPlanMutateAsync: ReturnType<typeof useApplyAiPlan>["mutateAsync"],
  queryClient: QueryClient,
  router: ReturnType<typeof useRouter>,
  toast: ReturnType<typeof useToast>["toast"],
  t: ReturnType<typeof useTranslations>,
): Promise<void> {
  const derivedTitle = deriveTitle(draft.idea)
  const shortDescription = deriveShortDescription(draft.idea)
  const techArray = splitTechStack(draft.techStack)

  const project = await createProjectMutateAsync({
    title: derivedTitle, description: shortDescription, technologies: techArray,
    status: "draft", visibility: "public",
    openRoles: teamRoles.length > 0
      ? [...teamRoles].map((role) => ({ role, totalNeeded: 1, hoursPerWeek: null, equityOptional: true }))
      : undefined,
  })

  try {
    const planResponse = await generatePlanMutateAsync({
      projectId: project.id, idea: truncateForPlanStorage(draft.idea, 1900),
      techStack: truncateForPlanStorage(draft.techStack, 480),
      customTags: customTags.length > 0 ? [...customTags] : undefined, customRoles: combinedRoles,
    })
    await applyPlanMutateAsync({ projectId: project.id, planId: planResponse.id })
    try {
      await apiClient.post(`/projects/${project.id}/docs/generate-passport`)
      await queryClient.invalidateQueries({ queryKey: ["projects", project.id, "documents"] })
    } catch {
      toast({ title: t("docsPassportWarnTitle"), description: t("docsPassportWarnDesc"), variant: "default" })
    }
  } catch (innerErr) {
    toast({ title: t("errorCreate"), description: innerErr instanceof Error ? innerErr.message : undefined, variant: "destructive" })
  }

  router.push(`/dashboard/projects/${project.id}`)
}

export function AiPlanView() {
  const t = useTranslations("ai.plan")
  const { toast } = useToast()
  const router = useRouter()
  const queryClient = useQueryClient()

  const [idea, setIdea] = useState(DEFAULT_PROMPT)
  const [techStack, setTechStack] = useState("")
  const [customTags, setCustomTags] = useState<string[]>([])
  const [roleHints, setRoleHints] = useState<string[]>([])
  const [tagDraft, setTagDraft] = useState("")
  const [roleHintDraft, setRoleHintDraft] = useState("")
  const [teamRoles, setTeamRoles] = useState<string[]>([])
  const [showAssistant, setShowAssistant] = useState(false)
  const [draft, setDraft] = useState<AiPlanDraftResponse | null>(null)
  const [advancedOpen, setAdvancedOpen] = useState(false)

  const generateMutation = useGenerateAiPlanDraft()
  const refineMutation = useRefineAiPlanDraft()
  const createProject = useCreateProject()
  const generatePlan = useGenerateAiPlan()
  const applyPlan = useApplyAiPlan()

  const isGenerating = generateMutation.isPending
  const isRefining = refineMutation.isPending
  const isMaterialising =
    createProject.isPending || generatePlan.isPending || applyPlan.isPending

  const combinedRoles = useMemo(
    () => mergeRoleLists(teamRoles, roleHints),
    [teamRoles, roleHints],
  )

  const generate = useCallback(async () => {
    if (!idea.trim() || isGenerating) return
    try {
      const result = await generateMutation.mutateAsync({
        idea,
        techStack: techStack.trim() || undefined,
        customTags: customTags.length > 0 ? customTags : undefined,
        customRoles: combinedRoles,
      })
      setDraft(result)
      setTeamRoles((prev) =>
        mergeInOrder(
          roleHints,
          prev,
          extractOpenRolesFromPlan(result.draft),
        ),
      )
    } catch (err) {
      toast({
        title: t("errorGenerate"),
        description: err instanceof Error ? err.message : undefined,
        variant: "destructive",
      })
    }
  }, [idea, isGenerating, generateMutation, techStack, customTags, combinedRoles, roleHints, toast, t])

  const onRefinePlan = useCallback(
    async (message: string) => {
      if (!draft) throw new Error("No plan")
      const result = await refineMutation.mutateAsync({
        idea: draft.idea,
        techStack: draft.techStack,
        currentPlan: draft.draft,
        instructions: message,
        customTags: customTags.length > 0 ? customTags : undefined,
        customRoles: combinedRoles,
      })
      setDraft(result)
      setTeamRoles((prev) => mergeInOrder(prev, extractOpenRolesFromPlan(result.draft)))
    },
    [draft, refineMutation, customTags, combinedRoles],
  )

  const createFromPlan = useCallback(async () => {
    if (!draft || isMaterialising) return
    try {
      await createProjectFromAiPlan(
        draft, teamRoles, customTags, combinedRoles,
        createProject.mutateAsync, generatePlan.mutateAsync, applyPlan.mutateAsync,
        queryClient, router, toast, t,
      )
    } catch (err) {
      toast({ title: t("errorCreate"), description: err instanceof Error ? err.message : undefined, variant: "destructive" })
    }
  }, [draft, isMaterialising, createProject.mutateAsync, generatePlan.mutateAsync, applyPlan.mutateAsync, customTags, combinedRoles, teamRoles, router, queryClient, toast, t])

  const chatContext = useMemo(() => buildAssistantContext(draft, idea, techStack, teamRoles, roleHints), [draft, idea, techStack, teamRoles, roleHints])

  return (
    <div className="mx-auto w-full max-w-[1100px] p-5 lg:p-7">
      <Header
        showAssistant={showAssistant}
        onToggleAssistant={() => setShowAssistant((v) => !v)}
      />
      <AiKeysCta />

      <div
        className={cn(
          "grid gap-5 lg:gap-6",
          showAssistant ? "lg:grid-cols-[minmax(0,1fr)_360px]" : "grid-cols-1",
        )}
      >
        <div className="flex min-w-0 flex-col gap-6">
          <PromptCard
            idea={idea}
            onIdeaChange={setIdea}
            techStack={techStack}
            onTechStackChange={setTechStack}
            tagDraft={tagDraft}
            onTagDraftChange={setTagDraft}
            tags={customTags}
            onAddTag={(tag) => setCustomTags((prev) => [...new Set([...prev, tag])])}
            onRemoveTag={(tag) => setCustomTags((prev) => prev.filter((x) => x !== tag))}
            roleHintDraft={roleHintDraft}
            onRoleHintDraftChange={setRoleHintDraft}
            roleHints={roleHints}
            onAddRoleHint={(role) => setRoleHints((prev) => [...new Set([...prev, role])])}
            onRemoveRoleHint={(role) => setRoleHints((prev) => prev.filter((r) => r !== role))}
            onGenerate={generate}
            isGenerating={isGenerating}
            modelLabel={draft?.draft.model?.trim() || null}
            hasDraft={!!draft}
            advancedOpen={advancedOpen}
            onToggleAdvanced={() => setAdvancedOpen((o) => !o)}
          />

          {!draft && <EmptyState />}

          {draft && (
            <div className="flex flex-col gap-6">
              <StackCard techStack={draft.techStack} />
              <RoadmapCard phases={draft.draft.phases} />
              <TeamRolesBlock
                teamRoles={teamRoles}
                onAdd={(r) => setTeamRoles((prev) => [...new Set([...prev, r])])}
                onRemove={(r) => setTeamRoles((prev) => prev.filter((x) => x !== r))}
              />
              <AiMatchedCollaboratorsCard techStack={draft.techStack} teamRoles={teamRoles} />

              <div className="flex flex-wrap items-center justify-end gap-2 border-t border-border/60 pt-5">
                <span className="mr-auto text-[11px] text-muted-foreground">
                  {t("ephemeralHint")}
                </span>
                <Button onClick={createFromPlan} disabled={isMaterialising || !draft}>
                  <Plus className="size-4" aria-hidden />
                  {t("createProject")}
                </Button>
              </div>
            </div>
          )}
        </div>

        {showAssistant && (
          <AiAssistantPanel
            plan={draft}
            chatContext={chatContext}
            onRefinePlan={onRefinePlan}
            isPlanMutating={isRefining}
          />
        )}
      </div>
    </div>
  )
}

/* ────────────────────── Header ────────────────────── */

function Header({
  showAssistant,
  onToggleAssistant,
}: {
  showAssistant: boolean
  onToggleAssistant: () => void
}) {
  const t = useTranslations("ai.plan")
  return (
    <div className="mb-7 flex flex-wrap items-end justify-between gap-3">
      <div>
        <div className="caption mb-1.5">[{t("eyebrow")}]</div>
        <h1
          className="font-serif text-[36px] leading-[1.08] tracking-[-0.6px] text-foreground md:text-[40px]"
          style={{ fontWeight: 400 }}
        >
          {t("heroTitle")}
        </h1>
        <p className="mt-1.5 max-w-[620px] text-sm text-muted-foreground">
          {t("heroSubtitle")}
        </p>
      </div>
      <Button
        variant="outline"
        onClick={onToggleAssistant}
        className="shrink-0"
        type="button"
      >
        <MessageSquarePlus className="size-4" aria-hidden />
        {showAssistant ? t("hideAssistant") : t("openAssistant")}
      </Button>
    </div>
  )
}

/* ────────────────────── Prompt card ────────────────────── */

interface PromptCardProps {
  idea: string
  onIdeaChange: (v: string) => void
  techStack: string
  onTechStackChange: (v: string) => void
  tagDraft: string
  onTagDraftChange: (v: string) => void
  tags: string[]
  onAddTag: (tag: string) => void
  onRemoveTag: (tag: string) => void
  roleHintDraft: string
  onRoleHintDraftChange: (v: string) => void
  roleHints: string[]
  onAddRoleHint: (role: string) => void
  onRemoveRoleHint: (role: string) => void
  onGenerate: () => void
  isGenerating: boolean
  modelLabel: string | null
  hasDraft: boolean
  advancedOpen: boolean
  onToggleAdvanced: () => void
}

function PromptCard(props: PromptCardProps) {
  const t = useTranslations("ai.plan")
  const charCount = props.idea.length

  const commitTag = useCallback(() => {
    const v = props.tagDraft.trim()
    if (!v) return
    props.onAddTag(v)
    props.onTagDraftChange("")
  }, [props])

  const commitRoleHint = useCallback(() => {
    const v = props.roleHintDraft.trim()
    if (!v) return
    props.onAddRoleHint(v)
    props.onRoleHintDraftChange("")
  }, [props])

  const onTagKey = useCallback(
    (e: KeyboardEvent<HTMLInputElement>) => {
      if (e.key === "Enter" || e.key === ",") {
        e.preventDefault()
        commitTag()
      }
    },
    [commitTag],
  )

  const onRoleHintKey = useCallback(
    (e: KeyboardEvent<HTMLInputElement>) => {
      if (e.key === "Enter" || e.key === ",") {
        e.preventDefault()
        commitRoleHint()
      }
    },
    [commitRoleHint],
  )

  return (
    <div className="ai-border">
      <div className="ai-glow rounded-[10px] p-5">
        <div className="caption mb-2 text-primary">[{t("promptLabel")}]</div>
        <Textarea
          value={props.idea}
          onChange={(e) => props.onIdeaChange(e.target.value)}
          placeholder={t("promptPlaceholder")}
          className="min-h-[88px] resize-none border-0 bg-transparent p-0 text-base leading-snug focus-visible:ring-0"
        />

        <button
          type="button"
          onClick={props.onToggleAdvanced}
          className="mt-3 flex w-full items-center justify-between rounded-md border border-border/60 bg-bg-subtle/50 px-3 py-2 text-left text-[12.5px] text-foreground transition-colors hover:bg-bg-subtle"
        >
          <span>
            <span className="font-medium">{t("advanced")}</span>{" "}
            <span className="text-muted-foreground">— {t("advancedHint")}</span>
          </span>
          <ChevronDown
            className={cn("size-4 shrink-0 text-muted-foreground transition-transform", props.advancedOpen && "rotate-180")}
            aria-hidden
          />
        </button>

        {props.advancedOpen && (
          <div className="mt-3 grid gap-3 border-t border-border/60 pt-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <label className="caption text-[10px]">[{t("techStackLabel")}]</label>
              <Input
                value={props.techStack}
                onChange={(e) => props.onTechStackChange(e.target.value)}
                placeholder={t("techStackPlaceholder")}
                className="h-8 text-[12.5px]"
              />
            </div>
            <ChipField
              label={t("customTagsLabel")}
              items={props.tags}
              draft={props.tagDraft}
              onDraftChange={props.onTagDraftChange}
              onKeyDown={onTagKey}
              onRemove={props.onRemoveTag}
              placeholder={t("customTagsPlaceholder")}
            />
            <ChipField
              label={t("customRolesLabel")}
              items={props.roleHints}
              draft={props.roleHintDraft}
              onDraftChange={props.onRoleHintDraftChange}
              onKeyDown={onRoleHintKey}
              onRemove={props.onRemoveRoleHint}
              placeholder={t("customRolesPlaceholder")}
              className="sm:col-span-2"
            />
          </div>
        )}

        <div className="mt-4 flex items-center justify-between border-t border-border/60 pt-3">
          <div className="font-mono text-[11px] text-muted-foreground">
            {props.modelLabel
              ? t("charsWithModel", { chars: charCount, model: props.modelLabel })
              : t("charsOnly", { chars: charCount })}
          </div>
          <Button
            type="button"
            onClick={props.onGenerate}
            disabled={props.isGenerating || charCount === 0}
          >
            {props.isGenerating ? (
              <>
                <span className="mr-1.5 inline-block size-1.5 animate-pulse rounded-full bg-current" />
                {t("generating")}
              </>
            ) : (
              <>
                {props.hasDraft ? t("regenerate") : t("generate")}
                <Sparkles className="size-3.5" aria-hidden />
              </>
            )}
          </Button>
        </div>
      </div>
    </div>
  )
}

interface ChipFieldProps {
  label: string
  items: readonly string[]
  draft: string
  onDraftChange: (v: string) => void
  onKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
  onRemove: (item: string) => void
  placeholder: string
  className?: string
}

function ChipField({
  label,
  items,
  draft,
  onDraftChange,
  onKeyDown,
  onRemove,
  placeholder,
  className,
}: ChipFieldProps) {
  return (
    <div className={cn("flex flex-col gap-1.5", className)}>
      <label className="caption text-[10px]">[{label}]</label>
      <div className="flex min-h-8 flex-wrap items-center gap-1.5 rounded-md border border-border bg-background px-2 py-1">
        {items.map((item) => (
          <span
            key={item}
            className="inline-flex items-center gap-1 rounded-full bg-primary/12 px-2 py-0.5 font-mono text-[11px] text-primary"
          >
            {item}
            <button
              type="button"
              onClick={() => onRemove(item)}
              className="text-primary/60 hover:text-primary"
              aria-label={`Remove ${item}`}
            >
              <X className="size-3" aria-hidden />
            </button>
          </span>
        ))}
        <input
          value={draft}
          onChange={(e) => onDraftChange(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder={items.length === 0 ? placeholder : ""}
          className="min-w-[80px] flex-1 bg-transparent px-1 text-[12.5px] outline-none placeholder:text-muted-foreground"
        />
      </div>
    </div>
  )
}

/* ────────────────────── Team roles (always visible) ────────────────────── */

function TeamRolesBlock({
  teamRoles,
  onAdd,
  onRemove,
}: {
  teamRoles: readonly string[]
  onAdd: (role: string) => void
  onRemove: (role: string) => void
}) {
  const t = useTranslations("ai.plan")
  const [draft, setDraft] = useState("")

  const commit = useCallback(() => {
    const v = draft.trim()
    if (!v) return
    onAdd(v)
    setDraft("")
  }, [draft, onAdd])

  return (
    <section className="fade-in">
      <div className="caption mb-1">[{t("sectionRolesLong")}]</div>
      <p className="mb-2.5 text-[12px] text-muted-foreground">{t("rolesToFillFromPlan")}</p>
      <div className="mb-2.5 flex flex-wrap items-center gap-2">
        <Input
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault()
              commit()
            }
          }}
          placeholder={t("customRolesPlaceholder")}
          className="h-8 min-w-[200px] max-w-sm flex-1 text-[12.5px]"
        />
        <Button size="sm" variant="outline" type="button" onClick={commit} disabled={!draft.trim()}>
          <Plus className="size-3.5" aria-hidden />
          {t("addRole")}
        </Button>
      </div>
      {teamRoles.length === 0 ? (
        <div className="rounded-lg border border-dashed border-border/80 bg-bg-subtle/50 px-4 py-5 text-center text-[12px] text-muted-foreground">
          {t("noRolesYet")}
        </div>
      ) : (
        <div className="grid gap-2.5 [grid-template-columns:repeat(auto-fit,minmax(240px,1fr))]">
          {teamRoles.map((role) => (
            <div
              key={role}
              className="flex items-center justify-between rounded-lg border border-border bg-card px-3.5 py-3"
            >
              <div>
                <div className="text-[13px] font-medium text-foreground">{role}</div>
                <div className="mt-0.5 font-mono text-[10px] text-muted-foreground">open</div>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="text-muted-foreground"
                onClick={() => onRemove(role)}
              >
                <X className="size-3.5" aria-hidden />
              </Button>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

/* ────────────────────── Empty state ────────────────────── */

function EmptyState() {
  const t = useTranslations("ai.plan")
  return (
    <div className="flex flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-border/80 bg-bg-subtle/60 py-16 text-center">
      <Sparkles className="size-8 text-muted-foreground/70" aria-hidden />
      <h2 className="text-sm font-semibold text-foreground">{t("emptyStateTitle")}</h2>
      <p className="max-w-[340px] text-xs text-muted-foreground">
        {t("emptyStateDescription")}
      </p>
    </div>
  )
}

/* ────────────────────── Stack card ────────────────────── */

function StackCard({ techStack }: { techStack: string }) {
  const t = useTranslations("ai.plan")
  const chunks = useMemo(() => splitTechStackGrouped(techStack), [techStack])

  return (
    <section className="fade-in">
      <div className="caption mb-2.5">[{t("sectionStack")}]</div>
      <div className="grid gap-2 [grid-template-columns:repeat(auto-fit,minmax(180px,1fr))]">
        {chunks.map(({ title, value }) => (
          <div
            key={title + value}
            className="rounded-lg border border-border bg-card px-3 py-2.5"
          >
            <div className="caption mb-1 text-[10px]">[{title}]</div>
            <div className="text-[13px] font-medium text-foreground">{value}</div>
          </div>
        ))}
      </div>
    </section>
  )
}

/* ────────────────────── Roadmap (expandable phases) ────────────────────── */

function RoadmapCard({ phases }: { phases: AiPlanPhase[] }) {
  const t = useTranslations("ai.plan")
  const [open, setOpen] = useState<Set<string>>(() => new Set())

  const title =
    phases.length === 1
      ? t("sectionRoadmapSingle")
      : t("sectionRoadmap", { count: phases.length })

  const toggle = useCallback((key: string) => {
    setOpen((prev) => {
      const n = new Set(prev)
      if (n.has(key)) n.delete(key)
      else n.add(key)
      return n
    })
  }, [])

  return (
    <section className="fade-in">
      <div className="caption mb-2.5">[{title}]</div>
      <div className="overflow-hidden rounded-lg border border-border bg-card">
        {phases.map((phase, i) => {
          const key = phase.id || `p-${i}`
          const isOpen = open.has(key)
          return (
            <div key={key} className={cn(i < phases.length - 1 && "border-b border-border/70")}>
              <div className="grid gap-2 px-4 py-3.5 md:grid-cols-[48px_minmax(0,1fr)_auto] md:items-start">
                <span className="row-span-1 font-mono text-[11px] text-primary md:pt-0.5">
                  {String(i + 1).padStart(2, "0")}
                </span>
                <div className="min-w-0">
                  <div className="truncate text-[13px] font-medium text-foreground">
                    {phase.name || `Phase ${i + 1}`}
                  </div>
                  {phase.description ? (
                    <p className="mt-0.5 text-[12px] text-muted-foreground line-clamp-2 md:line-clamp-none">
                      {phase.description}
                    </p>
                  ) : null}
                </div>
                <div className="flex items-center justify-end gap-1 md:pt-0.5">
                  <span className="font-mono text-[11px] text-muted-foreground">
                    {phase.tasks.length === 1
                      ? t("phaseTasksSingle")
                      : t("phaseTasks", { count: phase.tasks.length })}
                  </span>
                  <button
                    type="button"
                    onClick={() => toggle(key)}
                    className="inline-flex size-8 shrink-0 items-center justify-center rounded-md border border-transparent text-muted-foreground transition-colors hover:bg-bg-hover hover:text-foreground"
                    aria-expanded={isOpen}
                    aria-label={isOpen ? t("closePhaseTasks") : t("openPhaseTasks")}
                  >
                    <ChevronDown
                      className={cn("size-4 transition-transform", isOpen && "rotate-180")}
                      aria-hidden
                    />
                  </button>
                </div>
              </div>
              {isOpen && (
                <div className="border-t border-border/50 bg-bg-subtle/40 px-4 py-3 md:pl-[4.5rem]">
                  <PhaseTaskList
                    goals={phase.goals}
                    tasks={phase.tasks}
                    i18n={t}
                    emptyLabel={t("roadmapNoTasksInPhase")}
                  />
                </div>
              )}
            </div>
          )
        })}
      </div>
    </section>
  )
}

function PhaseTaskList({
  goals,
  tasks,
  i18n,
  emptyLabel,
}: {
  goals: readonly string[]
  tasks: readonly AiPlanTask[]
  i18n: (key: string) => string
  emptyLabel: string
}) {
  return (
    <div className="space-y-3">
      {goals.length > 0 && (
        <div>
          <div className="mb-1 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
            {i18n("phaseGoals")}
          </div>
          <ul className="list-inside list-disc text-[12px] text-foreground/90">
            {goals.map((g) => (
              <li key={g}>{g}</li>
            ))}
          </ul>
        </div>
      )}
      {tasks.length === 0 ? (
        <p className="text-[12px] text-muted-foreground">{emptyLabel}</p>
      ) : (
        <ul className="space-y-2">
          {tasks.map((task) => (
            <li key={task.id} className="rounded-md border border-border/60 bg-card px-3 py-2">
              <div className="text-[12.5px] font-medium text-foreground">{task.title}</div>
              {task.description ? (
                <p className="mt-0.5 text-[11.5px] text-muted-foreground leading-snug">
                  {task.description}
                </p>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/* ────────────────────── Helpers ────────────────────── */

/** Strip replacement chars and C0 controls; keep readable text for titles/descriptions. */
function sanitizeReadableText(s: string): string {
  return s
    .replace(/\uFFFD/g, "")
    .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, "")
    .replace(/\s+/g, " ")
    .trim()
}

/** Remove emoji / pictographs that clutter project titles (Unicode ranges, not exhaustive). */
function stripTitleEmojis(s: string): string {
  return s.replace(/[\u{1F300}-\u{1FAFF}\u2600-\u27BF\u231A-\u231B\u23E9-\u23FA]/gu, "").trim()
}

function deriveTitle(idea: string): string {
  let line = sanitizeReadableText(idea).split(/\n/, 1)[0] ?? ""
  line = stripTitleEmojis(line)
  line = line.split(/[.!?]/, 1)[0] ?? line
  const words = line
    .split(/\s+/)
    .filter(Boolean)
    .filter((w) => w.length <= 48)
    .slice(0, 6)
  let title = words.join(" ").trim()
  if (title.length > 72) title = title.slice(0, 72).trim()
  if (title.length >= 3) return title[0]!.toUpperCase() + title.slice(1)
  return "AI Project"
}

/** Short project description for overview (not the full pitch). */
function deriveShortDescription(idea: string, maxChars = 560): string {
  const t = sanitizeReadableText(idea)
  if (!t) return ""
  const block = t.split(/\n\n+/, 1)[0] ?? t
  if (block.length <= maxChars) return block
  return `${block.slice(0, maxChars - 1).trim()}…`
}

function truncateForPlanStorage(s: string, maxLen: number): string {
  const t = sanitizeReadableText(s)
  if (t.length <= maxLen) return t
  return `${t.slice(0, maxLen - 1)}…`
}

function splitTechStack(techStack: string): string[] {
  return techStack
    .split(/[+,·/|;\n]/)
    .map((s) => s.trim())
    .filter((s) => s.length > 0 && s.length < 40)
    .slice(0, 12)
}

function splitTechStackGrouped(
  techStack: string,
): ReadonlyArray<{ title: string; value: string }> {
  const parts = splitTechStack(techStack)
  if (parts.length === 0) return [{ title: "Stack", value: techStack }]

  const pairs = parts
    .map((p) => {
      const match = p.match(/^([^:—–-]+)[:—–-]\s*(.+)$/)
      if (match) return { title: match[1].trim(), value: match[2].trim() }
      return null
    })
    .filter((x): x is { title: string; value: string } => x !== null)

  if (pairs.length >= Math.ceil(parts.length / 2)) return pairs

  const bucketNames = ["Frontend", "Backend", "Data", "Infra", "Tools", "Extras"]
  return parts.map((part, i) => ({
    title: bucketNames[i] ?? `Stack ${i + 1}`,
    value: part,
  }))
}

function buildAssistantContext(
  draft: AiPlanDraftResponse | null,
  idea: string,
  techStack: string,
  teamRoles: readonly string[],
  roleHints: readonly string[],
): string {
  const base =
    `User is in the DevHunt AI Plan workspace (pre-project, no project id yet).\n` +
    `Idea: ${idea.slice(0, 800)}\n`
  const roles = mergeRoleLists([...teamRoles], [...roleHints])
  const roleLine = roles?.length
    ? `Roles to fill / hints: ${roles.join(", ")}\n`
    : ""

  if (!draft) {
    return (
      base +
      (techStack ? `Preferred stack hint: ${techStack}\n` : "") +
      roleLine +
      `No generated plan yet — help brainstorm, scope, and tradeoffs.`
    )
  }
  return (
    base +
    (techStack ? `Preferred stack hint: ${techStack}\n` : "") +
    roleLine +
    `Current tech stack in plan: ${draft.techStack}\n` +
    `Plan version: ${draft.planVersion}\n` +
    (draft.draft.model ? `Model used for the plan: ${draft.draft.model}\n` : "") +
    `Full plan (for Q&A; do not invent tasks not listed):\n${summarisePlanDetailed(draft.draft)}`
  )
}

function summarisePlanDetailed(d: AiPlanDraft): string {
  return d.phases
    .map((p, i) => {
      const taskBlock =
        p.tasks.length === 0
          ? "  (no tasks listed)"
          : p.tasks
              .map(
                (task) =>
                  `  - ${task.title}${task.description ? ` — ${task.description.slice(0, 200)}` : ""}`,
              )
              .join("\n")
      const goals = p.goals.length ? p.goals.map((g) => `  * ${g}`).join("\n") : ""
      return [
        `### Phase ${i + 1}: ${p.name || "Untitled"}`,
        p.description,
        goals && `Goals:\n${goals}`,
        "Tasks:\n" + taskBlock,
      ]
        .filter(Boolean)
        .join("\n")
    })
    .join("\n\n")
    .slice(0, 12_000)
}

function AiKeysCta() {
  const tBy = useTranslations("ai.byok")
  const { data: keys = [], isLoading } = useUserApiKeys()
  if (isLoading) return null

  const hasKeys = keys.length > 0
  return (
    <section
      className={cn(
        "mb-6 flex flex-col gap-3 rounded-[10px] border px-4 py-3 sm:flex-row sm:items-center sm:justify-between",
        hasKeys
          ? "border-border bg-card"
          : "border-primary/40 bg-primary/[0.06]",
      )}
    >
      <div className="min-w-0">
        <div className="font-mono text-[10.5px] uppercase tracking-[0.08em] text-primary">
          [{tBy("eyebrow")}]
        </div>
        <p className="mt-0.5 text-[13px] text-foreground">
          {hasKeys
            ? tBy("ctaConfigured", { count: keys.length })
            : tBy("ctaUnconfigured")}
        </p>
      </div>
      <Link
        href="/dashboard/profile/ai-keys"
        className="inline-flex h-9 shrink-0 items-center justify-center rounded-[7px] border border-border bg-bg-elev px-3 text-[12.5px] font-medium transition-colors hover:bg-bg-hover"
      >
        {hasKeys ? tBy("ctaManage") : tBy("ctaSetup")}
      </Link>
    </section>
  )
}
