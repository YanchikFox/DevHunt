"use client"

import { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslations } from "next-intl"
import { CheckCircle2, KeyRound, RefreshCw, Trash2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { useToast } from "@/hooks/use-toast"
import { ProfileSettingsNav } from "@/components/profile/ProfileSettingsNav"
import { ProviderModelBrowser } from "@/app/[locale]/dashboard/profile/ai-keys/_components/ProviderModelBrowser"
import {
  useDeleteUserApiKey,
  useLlmProviders,
  useSaveUserApiKey,
  useSyncLlmModels,
  useUserApiKeys,
  type LlmProviderView,
  type UserApiKeyView,
} from "@/lib/api/queries/llm"
import { cn } from "@/lib/utils"

export default function AiKeysSettingsPage() {
  const tBy = useTranslations("ai.byok")
  const { toast } = useToast()
  const { data: providers = [], isLoading: providersLoading } = useLlmProviders()
  const { data: keys = [], isLoading: keysLoading } = useUserApiKeys()
  const saveKey = useSaveUserApiKey()
  const deleteKey = useDeleteUserApiKey()
  const syncModels = useSyncLlmModels()

  const [provider, setProvider] = useState("")
  const [label, setLabel] = useState("")
  const [apiKey, setApiKey] = useState("")

  // Default-pick the first provider once they load. Don't override the user's
  // choice on subsequent re-renders — they may have already picked.
  useEffect(() => {
    if (provider || providers.length === 0) return
    setProvider(providers[0]?.providerId ?? "")
  }, [provider, providers])

  const keyedProviders = useMemo(
    () => providers.filter((item) => keys.some((key) => key.provider === item.providerId)),
    [providers, keys],
  )

  const isBusy = providersLoading || keysLoading
  const canSave = Boolean(provider && apiKey.trim()) && !saveKey.isPending

  const handleSave = useCallback(async () => {
    if (!canSave) return
    try {
      await saveKey.mutateAsync({
        provider,
        apiKey: apiKey.trim(),
        label: label.trim() || null,
      })
      setApiKey("")
      setLabel("")
      toast({ title: tBy("savedTitle"), description: tBy("savedDesc") })
    } catch (error) {
      toast({
        title: tBy("saveFailedTitle"),
        description: error instanceof Error ? error.message : tBy("saveFailedDesc"),
        variant: "destructive",
      })
    }
  }, [canSave, provider, apiKey, label, saveKey, toast, tBy])

  const handleSync = useCallback(
    async (providerId: string) => {
      try {
        const result = await syncModels.mutateAsync(providerId)
        toast({
          title: tBy("syncDoneTitle"),
          description: tBy("syncDoneDesc", {
            count: result.providerModelCount,
            updated: result.updated + result.created,
          }),
        })
      } catch (error) {
        toast({
          title: tBy("syncFailedTitle"),
          description: error instanceof Error ? error.message : tBy("syncFailedDesc"),
          variant: "destructive",
        })
      }
    },
    [syncModels, toast, tBy],
  )

  const handleDelete = useCallback(
    (keyId: string) => {
      deleteKey.mutate(keyId, {
        onSuccess: () => toast({ title: tBy("deletedTitle"), description: tBy("deletedDesc") }),
      })
    },
    [deleteKey, toast, tBy],
  )

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{tBy("pageTitle")}</h1>
          <p className="text-sm text-muted-foreground">{tBy("pageSubtitle")}</p>
        </div>
        <ProfileSettingsNav />
      </div>

      <SectionCard
        eyebrow={tBy("eyebrow")}
        title={tBy("addSectionTitle")}
        description={tBy("description")}
      >
        <div className="space-y-3">
          <div className="grid gap-2 sm:grid-cols-[minmax(0,0.55fr)_minmax(0,1fr)]">
            <FormField label={tBy("providerLabel")} hint={tBy("providerHint")}>
              <Select value={provider} onValueChange={setProvider} disabled={isBusy}>
                <SelectTrigger className="h-9 rounded-[8px] text-[13px]">
                  <SelectValue placeholder={tBy("providerPlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {providers.map((item) => (
                    <SelectItem key={item.providerId} value={item.providerId}>
                      {item.displayName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FormField>
            <FormField label={tBy("labelLabel")} hint={tBy("labelHint")}>
              <Input
                value={label}
                onChange={(event) => setLabel(event.target.value)}
                placeholder={tBy("labelPlaceholder")}
                className="h-9 text-[13px]"
                maxLength={64}
              />
            </FormField>
          </div>

          <FormField label={tBy("keyLabel")} hint={tBy("keyHint")}>
            <div className="flex flex-col gap-2 sm:flex-row">
              <Input
                value={apiKey}
                onChange={(event) => setApiKey(event.target.value)}
                placeholder={tBy("keyPlaceholder")}
                type="password"
                autoComplete="off"
                className="h-9 flex-1 font-mono text-[12.5px]"
                maxLength={512}
              />
              <Button type="button" onClick={handleSave} disabled={!canSave} className="h-9 shrink-0">
                <KeyRound className="size-3.5" aria-hidden />
                {saveKey.isPending ? tBy("saving") : tBy("save")}
              </Button>
            </div>
          </FormField>

          <p className="font-mono text-[10.5px] uppercase tracking-[0.08em] text-muted-foreground">
            {tBy("validationNote")}
          </p>
        </div>
      </SectionCard>

      <SectionCard
        eyebrow={tBy("savedKeysEyebrow")}
        title={tBy("savedKeys")}
        description={tBy("savedCount", { count: keys.length })}
      >
        {keys.length === 0 ? (
          <div className="rounded-[8px] border border-dashed border-border px-3 py-6 text-center text-[12.5px] text-muted-foreground">
            {tBy("empty")}
          </div>
        ) : (
          <div className="flex flex-col gap-2">
            {keys.map((key) => (
              <ApiKeyRow
                key={key.id}
                apiKey={key}
                provider={providers.find((item) => item.providerId === key.provider)}
                isDeleting={deleteKey.isPending}
                isSyncing={syncModels.isPending}
                onDelete={handleDelete}
                onSync={handleSync}
                lastValidatedLabel={tBy("lastValidatedAt")}
                lastUsedLabel={tBy("lastUsedAt")}
                neverLabel={tBy("never")}
                deleteLabel={tBy("delete")}
                syncLabel={tBy("sync")}
              />
            ))}
          </div>
        )}
      </SectionCard>

      {keyedProviders.length > 0 && (
        <SectionCard
          eyebrow={tBy("modelsEyebrow")}
          title={tBy("modelsTitle")}
          description={tBy("modelsDescription")}
        >
          <ProviderModelBrowser providers={keyedProviders} />
        </SectionCard>
      )}
    </div>
  )
}

// ── small building blocks ────────────────────────────────────────

function SectionCard({
  eyebrow,
  title,
  description,
  children,
}: {
  readonly eyebrow: string
  readonly title: string
  readonly description: string
  readonly children: React.ReactNode
}) {
  return (
    <section className="rounded-[12px] border border-border bg-card">
      <div className="border-b border-border px-5 py-4">
        <div className="font-mono text-[10.5px] uppercase tracking-[0.08em] text-primary">
          [{eyebrow}]
        </div>
        <h2 className="mt-1 text-[15px] font-semibold text-foreground">{title}</h2>
        <p className="mt-1 max-w-[68ch] text-[12.5px] leading-relaxed text-muted-foreground">
          {description}
        </p>
      </div>
      <div className="p-5">{children}</div>
    </section>
  )
}

function FormField({
  label,
  hint,
  children,
}: {
  readonly label: string
  readonly hint?: string
  readonly children: React.ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="text-[12.5px] font-medium text-foreground">{label}</label>
      {children}
      {hint && (
        <span className="font-mono text-[10.5px] leading-relaxed text-muted-foreground">{hint}</span>
      )}
    </div>
  )
}

function ApiKeyRow({
  apiKey,
  provider,
  isDeleting,
  isSyncing,
  onDelete,
  onSync,
  lastValidatedLabel,
  lastUsedLabel,
  neverLabel,
  deleteLabel,
  syncLabel,
}: {
  readonly apiKey: UserApiKeyView
  readonly provider?: LlmProviderView
  readonly isDeleting: boolean
  readonly isSyncing: boolean
  readonly onDelete: (keyId: string) => void
  readonly onSync: (providerId: string) => void
  readonly lastValidatedLabel: string
  readonly lastUsedLabel: string
  readonly neverLabel: string
  readonly deleteLabel: string
  readonly syncLabel: string
}) {
  const handleDelete = useCallback(() => onDelete(apiKey.id), [apiKey.id, onDelete])
  const handleSync = useCallback(() => onSync(apiKey.provider), [apiKey.provider, onSync])

  return (
    <div className="flex items-center gap-3 rounded-[8px] border border-border bg-background px-3 py-2.5">
      <CheckCircle2 className="size-4 shrink-0 text-emerald-500" aria-hidden />
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2 text-[13px] font-medium text-foreground">
          <span className="truncate">{provider?.displayName ?? apiKey.provider}</span>
          {apiKey.label && <span className="text-muted-foreground">· {apiKey.label}</span>}
          <span className="rounded-full bg-bg-subtle px-1.5 py-px font-mono text-[10px] text-muted-foreground">
            {apiKey.keyHint}
          </span>
        </div>
        <div className="mt-0.5 font-mono text-[10.5px] text-muted-foreground">
          {lastValidatedLabel}: {formatRelative(apiKey.lastValidatedAt) ?? neverLabel}
          {" · "}
          {lastUsedLabel}: {formatRelative(apiKey.lastUsedAt) ?? neverLabel}
        </div>
      </div>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={handleSync}
        disabled={isSyncing}
        className="h-8 w-8 rounded-[7px]"
        aria-label={syncLabel}
        title={syncLabel}
      >
        <RefreshCw className={cn("size-3.5", isSyncing && "animate-spin")} aria-hidden />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        onClick={handleDelete}
        disabled={isDeleting}
        className="h-8 w-8 rounded-[7px] text-destructive hover:bg-destructive/10 hover:text-destructive"
        aria-label={deleteLabel}
        title={deleteLabel}
      >
        <Trash2 className="size-3.5" aria-hidden />
      </Button>
    </div>
  )
}

// "2h ago" / "5d ago" — kept simple to avoid pulling in date-fns just for this.
// Returns null when the timestamp is missing so callers can render "never".
function formatRelative(iso?: string | null): string | null {
  if (!iso) return null
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return null
  const diffMs = Date.now() - date.getTime()
  if (diffMs < 60_000) return "just now"
  const minutes = Math.floor(diffMs / 60_000)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`
  const months = Math.floor(days / 30)
  if (months < 12) return `${months}mo ago`
  return `${Math.floor(months / 12)}y ago`
}
