"use client"

import { useState, useCallback, useEffect } from "react"
import { useSuspendUser } from "@/lib/api/queries/admin"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Loader2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

interface SuspendUserDialogProps {
  userId: string
  open: boolean
  onOpenChange: (open: boolean) => void
}

function getTomorrowDateInputValue() {
  const tomorrow = new Date()
  tomorrow.setDate(tomorrow.getDate() + 1)
  return tomorrow.toISOString().split("T")[0]
}

export function SuspendUserDialog({ userId, open, onOpenChange }: SuspendUserDialogProps) {
  const t = useTranslations("admin")
  const { toast } = useToast()
  const [date, setDate] = useState("")
  const [reason, setReason] = useState("")
  const [minDate, setMinDate] = useState("")
  const suspendUser = useSuspendUser()

  useEffect(() => {
    if (open) setMinDate(getTomorrowDateInputValue())
  }, [open])

  const handleSubmit = useCallback(async () => {
    if (!date || !reason.trim()) return
    try {
      await suspendUser.mutateAsync({
        userId,
        suspendedUntil: new Date(date).toISOString(),
        reason: reason.trim(),
      })
      toast({ title: t("userSuspended") })
      onOpenChange(false)
      setDate("")
      setReason("")
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [userId, date, reason, suspendUser, toast, t, onOpenChange])

  const handleDateChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setDate(e.target.value)
  }, [])

  const handleReasonChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setReason(e.target.value)
  }, [])

  const handleCancel = useCallback(() => {
    onOpenChange(false)
  }, [onOpenChange])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t("suspendUser")}</DialogTitle>
          <DialogDescription>{t("suspendUserDescription")}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <label className="text-sm font-medium mb-1 block">{t("suspendUntil")}</label>
            <Input type="date" value={date} onChange={handleDateChange} min={minDate} />
          </div>
          <div>
            <label className="text-sm font-medium mb-1 block">{t("reason")}</label>
            <Textarea value={reason} onChange={handleReasonChange} placeholder={t("suspensionReasonPlaceholder")} />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleCancel}>{t("cancel")}</Button>
          <Button
            variant="destructive"
            onClick={handleSubmit}
            disabled={suspendUser.isPending || !date || !reason.trim()}
          >
            {suspendUser.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
            {t("suspend")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
