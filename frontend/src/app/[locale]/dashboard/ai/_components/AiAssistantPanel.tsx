"use client"

import {
  type FormEvent,
  type KeyboardEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react"
import { useTranslations } from "next-intl"
import { Send, Sparkles } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"
import type { AiPlanDraftResponse } from "@/lib/api/queries/ai-plan-drafts"

/**
 * AI Plan side panel: with a plan, you can either **refine the plan** via the
 * same `draft/refine` API (chat-as-edit) or **ask questions only** (proxy chat)
 * using the full plan as context. Pre-project: no `projectId`.
 */
interface Message {
  readonly id: string
  readonly role: "user" | "assistant"
  readonly content: string
  readonly pending?: boolean
}

const MAX_HISTORY = 12
const API_ENDPOINT = "/api/proxy-ml/ai/chat"

function newId(): string {
  return `m-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`
}

export interface AiAssistantPanelProps {
  readonly plan: AiPlanDraftResponse | null
  readonly chatContext: string
  readonly onRefinePlan: (message: string) => Promise<void>
  /** When true, disables sending while a generate/refine mutation runs in the parent. */
  readonly isPlanMutating?: boolean
  readonly className?: string
}

export function AiAssistantPanel({
  plan,
  chatContext,
  onRefinePlan,
  isPlanMutating = false,
  className,
}: AiAssistantPanelProps) {
  const t = useTranslations("ai.plan")
  const [updatePlanByChat, setUpdatePlanByChat] = useState(true)
  const [messages, setMessages] = useState<Message[]>(() => [
    { id: newId(), role: "assistant", content: t("assistantWelcome") },
  ])
  const [draft, setDraft] = useState("")
  const [isSending, setIsSending] = useState(false)
  const scrollRef = useRef<HTMLDivElement | null>(null)
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" })
  }, [messages])

  useEffect(() => () => abortRef.current?.abort(), [])

  const history = useMemo(
    () =>
      messages
        .filter((m) => !m.pending)
        .slice(-MAX_HISTORY)
        .map(({ role, content }) => ({ role, content })),
    [messages],
  )

  const runChat = useCallback(
    async (input: string) => {
      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller

      const res = await fetch(API_ENDPOINT, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        signal: controller.signal,
        body: JSON.stringify({
          message: input,
          history,
          context: chatContext,
          language: typeof navigator !== "undefined" ? (navigator.language ?? "en") : "en",
        }),
      })
      return parseReply(res)
    },
    [history, chatContext],
  )

  const send = useCallback(
    async (raw: string) => {
      const trimmed = raw.trim()
      if (!trimmed || isSending || isPlanMutating) return

      const userMsg: Message = { id: newId(), role: "user", content: trimmed }
      const pendingId = newId()
      setMessages((prev) => [
        ...prev,
        userMsg,
        { id: pendingId, role: "assistant", content: "…", pending: true },
      ])
      setDraft("")
      setIsSending(true)

      const refine = plan && updatePlanByChat

      try {
        if (refine) {
          await onRefinePlan(trimmed)
          setMessages((prev) =>
            prev.map((m) =>
              m.id === pendingId
                ? { ...m, content: t("refineFromChatSuccess"), pending: false }
                : m,
            ),
          )
        } else {
          const replyText = await runChat(trimmed)
          setMessages((prev) =>
            prev.map((m) =>
              m.id === pendingId ? { ...m, content: replyText, pending: false } : m,
            ),
          )
        }
      } catch (err) {
        if ((err as Error).name === "AbortError") return
        if (refine) {
          setMessages((prev) =>
            prev.map((m) =>
              m.id === pendingId
                ? { ...m, content: t("refineFromChatError"), pending: false }
                : m,
            ),
          )
        } else {
          setMessages((prev) =>
            prev.map((m) =>
              m.id === pendingId
                ? { ...m, content: "⚠️ " + (err as Error).message, pending: false }
                : m,
            ),
          )
        }
      } finally {
        setIsSending(false)
      }
    },
    [isSending, isPlanMutating, plan, updatePlanByChat, onRefinePlan, runChat, t],
  )

  const onSubmit = useCallback(
    (e: FormEvent<HTMLFormElement>) => {
      e.preventDefault()
      void send(draft)
    },
    [draft, send],
  )

  const onKeyDown = useCallback(
    (e: KeyboardEvent<HTMLInputElement>) => {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault()
        void send(draft)
      }
    },
    [draft, send],
  )

  return (
    <aside
      className={cn(
        "sticky top-6 flex h-[calc(100vh-180px)] min-h-[420px] flex-col overflow-hidden rounded-xl border border-border bg-card shadow-sm",
        className,
      )}
      aria-label={t("assistantTitle")}
    >
      <header className="space-y-2 border-b border-border bg-bg-subtle px-3.5 py-3">
        <div className="flex items-center gap-2">
          <span className="flex size-7 items-center justify-center rounded-md bg-primary/12 text-primary">
            <Sparkles className="size-3.5" aria-hidden />
          </span>
          <div className="flex min-w-0 flex-1 flex-col">
            <span className="text-[12.5px] font-semibold text-foreground">
              {t("assistantTitle")}
            </span>
            <span className="font-mono text-[10px] text-emerald-500">
              ● {t("assistantOnline")}
            </span>
          </div>
        </div>
        {plan && (
          <div className="flex items-center justify-between gap-2 pt-0.5">
            <div className="min-w-0">
              <Label
                htmlFor="ai-plan-chat-edit"
                className="text-[11px] font-normal text-foreground"
              >
                {t("assistantEditToggle")}
              </Label>
              <p className="text-[10px] text-muted-foreground">{t("assistantEditHint")}</p>
            </div>
            <Switch
              id="ai-plan-chat-edit"
              checked={updatePlanByChat}
              onCheckedChange={setUpdatePlanByChat}
              className="shrink-0"
            />
          </div>
        )}
      </header>

      <div ref={scrollRef} className="flex-1 space-y-2.5 overflow-y-auto px-3.5 py-3">
        {messages.map((m) => (
          <div
            key={m.id}
            className={cn(
              "max-w-[88%] rounded-xl px-3 py-2 text-[12.5px] leading-relaxed",
              m.role === "user"
                ? "ml-auto bg-primary text-primary-foreground"
                : "mr-auto bg-bg-subtle text-foreground",
              m.pending && "opacity-60",
            )}
          >
            {m.content}
          </div>
        ))}
      </div>

      <form
        onSubmit={onSubmit}
        className="flex flex-col gap-1.5 border-t border-border bg-bg-subtle px-3 py-2.5"
      >
        <p className="text-[10px] text-muted-foreground">
          {plan
            ? updatePlanByChat
              ? t("assistantInputFooterRefine")
              : t("assistantInputFooterQa")
            : t("assistantContextHint")}
        </p>
        <div className="flex gap-2">
          <Input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={onKeyDown}
            placeholder={
              plan && updatePlanByChat
                ? t("assistantPlaceholderRefine")
                : t("assistantPlaceholder")
            }
            disabled={isSending || isPlanMutating}
            className="h-9 text-[12.5px]"
          />
          <Button
            type="submit"
            size="icon"
            variant="default"
            className="size-9 shrink-0"
            disabled={isSending || isPlanMutating || !draft.trim()}
            aria-label={t("assistantPlaceholder")}
          >
            <Send className="size-3.5" aria-hidden />
          </Button>
        </div>
      </form>
    </aside>
  )
}

async function parseReply(res: Response): Promise<string> {
  if (!res.ok) {
    const text = await res.text().catch(() => "")
    throw new Error(text || `HTTP ${res.status}`)
  }

  const contentType = res.headers.get("content-type") ?? ""
  if (contentType.includes("application/json")) {
    const data = (await res.json()) as Record<string, unknown>
    const candidate =
      data.response ?? data.reply ?? data.message ?? data.content ?? data.text
    if (typeof candidate === "string" && candidate.trim().length > 0) {
      return candidate
    }
    return JSON.stringify(data)
  }

  const raw = await res.text()
  return raw.trim().length > 0 ? raw : "…"
}
