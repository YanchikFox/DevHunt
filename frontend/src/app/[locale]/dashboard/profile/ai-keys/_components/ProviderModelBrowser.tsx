"use client"

import { useCallback, useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { ChevronRight, Wrench, Eye, Zap, Cpu } from "lucide-react"
import { useLlmModels, type LlmProviderView, type LlmModelView } from "@/lib/api/queries/llm"
import { cn } from "@/lib/utils"

interface CapabilityFilters {
  readonly tools: boolean
  readonly vision: boolean
  readonly streaming: boolean
}

/**
 * Per-provider collapsible list of available models pulled from the local
 * model registry (synced via /llm/providers/{id}/models/sync). The browser
 * lets the user see what they unlock with each saved key — capabilities,
 * context window, prices — without leaving the settings page.
 *
 * Filters apply across all providers at once. Expansion state is per-provider.
 */
export function ProviderModelBrowser({
  providers,
}: {
  readonly providers: readonly LlmProviderView[]
}) {
  const tBy = useTranslations("ai.byok")
  const [filters, setFilters] = useState<CapabilityFilters>({
    tools: false,
    vision: false,
    streaming: false,
  })

  const toggleFilter = useCallback((key: keyof CapabilityFilters) => {
    setFilters((prev) => ({ ...prev, [key]: !prev[key] }))
  }, [])

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-mono text-[10.5px] uppercase tracking-[0.08em] text-muted-foreground">
          {tBy("filterCapabilities")}
        </span>
        <FilterChip
          active={filters.tools}
          icon={<Wrench className="size-3" aria-hidden />}
          label={tBy("capabilityTools")}
          onClick={() => toggleFilter("tools")}
        />
        <FilterChip
          active={filters.vision}
          icon={<Eye className="size-3" aria-hidden />}
          label={tBy("capabilityVision")}
          onClick={() => toggleFilter("vision")}
        />
        <FilterChip
          active={filters.streaming}
          icon={<Zap className="size-3" aria-hidden />}
          label={tBy("capabilityStreaming")}
          onClick={() => toggleFilter("streaming")}
        />
      </div>

      <div className="flex flex-col gap-2">
        {providers.map((provider) => (
          <ProviderGroup key={provider.providerId} provider={provider} filters={filters} />
        ))}
      </div>
    </div>
  )
}

function FilterChip({
  active,
  icon,
  label,
  onClick,
}: {
  readonly active: boolean
  readonly icon: React.ReactNode
  readonly label: string
  readonly onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-[10.5px] uppercase tracking-[0.04em] transition-colors",
        active
          ? "border-transparent bg-primary/12 text-primary"
          : "border-border bg-bg-subtle text-muted-foreground hover:bg-bg-hover",
      )}
    >
      {icon}
      {label}
    </button>
  )
}

