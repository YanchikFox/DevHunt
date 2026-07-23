"use client"

import { useEffect, useState } from "react"
import { useTranslations } from "next-intl"
import { toast } from "@/hooks/use-toast"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Switch } from "@/components/ui/switch"
import {
  useCreateProjectChannel,
  useUpdateProjectChannel,
  type ProjectChannel,
} from "@/lib/api/queries/project-channels"
import { Loader2, Hash } from "lucide-react"

const SLUG_PATTERN = /^[a-z0-9]([a-z0-9\-_]{0,48}[a-z0-9])?$/

type Props = {
  readonly projectId: string
  readonly channel: ProjectChannel | null
  readonly onSaved: (channel: ProjectChannel) => void
  readonly onCancel: () => void
}

/**
 * Overview tab — edits slug/title/topic/visibility. Works for both create mode
 * (channel = null) and edit mode. The parent dialog owns the tab shell and
 * swaps this in/out; keeping it a dumb form avoids re-fetching on tab switch.
 */
export function ChannelOverviewTab({ projectId, channel, onSaved, onCancel }: Props) {
  const t = useTranslations("chat.channels")
  const tCommon = useTranslations("common")
  const isEdit = Boolean(channel)
  const isGeneral = channel?.slug === "general"

  const [slug, setSlug] = useState("")
  const [title, setTitle] = useState("")
  const [topic, setTopic] = useState("")
  const [isPrivate, setIsPrivate] = useState(false)
  const [slugError, setSlugError] = useState<string | null>(null)

  const createMutation = useCreateProjectChannel(projectId)
  const updateMutation = useUpdateProjectChannel(projectId)
  const isPending = createMutation.isPending || updateMutation.isPending

  useEffect(() => {
    setSlug(channel?.slug ?? "")
    setTitle(channel?.title ?? "")
    setTopic(channel?.topic ?? "")
    setIsPrivate(channel?.isPrivate ?? false)
    setSlugError(null)
  }, [channel])

  const handleSlugChange = (value: string) => {
    const normalized = value.toLowerCase().replace(/\s+/g, "-")
    setSlug(normalized)
    if (normalized === "") {
      setSlugError(null)
    } else if (!SLUG_PATTERN.test(normalized)) {
      setSlugError(t("slugInvalid"))
    } else {
      setSlugError(null)
    }
  }

  const handleSubmit = async () => {
    if (!slug.trim() || !SLUG_PATTERN.test(slug)) {
      setSlugError(t("slugInvalid"))
      return
    }
    try {
      const saved = isEdit && channel
        ? await updateMutation.mutateAsync({
            channelId: channel.id,
            payload: {
              slug: isGeneral ? undefined : slug,
              title: title.trim() || null,
              topic: topic.trim() || null,
              isPrivate: isGeneral ? undefined : isPrivate,
            },
          })
        : await createMutation.mutateAsync({
            slug,
            title: title.trim() || null,
            topic: topic.trim() || null,
            isPrivate,
          })
      onSaved(saved)
    } catch (err) {
      const message =
        err && typeof err === "object" && "message" in err
          ? String((err as { message: unknown }).message)
          : "Request failed"
      if (/409|taken|already/i.test(message)) {
        setSlugError(t("slugTaken"))
      } else {
        toast({ description: message, variant: "destructive" })
      }
    }
  }

  return (
    <div className="space-y-4 py-1">
      {isGeneral && (
        <p className="rounded-md border border-border/60 bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
          {t("generalLocked")}
        </p>
      )}

      <div className="space-y-1.5">
        <Label htmlFor="channel-slug">{t("slugLabel")}</Label>
        <div className="relative">
          <Hash className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="channel-slug"
            value={slug}
            onChange={(e) => handleSlugChange(e.target.value)}
            placeholder={t("slugPlaceholder")}
            disabled={isGeneral}
            className="pl-8"
            maxLength={50}
            autoFocus={!isEdit}
          />
        </div>
        {slugError ? (
          <p className="text-xs text-destructive">{slugError}</p>
        ) : (
          <p className="text-xs text-muted-foreground">{t("slugHint")}</p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="channel-title">{t("titleLabel")}</Label>
        <Input
          id="channel-title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder={t("titlePlaceholder")}
          maxLength={50}
        />
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="channel-topic">{t("topicLabel")}</Label>
        <Textarea
          id="channel-topic"
          value={topic}
          onChange={(e) => setTopic(e.target.value)}
          placeholder={t("topicPlaceholder")}
          maxLength={280}
          rows={2}
          className="resize-none"
        />
      </div>

      <div className="flex items-start justify-between gap-4 rounded-lg border border-border/60 bg-muted/30 px-3 py-2.5">
        <div className="space-y-0.5">
          <Label htmlFor="channel-private" className="text-sm">{t("privateLabel")}</Label>
          <p className="text-xs text-muted-foreground">
            {isPrivate ? t("privateHint") : t("publicHint")}
          </p>
        </div>
        <Switch
          id="channel-private"
          checked={isPrivate}
          onCheckedChange={setIsPrivate}
          disabled={isGeneral}
        />
      </div>

      <div className="flex justify-end gap-2 pt-2">
        <Button variant="ghost" onClick={onCancel} disabled={isPending}>
          {tCommon("cancel")}
        </Button>
        <Button
          onClick={handleSubmit}
          disabled={isPending || slugError !== null || !slug.trim()}
        >
          {isPending && <Loader2 className="mr-2 size-4 animate-spin" />}
          {isEdit ? t("saveSubmit") : t("createSubmit")}
        </Button>
      </div>
    </div>
  )
}
