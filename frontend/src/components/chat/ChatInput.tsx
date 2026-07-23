"use client"

import { useCallback, useEffect, useMemo, useState } from "react"
import { Send, Loader2, Sparkles, Paperclip, X, Reply, Pencil, KeyRound, Gauge } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { cn } from "@/lib/utils"
import { useTranslations } from "next-intl"
import { useDebouncedValue } from "@/hooks/use-debounced-value"
import {
  useLlmChatEstimate,
  useLlmModels,
  useLlmProviders,
  useUserApiKeys,
  type LlmChatEstimateRequest,
  type LlmChatEstimateResponse,
  type LlmModelView,
} from "@/lib/api/queries/llm"
import type { LlmPreferences } from "./useLlmPreferences"
import type { DisplayMessage } from "./types"

interface ChatInputProps {
  readonly value: string
  readonly onChange: (value: string) => void
  readonly onSend: () => void
  readonly mode?: "default" | "reply" | "edit"
  readonly contextMessage?: DisplayMessage | null
  readonly onCancelContext?: () => void
  readonly disabled: boolean
  readonly isLoading: boolean
  readonly isAiCommand: boolean
  readonly conversationId: string
  readonly projectId?: string | null
  readonly canUseAiTools: boolean
  readonly llmPreferences: LlmPreferences
  readonly onLlmPreferencesChange: (patch: Partial<LlmPreferences>) => void
}

export function ChatInput({
  value,
  onChange,
  onSend,
  mode = "default",
  contextMessage,
  onCancelContext,
  disabled,
  isLoading,
  isAiCommand,
  conversationId,
  projectId,
  canUseAiTools,
  llmPreferences,
  onLlmPreferencesChange,
}: ChatInputProps) {
  const t = useTranslations("chat")
  const placeholder = t("messageOrAiPlaceholder")
  const aiQuery = useMemo(() => extractAiQuery(value, isAiCommand), [value, isAiCommand])
  const debouncedAiQuery = useDebouncedValue(aiQuery, 500)
  const estimateRequest = useMemo(
    () => buildEstimateRequest({
      conversationId,
      projectId,
      query: debouncedAiQuery,
      preferences: llmPreferences,
      canUseAiTools,
    }),
    [conversationId, projectId, debouncedAiQuery, llmPreferences, canUseAiTools],
  )
  const estimate = useLlmChatEstimate(estimateRequest, Boolean(estimateRequest))
  const estimateLabel = useMemo(
    () => formatEstimateLabel(estimate.data, t),
    [estimate.data, t],
  )
  const estimateTitle = estimate.isFetching ? t("aiEstimateLoading") : estimateLabel

  const handleSubmit = useCallback((e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    onSend()
  }, [onSend])

  const handleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    onChange(e.target.value)
  }, [onChange])

  const trimmed = value.trim().length > 0
  const sendDisabled = disabled || isLoading || !trimmed

  return (
    <div className="shrink-0 border-t border-border bg-background p-3">
      {mode !== "default" && contextMessage && (
        <div className="mb-2 flex items-center gap-2 rounded-[10px] border border-border bg-bg-subtle px-3 py-2">
          {mode === "edit" ? (
            <Pencil className="h-3.5 w-3.5 shrink-0 text-primary" />
          ) : (
            <Reply className="h-3.5 w-3.5 shrink-0 text-primary" />
          )}
          <div className="min-w-0 flex-1">
            <div className="text-[11px] font-medium text-foreground">
              {mode === "edit" ? "Editing message" : `Replying to ${contextMessage.senderFullName ?? "message"}`}
            </div>
            <div className="truncate text-[11px] text-muted-foreground">{contextMessage.content}</div>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={onCancelContext}
            className="h-7 w-7 shrink-0 rounded-[7px]"
            aria-label="Cancel"
          >
            <X className="h-3.5 w-3.5" />
          </Button>
        </div>
      )}
      {isAiCommand && (
        <>
          <AiModelPicker
            preferences={llmPreferences}
            onPreferencesChange={onLlmPreferencesChange}
            canUseTools={canUseAiTools}
          />
          <AiEstimateLine
            label={estimateLabel}
            loading={estimate.isFetching}
            loadingLabel={t("aiEstimateLoading")}
          />
        </>
      )}
      <form
        onSubmit={handleSubmit}
        className={cn(
          "flex items-center gap-1.5 rounded-[10px] bg-bg-subtle p-1 transition-colors",
          "focus-within:bg-bg-hover",
          isAiCommand && "ring-1 ring-primary/40"
        )}
      >
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-[30px] w-[30px] shrink-0 rounded-[6px] text-muted-foreground hover:bg-bg-elevated hover:text-foreground"
          aria-label="Attach file"
        >
          <Paperclip className="h-[15px] w-[15px]" />
        </Button>

        <input
          value={value}
          onChange={handleChange}
          placeholder={placeholder}
          disabled={disabled || isLoading}
          className="min-w-0 flex-1 border-0 bg-transparent px-1 py-1.5 text-[13px] text-foreground outline-none placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-60"
        />

        <Button
          type="submit"
          disabled={sendDisabled}
          title={isAiCommand ? estimateTitle ?? undefined : undefined}
          className="h-[30px] shrink-0 gap-1.5 rounded-[6px] px-3 text-[12px] font-medium shadow-none"
        >
          {getButtonIcon(isLoading, isAiCommand)}
          <span>{isAiCommand ? "Ask AI" : "Send"}</span>
        </Button>
      </form>
    </div>
  )
}