function ProviderGroup({
  provider,
  filters,
}: {
  readonly provider: LlmProviderView
  readonly filters: CapabilityFilters
}) {
  const tBy = useTranslations("ai.byok")
  const [open, setOpen] = useState(false)
  const { data: rawModels = [], isLoading } = useLlmModels({ provider: provider.providerId })

  const models = useMemo(() => {
    return rawModels.filter((model) => {
      if (filters.tools && !model.supportsTools) return false
      if (filters.vision && !model.supportsVision) return false
      if (filters.streaming && !model.supportsStreaming) return false
      return true
    })
  }, [rawModels, filters])

  const toggle = useCallback(() => setOpen((prev) => !prev), [])

  return (
    <div className="overflow-hidden rounded-[8px] border border-border bg-background">
      <button
        type="button"
        onClick={toggle}
        className="flex w-full items-center gap-3 px-3 py-2.5 text-left transition-colors hover:bg-bg-hover"
        aria-expanded={open}
      >
        <ChevronRight
          className={cn("size-3.5 shrink-0 text-muted-foreground transition-transform", open && "rotate-90")}
          aria-hidden
        />
        <Cpu className="size-3.5 shrink-0 text-muted-foreground" aria-hidden />
        <span className="text-[13px] font-medium text-foreground">{provider.displayName}</span>
        <span className="font-mono text-[10.5px] uppercase tracking-[0.04em] text-muted-foreground">
          {isLoading
            ? tBy("loading")
            : tBy("modelCount", { count: models.length, total: rawModels.length })}
        </span>
      </button>

      {open && (
        <div className="border-t border-border">
          {isLoading ? (
            <div className="px-3 py-3 text-[12.5px] text-muted-foreground">{tBy("loading")}</div>
          ) : models.length === 0 ? (
            <div className="px-3 py-3 text-[12.5px] text-muted-foreground">
              {rawModels.length === 0 ? tBy("noModelsHint") : tBy("noModelsMatch")}
            </div>
          ) : (
            <ul className="divide-y divide-border">
              {models.map((model) => (
                <ModelRow key={model.id} model={model} />
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  )
}

function ModelRow({ model }: { readonly model: LlmModelView }) {
  const tBy = useTranslations("ai.byok")
  return (
    <li className="flex flex-col gap-1.5 px-3 py-2.5 sm:flex-row sm:items-center sm:gap-3">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-1.5">
          <span className="text-[13px] font-medium text-foreground">{model.displayName}</span>
          <TierBadge tier={model.tier} />
          {model.supportsTools && <CapBadge label={tBy("capabilityTools")} />}
          {model.supportsVision && <CapBadge label={tBy("capabilityVision")} />}
          {model.supportsStreaming && <CapBadge label={tBy("capabilityStreaming")} />}
        </div>
        <div className="mt-0.5 font-mono text-[10.5px] text-muted-foreground">
          {model.modelId}
          {" · "}
          {tBy("contextWindow", { size: formatTokenCount(model.contextWindow) })}
          {model.maxOutputTokens && (
            <> · {tBy("maxOutput", { size: formatTokenCount(model.maxOutputTokens) })}</>
          )}
        </div>
      </div>
      <div className="font-mono text-[10.5px] text-muted-foreground sm:text-right">
        {model.inputPricePer1M != null && model.outputPricePer1M != null ? (
          <>
            <div>{tBy("priceInput", { price: formatPrice(model.inputPricePer1M) })}</div>
            <div>{tBy("priceOutput", { price: formatPrice(model.outputPricePer1M) })}</div>
          </>
        ) : (
          <span className="opacity-75">{tBy("priceFreeOrUnknown")}</span>
        )}
      </div>
    </li>
  )
}

const KNOWN_TIERS = new Set(["cheap", "balanced", "smart"])

function TierBadge({ tier }: { readonly tier: string }) {
  const tBy = useTranslations("ai.byok")
  // The tier-to-color mapping is intentionally subtle — these badges sit
  // next to capability chips and shouldn't shout louder than them.
  const styles: Record<string, string> = {
    cheap: "bg-emerald-500/12 text-emerald-700 dark:text-emerald-400",
    balanced: "bg-amber-500/12 text-amber-700 dark:text-amber-400",
    smart: "bg-primary/12 text-primary",
  }
  const className = styles[tier] ?? "bg-bg-subtle text-muted-foreground"
  // next-intl throws on missing keys, so only translate the known set;
  // unknown tiers (introduced by /sync from a provider we haven't mapped
  // yet) render the raw value so they stay informative.
  const label = KNOWN_TIERS.has(tier) ? tBy(`tier.${tier}`) : tier
  return (
    <span className={cn("rounded-full px-1.5 py-px font-mono text-[10px] uppercase tracking-[0.04em]", className)}>
      {label}
    </span>
  )
}

function CapBadge({ label }: { readonly label: string }) {
  return (
    <span className="rounded-full border border-border px-1.5 py-px font-mono text-[10px] uppercase tracking-[0.04em] text-muted-foreground">
      {label}
    </span>
  )
}

function formatTokenCount(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(value % 1_000_000 === 0 ? 0 : 1)}M`
  if (value >= 1_000) return `${(value / 1_000).toFixed(value % 1_000 === 0 ? 0 : 1)}K`
  return value.toString()
}

function formatPrice(value: number): string {
  if (value === 0) return "$0"
  if (value < 1) return `$${value.toFixed(2)}`
  return `$${value.toFixed(2)}`
}
