"use client"

import { useState, useCallback, useMemo } from "react"
import { useSubmitReport } from "@/lib/api/queries/moderation"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Flag, Loader2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

export interface ReportDialogProps {
  targetType: string
  targetId: string
  trigger?: React.ReactNode
}

const REASON_PRESETS = [
  "spam",
  "harassment",
  "inappropriate_content",
  "misinformation",
  "other",
] as const

export function ReportDialog({ targetType, targetId, trigger }: ReportDialogProps) {
  const t = useTranslations("moderation")
  const { toast } = useToast()
  const submitReport = useSubmitReport()

  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState("")
  const [preset, setPreset] = useState<string>("")

  const effectiveReason = useMemo(() => {
    if (!preset || preset === "other") return reason
    return preset
  }, [preset, reason])

  const handleOpen = useCallback(() => setOpen(true), [])

  const handleOpenChange = useCallback((v: boolean) => {
    setOpen(v)
    if (!v) {
      setReason("")
      setPreset("")
    }
  }, [])

  const handlePresetChange = useCallback((v: string) => setPreset(v), [])

  const handleReasonChange = useCallback(
    (e: React.ChangeEvent<HTMLTextAreaElement>) => setReason(e.target.value),
    []
  )

  const handleSubmit = useCallback(async () => {
    if (!effectiveReason.trim()) return
    try {
      await submitReport.mutateAsync({
        targetType,
        targetId,
        reason: effectiveReason,
      })
      toast({ title: t("reportSuccess") })
      setOpen(false)
      setReason("")
      setPreset("")
    } catch {
      toast({ title: t("reportFailed"), variant: "destructive" })
    }
  }, [effectiveReason, targetType, targetId, submitReport, toast, t])

  return (
    <>
      {trigger ? (
        <span onClick={handleOpen} className="cursor-pointer">
          {trigger}
        </span>
      ) : (
        <Button
          variant="ghost"
          size="icon"
          onClick={handleOpen}
          title={t("reportDialogTitle")}
          className="h-8 w-8 text-muted-foreground/50 hover:text-destructive hover:bg-destructive/10 transition-colors"
        >
          <Flag className="h-3.5 w-3.5" />
        </Button>
      )}
      <Dialog open={open} onOpenChange={handleOpenChange}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("reportDialogTitle")}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <Select value={preset} onValueChange={handlePresetChange}>
              <SelectTrigger>
                <SelectValue placeholder={t("selectReason")} />
              </SelectTrigger>
              <SelectContent>
                {REASON_PRESETS.map((r) => (
                  <SelectItem key={r} value={r}>
                    {t(`reasons.${r}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            {preset === "other" && (
              <Textarea
                placeholder={t("reportReasonPlaceholder")}
                value={reason}
                onChange={handleReasonChange}
                maxLength={500}
                rows={3}
              />
            )}
          </div>
          <DialogFooter>
            <Button
              onClick={handleSubmit}
              disabled={submitReport.isPending || !effectiveReason.trim()}
            >
              {submitReport.isPending && (
                <Loader2 className="h-4 w-4 animate-spin mr-1" />
              )}
              {t("reportSubmit")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
