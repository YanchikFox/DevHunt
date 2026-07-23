"use client"

import Image from "next/image"
import { useState, useCallback } from "react"
import { useBadges, useCreateBadge, useUpdateBadge, useDeleteBadge, CreateAchievementDto } from "@/lib/api/queries/badges"
import { Button } from "@/components/ui/button"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Loader2, Pencil, Trash2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { BadgeFormDialog } from "./BadgeFormDialog"
import type { Badge } from "./types"

export type { Badge }

// ── Sub-component for table rows ────────────────────────────

interface BadgeTableRowProps {
  badge: Badge
  onEdit: (badge: Badge) => void
  onDelete: (id: string) => void
}

function BadgeTableRow({ badge, onEdit, onDelete }: BadgeTableRowProps) {
  const handleEdit = useCallback(() => onEdit(badge), [onEdit, badge])
  const handleDelete = useCallback(() => onDelete(badge.id), [onDelete, badge.id])

  return (
    <TableRow key={badge.id}>
      <TableCell>
        {badge.iconUrl && <Image src={badge.iconUrl} alt={badge.title} width={32} height={32} className="h-8 w-8 rounded-full" />}
      </TableCell>
      <TableCell className="font-medium">{badge.title}</TableCell>
      <TableCell className="font-mono text-xs">{badge.code}</TableCell>
      <TableCell>{badge.category}</TableCell>
      <TableCell>{badge.points}</TableCell>
      <TableCell>
        <div className="flex gap-2">
          <Button size="sm" variant="ghost" onClick={handleEdit}>
            <Pencil className="h-4 w-4" />
          </Button>
          <Button size="sm" variant="ghost" className="text-red-500" onClick={handleDelete}>
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </TableCell>
    </TableRow>
  )
}

// ── Main Component ────────────────────────────────────────────

export function BadgeManagement() {
  const t = useTranslations()
  const { data: badges, isLoading } = useBadges()
  const { toast } = useToast()

  const createBadge = useCreateBadge()
  const updateBadge = useUpdateBadge()
  const deleteBadge = useDeleteBadge()

  const [isDialogOpen, setIsDialogOpen] = useState(false)
  const [editingBadge, setEditingBadge] = useState<Badge | null>(null)

  const handleOpenCreate = useCallback(() => {
    setEditingBadge(null)
    setIsDialogOpen(true)
  }, [])

  const handleEdit = useCallback((badge: Badge) => {
    setEditingBadge(badge)
    setIsDialogOpen(true)
  }, [])

  const handleDelete = useCallback(async (id: string) => {
    if (!confirm(t("admin.deleteBadgeConfirm"))) return
    try {
      await deleteBadge.mutateAsync(id)
      toast({ title: t("admin.badgeDeleted") })
    } catch {
      toast({ title: t("admin.badgeDeleteFailed"), variant: "destructive" })
    }
  }, [deleteBadge, toast, t])

  const handleSave = useCallback(async (data: CreateAchievementDto) => {
    try {
      if (editingBadge) {
        await updateBadge.mutateAsync({ id: editingBadge.id, dto: data })
        toast({ title: t("admin.badgeUpdated") })
      } else {
        await createBadge.mutateAsync(data)
        toast({ title: t("admin.badgeCreated") })
      }
      setIsDialogOpen(false)
      setEditingBadge(null)
    } catch {
      toast({ title: t("admin.badgeSaveFailed"), variant: "destructive" })
    }
  }, [editingBadge, updateBadge, createBadge, toast, t])

  const handleDialogOpenChange = useCallback((open: boolean) => {
    setIsDialogOpen(open)
    if (!open) setEditingBadge(null)
  }, [])

  if (isLoading) return <div className="flex justify-center p-8"><Loader2 className="h-8 w-8 animate-spin" /></div>

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <BadgeFormDialog
          isOpen={isDialogOpen}
          onOpenChange={handleDialogOpenChange}
          editingBadge={editingBadge}
          onSave={handleSave}
          onRequestCreate={handleOpenCreate}
        />
      </div>
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("admin.badgeIcon")}</TableHead>
              <TableHead>{t("admin.badgeTitleLabel")}</TableHead>
              <TableHead>{t("admin.badgeCode")}</TableHead>
              <TableHead>{t("admin.badgeCategory")}</TableHead>
              <TableHead>{t("admin.badgePoints")}</TableHead>
              <TableHead>{t("common.actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {badges?.map((badge) => (
              <BadgeTableRow key={badge.id} badge={badge} onEdit={handleEdit} onDelete={handleDelete} />
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  )
}