function AiEstimateLine({
  label,
  loading,
  loadingLabel,
}: {
  readonly label: string | null
  readonly loading: boolean
  readonly loadingLabel: string
}) {
  if (!label && !loading) return null

  return (
    <div className="mb-2 flex items-center gap-1.5 px-1 font-mono text-[10px] text-primary/75">
      {loading && <Loader2 className="h-3 w-3 animate-spin" aria-hidden />}
      <span>{loading ? loadingLabel : label}</span>
    </div>
  )
}

function AiModelPicker({
  preferences,
  onPreferencesChange,
  canUseTools,
}: {
  readonly preferences: LlmPreferences
  readonly onPreferencesChange: (patch: Partial<LlmPreferences>) => void
  readonly canUseTools: boolean
}) {
  const t = useTranslations("chat")
  const { data: providers = [] } = useLlmProviders()
  const { data: keys = [] } = useUserApiKeys()
  const keyedProviderIds = useMemo(() => new Set(keys.map((key) => key.provider)), [keys])
  const availableProviders = useMemo(
    () => providers.filter((provider) => keyedProviderIds.has(provider.providerId)),
    [providers, keyedProviderIds],
  )
  const selectedProvider =
    preferences.provider && keyedProviderIds.has(preferences.provider)
      ? preferences.provider
      : availableProviders[0]?.providerId
  const { data: models = [] } = useLlmModels({ provider: selectedProvider, requiresStreaming: true })
  const selectedModel = resolveSelectedModel(models, preferences.modelId)

  const handleProviderChange = useCallback(
    (provider: string) => {
      onPreferencesChange({ provider, modelId: null })
    },
    [onPreferencesChange],
  )

  const handleModelChange = useCallback(
    (modelId: string) => {
      onPreferencesChange({ provider: selectedProvider, modelId })
    },
    [onPreferencesChange, selectedProvider],
  )

  const handleToolsChange = useCallback(
    (enabled: boolean) => {
      onPreferencesChange({ enableTools: enabled })
    },
    [onPreferencesChange],
  )

  if (availableProviders.length === 0) {
    return (
      <div className="mb-2 flex items-center gap-2 rounded-[10px] border border-amber-500/20 bg-amber-500/8 px-3 py-2 text-[11px] text-amber-700 dark:text-amber-300">
        <KeyRound className="h-3.5 w-3.5 shrink-0" aria-hidden />
        <span>{t("aiKeyRequired")}</span>
      </div>
    )
  }

  return (
    <>
      <div className="mb-2 grid gap-2 rounded-[10px] border border-primary/15 bg-primary/5 p-2 sm:grid-cols-[minmax(120px,0.85fr)_minmax(160px,1.4fr)_auto]">
        <Select value={selectedProvider ?? ""} onValueChange={handleProviderChange}>
          <SelectTrigger className="h-8 rounded-[7px] bg-background text-[12px]">
            <SelectValue placeholder={t("aiProvider")} />
          </SelectTrigger>
          <SelectContent>
            {availableProviders.map((provider) => (
              <SelectItem key={provider.providerId} value={provider.providerId}>
                {provider.displayName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select value={selectedModel?.modelId ?? ""} onValueChange={handleModelChange}>
          <SelectTrigger className="h-8 rounded-[7px] bg-background text-[12px]">
            <SelectValue placeholder={t("aiModel")} />
          </SelectTrigger>
          <SelectContent>
            {models.map((model) => (
              <SelectItem key={model.id} value={model.modelId}>
                {formatModelLabel(model)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <label className="flex items-center justify-between gap-2 rounded-[7px] bg-background px-2 py-1 text-[11px] text-muted-foreground sm:justify-center">
          <span>{t("aiTools")}</span>
          <Switch
            checked={preferences.enableTools && canUseTools && selectedModel?.supportsTools === true}
            disabled={!canUseTools || selectedModel?.supportsTools !== true}
            onCheckedChange={handleToolsChange}
            className="scale-75"
          />
        </label>
      </div>
      {selectedModel && <AiModelLimitLine model={selectedModel} />}
    </>
  )
}

function AiModelLimitLine({ model }: { readonly model: LlmModelView }) {
  const t = useTranslations("chat")
  const contextWindow = model.contextWindow > 0 ? formatTokenCount(model.contextWindow) : t("aiLimitUnknown")
  const maxOutput = model.maxOutputTokens ? formatTokenCount(model.maxOutputTokens) : t("aiLimitUnknown")
  const price = formatModelPrice(model)
  const priceLabel = price ? t("aiModelPrice", { price }) : t("aiPriceFreeOrUnknown")

  return (
    <div className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1 px-1 font-mono text-[10px] text-muted-foreground">
      <span className="inline-flex items-center gap-1 text-primary/75">
        <Gauge className="h-3 w-3" aria-hidden />
        {t("aiModelLimits")}
      </span>
      <span>{t("aiContextWindow", { size: contextWindow })}</span>
      <span>{t("aiMaxOutput", { size: maxOutput })}</span>
      <span>{priceLabel}</span>
    </div>
  )
}

function resolveSelectedModel(models: readonly LlmModelView[], selectedModelId?: string | null): LlmModelView | null {
  if (models.length === 0) return null
  if (!selectedModelId) return models[0] ?? null
  return models.find((model) => model.modelId === selectedModelId) ?? models[0] ?? null
}

function formatModelLabel(model: LlmModelView): string {
  const price = formatModelPrice(model)
  return price ? `${model.displayName} · ${price}` : model.displayName
}

function formatModelPrice(model: LlmModelView): string | null {
  if (typeof model.inputPricePer1M !== "number" || typeof model.outputPricePer1M !== "number") {
    return null
  }
  return `$${model.inputPricePer1M}/$${model.outputPricePer1M}`
}

function extractAiQuery(value: string, isAiCommand: boolean): string {
  if (!isAiCommand) return ""
  return value.trim().slice(4).trim()
}

function buildEstimateRequest({
  conversationId,
  projectId,
  query,
  preferences,
  canUseAiTools,
}: {
  readonly conversationId: string
  readonly projectId?: string | null
  readonly query: string
  readonly preferences: LlmPreferences
  readonly canUseAiTools: boolean
}): LlmChatEstimateRequest | null {
  if (!query || !conversationId || conversationId === "new") return null

  return {
    conversationId,
    message: query,
    provider: preferences.provider ?? null,
    modelId: preferences.modelId ?? null,
    enableTools: preferences.enableTools === true && canUseAiTools === true && Boolean(projectId),
    useHistory: null,
  }
}

function formatEstimateLabel(
  estimate: LlmChatEstimateResponse | undefined,
  t: (key: string, values?: Record<string, string | number>) => string
): string | null {
  if (!estimate) return null
  const tokens = formatTokenCount(estimate.promptTokens)
  const cost = formatCostRange(estimate.minCostUsd, estimate.maxCostUsd) ?? t("aiEstimateFreeOrUnknown")
  return cost ? `~${tokens} tokens / ${cost}` : `~${tokens} tokens`
}

function formatTokenCount(tokens: number): string {
  if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(tokens % 1_000_000 === 0 ? 0 : 1)}M`
  if (tokens < 1000) return `${tokens}`
  return `${(tokens / 1000).toFixed(tokens < 10_000 ? 1 : 0)}k`
}

function formatCostRange(minCost?: number | null, maxCost?: number | null): string | null {
  if (typeof minCost !== "number" && typeof maxCost !== "number") return null
  if (typeof minCost === "number" && typeof maxCost === "number") {
    return `$${formatUsd(minCost)}-$${formatUsd(maxCost)}`
  }
  const cost = minCost ?? maxCost
  return typeof cost === "number" ? `$${formatUsd(cost)}` : null
}

function formatUsd(value: number): string {
  return value.toFixed(value < 0.01 ? 6 : 4)
}

function getButtonIcon(isLoading: boolean, isAiCommand: boolean) {
  if (isLoading) return <Loader2 className="h-3 w-3 animate-spin" />
  if (isAiCommand) return <Sparkles className="h-3 w-3" />
  return <Send className="h-3 w-3" />
}
