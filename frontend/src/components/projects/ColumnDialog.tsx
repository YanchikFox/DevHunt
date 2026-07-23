"use client"

import { useTranslations } from "next-intl"
import { useCallback, useEffect } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Checkbox } from "@/components/ui/checkbox"

const createColumnSchema = (t: (key: string) => string) => z.object({
  name: z.string().min(1, t("tasks.columnNameRequired")).max(50, t("tasks.columnNameTooLong")),
  color: z.string().optional(),
  wipLimit: z.union([z.coerce.number().min(0), z.literal("")]).optional().transform(val => typeof val === "number" ? val : undefined),
  isCompleted: z.boolean(),
})

type ColumnFormData = z.infer<ReturnType<typeof createColumnSchema>>

interface TaskColumn {
  id: string
  projectId: string
  name: string
  position: number
  color?: string
  isDefault?: boolean
  isCompleted?: boolean
  wipLimit?: number
  taskCount: number
}

interface ColorButtonProps {
  color: { value: string; label: string }
  isSelected: boolean
  onToggle: (value: string) => void
}

function ColorButton({ color, isSelected, onToggle }: ColorButtonProps) {
  const handleClick = useCallback(() => onToggle(color.value), [onToggle, color.value])
  return (
    <button
      type="button"
      className={`w-8 h-8 rounded-full border-2 transition-all ${isSelected ? "border-foreground scale-110" : "border-transparent hover:scale-105"}`}
      style={{ backgroundColor: color.value }}
      onClick={handleClick}
      title={color.label}
    />
  )
}

interface ColumnDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  column?: TaskColumn | null
  onSubmit: (data: { name: string; color?: string; wipLimit?: number; isCompleted?: boolean }) => Promise<void>
  isSubmitting: boolean
}

const getColumnColors = (t: (key: string) => string) => [
  { value: "#ef4444", label: t("tasks.colorRed") },
  { value: "#f97316", label: t("tasks.colorOrange") },
  { value: "#eab308", label: t("tasks.colorYellow") },
  { value: "#22c55e", label: t("tasks.colorGreen") },
  { value: "#3b82f6", label: t("tasks.colorBlue") },
  { value: "#8b5cf6", label: t("tasks.colorPurple") },
  { value: "#ec4899", label: t("tasks.colorPink") },
  { value: "#6b7280", label: t("tasks.colorGray") },
]

export function ColumnDialog({
  open,
  onOpenChange,
  column,
  onSubmit,
  isSubmitting,
}: ColumnDialogProps) {
  const t = useTranslations()
  const isEditing = Boolean(column)

  const form = useForm({
    resolver: zodResolver(createColumnSchema(t)),
    defaultValues: {
      name: "",
      color: undefined as string | undefined,
      wipLimit: undefined as number | "" | undefined,
      isCompleted: false,
    },
  })

  const { register, handleSubmit, formState: { errors }, reset, watch, setValue } = form
  const selectedColor = watch("color")
  const isCompleted = watch("isCompleted")

  const handleToggleColor = useCallback((colorValue: string) => {
    setValue("color", selectedColor === colorValue ? undefined : colorValue)
  }, [setValue, selectedColor])

  const handleClose = useCallback(() => onOpenChange(false), [onOpenChange])
  const handleIsCompletedChange = useCallback((checked: boolean | "indeterminate") => setValue("isCompleted", checked === true), [setValue])

  useEffect(() => {
    if (column) {
      reset({
        name: column.name,
        color: column.color,
        wipLimit: column.wipLimit,
        isCompleted: column.isCompleted ?? false,
      })
    } else {
      reset({
        name: "",
        color: undefined,
        wipLimit: undefined,
        isCompleted: false,
      })
    }
  }, [column, reset])

  const handleFormSubmit = async (data: ColumnFormData) => {
    await onSubmit({
      name: data.name,
      color: data.color,
      wipLimit: data.wipLimit || undefined,
      isCompleted: data.isCompleted,
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>{isEditing ? t("tasks.editColumn") : t("tasks.addColumn")}</DialogTitle>
          <DialogDescription>
            {isEditing
              ? t("tasks.updateColumnSettings")
              : t("tasks.createNewColumn")}
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit(handleFormSubmit)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="column-name">{t("tasks.columnName")}</Label>
            <Input
              id="column-name"
              {...register("name")}
              placeholder={t("tasks.columnName")}
              aria-invalid={errors.name ? "true" : "false"}
            />
            {errors.name && (
              <p className="text-sm text-destructive">{errors.name.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label>{t("tasks.colorOptional")}</Label>
            <div className="flex flex-wrap gap-2">
              {getColumnColors(t).map((color) => (
                <ColorButton
                  key={color.value}
                  color={color}
                  isSelected={selectedColor === color.value}
                  onToggle={handleToggleColor}
                />
              ))}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="column-wip">{t("tasks.wipLimitOptional")}</Label>
            <Input
              id="column-wip"
              type="number"
              min={0}
              {...register("wipLimit")}
              placeholder={t("tasks.maxTasksInColumn")}
            />
            <p className="text-xs text-muted-foreground">
              {t("tasks.wipLimitDescription")}
            </p>
          </div>

          <div className="flex items-center space-x-2">
            <Checkbox
              id="column-completed"
              checked={isCompleted}
              onCheckedChange={handleIsCompletedChange}
            />
            <Label htmlFor="column-completed" className="text-sm font-normal cursor-pointer">
              {t("tasks.tasksInColumnCompleted")}
            </Label>
          </div>

          <div className="flex justify-end gap-2 pt-4">
            <Button type="button" variant="outline" onClick={handleClose}>
              {t("common.cancel")}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting
                ? isEditing
                  ? t("common.updating")
                  : t("common.creating")
                : isEditing
                  ? t("tasks.updateColumn")
                  : t("tasks.createColumn")}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}
