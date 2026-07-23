"use client"

import { useCallback } from "react"
import { Calendar, Clock } from "lucide-react"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"

interface TaskDueDateProps {
    readonly dueDate: string | undefined
    readonly isOverdue: boolean
    readonly canEdit: boolean
    readonly isSaving: boolean
    readonly handleDueDateChange: (value: string) => Promise<void>
    readonly t: (key: string) => string
}

/**
 * Renders the task due date field.
 */
export function TaskDueDate({
    dueDate,
    isOverdue,
    canEdit,
    isSaving,
    handleDueDateChange,
    t,
}: TaskDueDateProps) {
    const formattedDate = dueDate ? new Date(dueDate).toISOString().split("T")[0] : ""
    const displayDate = dueDate ? new Date(dueDate).toLocaleDateString() : t("tasks.noDueDate")
    const handleChange = useCallback(
        (e: React.ChangeEvent<HTMLInputElement>) => handleDueDateChange(e.target.value),
        [handleDueDateChange]
    )

    return (
        <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                <Calendar className="h-3 w-3" />
                {t("tasks.dueDate")}
            </Label>
            {canEdit ? (
                <Input
                    type="date"
                    className={cn("h-9", isOverdue && "border-red-500 text-red-500")}
                    value={formattedDate}
                    onChange={handleChange}
                    disabled={isSaving}
                />
            ) : (
                <div className={cn(
                    "flex items-center gap-2 h-9 px-3 border rounded-md",
                    isOverdue ? "bg-red-50 border-red-200 text-red-600 dark:bg-red-900/20 dark:border-red-800" : "bg-muted/30"
                )}>
                    {isOverdue && <Clock className="h-3 w-3" />}
                    <span className="text-sm">{displayDate}</span>
                </div>
            )}
        </div>
    )
}
