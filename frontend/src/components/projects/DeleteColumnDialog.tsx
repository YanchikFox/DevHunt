"use client"

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Label } from "@/components/ui/label"
import { useState } from "react"
import { useTranslations } from "next-intl"

interface TaskColumn {
  id: string
  name: string
  taskCount: number
}

interface DeleteColumnDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  column: TaskColumn | null
  otherColumns: TaskColumn[]
  onConfirm: (moveTasksTo?: string) => Promise<void>
  isDeleting: boolean
}

export function DeleteColumnDialog({
  open,
  onOpenChange,
  column,
  otherColumns,
  onConfirm,
  isDeleting,
}: Readonly<DeleteColumnDialogProps>) {
  const t = useTranslations()
  const [moveTasksTo, setMoveTasksTo] = useState<string | undefined>(undefined)
  const hasTasks = column && column.taskCount > 0

  const handleConfirm = async () => {
    await onConfirm(hasTasks ? moveTasksTo : undefined)
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{t("dialogs.deleteColumn")}</AlertDialogTitle>
          <AlertDialogDescription>
            {t("tasks.deleteColumnConfirm", { name: column?.name ?? "" })}
            {hasTasks && (
              <span className="block mt-2 text-orange-600 dark:text-orange-400">
                {t("tasks.columnContainsTasks", { count: column.taskCount })}
              </span>
            )}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {hasTasks && otherColumns.length > 0 && (
          <div className="py-4">
            <Label htmlFor="move-tasks">{t("tasks.moveTasksTo")}</Label>
            <Select value={moveTasksTo} onValueChange={setMoveTasksTo}>
              <SelectTrigger id="move-tasks" className="mt-2">
                <SelectValue placeholder={t("placeholders.selectColumn")} />
              </SelectTrigger>
              <SelectContent>
                {otherColumns.map((col) => (
                  <SelectItem key={col.id} value={col.id}>
                    {col.name} ({t("tasks.tasksCount", { count: col.taskCount })})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        {hasTasks && otherColumns.length === 0 && (
          <div className="py-4 text-sm text-destructive">
            {t("tasks.cannotDeleteOnlyColumn")}
          </div>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={isDeleting}>{t("common.cancel")}</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            disabled={isDeleting || (hasTasks === true && !moveTasksTo && otherColumns.length > 0)}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isDeleting ? t("tasks.deletingColumn") : t("dialogs.deleteColumn")}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
