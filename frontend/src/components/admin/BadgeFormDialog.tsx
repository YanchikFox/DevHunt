"use client"

import { useState, useCallback, useEffect, useMemo } from "react"
import { CreateAchievementDto } from "@/lib/api/queries/badges"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog"
import { Plus } from "lucide-react"
import { useTranslations } from "next-intl"
import type { Badge } from "./types"

const EMPTY_FORM: CreateAchievementDto = { code: "", title: "", description: "", iconUrl: "", category: "", points: 0 }

export interface BadgeFormDialogProps {
  isOpen: boolean
  onOpenChange: (open: boolean) => void
  editingBadge: Badge | null
  onSave: (data: CreateAchievementDto) => Promise<void>
  onRequestCreate: () => void
}

export function BadgeFormDialog({ isOpen, onOpenChange, editingBadge, onSave, onRequestCreate }: BadgeFormDialogProps) {
  const t = useTranslations()
  const [formData, setFormData] = useState<CreateAchievementDto>(EMPTY_FORM)

  useEffect(() => {
    setFormData(editingBadge
      ? { code: editingBadge.code, title: editingBadge.title, description: editingBadge.description ?? "", iconUrl: editingBadge.iconUrl ?? "", category: editingBadge.category ?? "", points: editingBadge.points }
      : EMPTY_FORM
    )
  }, [editingBadge])

  // Single stable handler object replaces 7 individual useCallbacks
  const handlers = useMemo(() => ({
    code: (e: React.ChangeEvent<HTMLInputElement>) => setFormData(p => ({ ...p, code: e.target.value })),
    title: (e: React.ChangeEvent<HTMLInputElement>) => setFormData(p => ({ ...p, title: e.target.value })),
    description: (e: React.ChangeEvent<HTMLTextAreaElement>) => setFormData(p => ({ ...p, description: e.target.value })),
    category: (e: React.ChangeEvent<HTMLInputElement>) => setFormData(p => ({ ...p, category: e.target.value })),
    iconUrl: (e: React.ChangeEvent<HTMLInputElement>) => setFormData(p => ({ ...p, iconUrl: e.target.value })),
    points: (e: React.ChangeEvent<HTMLInputElement>) => setFormData(p => ({ ...p, points: parseInt(e.target.value) || 0 })),
  }), [])

  const handleSubmit = useCallback(() => onSave(formData), [onSave, formData])

  return (
    <Dialog open={isOpen} onOpenChange={onOpenChange}>
      <DialogTrigger asChild>
        <Button onClick={onRequestCreate}>
          <Plus className="h-4 w-4 mr-2" /> {t("admin.createBadge")}
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{editingBadge ? t("admin.editBadge") : t("admin.createBadge")}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t("admin.badgeCode")}</label>
              <Input value={formData.code} onChange={handlers.code} disabled={!!editingBadge} placeholder={t("placeholders.badgeCodePlaceholder")} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t("admin.badgePoints")}</label>
              <Input type="number" value={formData.points} onChange={handlers.points} />
            </div>
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("admin.badgeTitleLabel")}</label>
            <Input value={formData.title} onChange={handlers.title} placeholder={t("placeholders.badgeTitle")} />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium">{t("admin.badgeDescriptionLabel")}</label>
            <Textarea value={formData.description} onChange={handlers.description} placeholder={t("placeholders.badgeDescription")} />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label className="text-sm font-medium">{t("admin.badgeCategory")}</label>
              <Input value={formData.category} onChange={handlers.category} placeholder={t("placeholders.badgeCategoryPlaceholder")} />
            </div>
            <div className="space-y-2">
              <label className="text-sm font-medium">{t("admin.badgeIconUrl")}</label>
              <Input value={formData.iconUrl} onChange={handlers.iconUrl} placeholder="https://..." />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button onClick={handleSubmit}>{editingBadge ? t("common.save") : t("common.create")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
