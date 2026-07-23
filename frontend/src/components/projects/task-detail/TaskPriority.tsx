"use client"

import { Flag } from "lucide-react"
import { Label } from "@/components/ui/label"
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select"
import { cn } from "@/lib/utils"
import { priorityConfig, type PriorityKey } from "./types"

interface TaskPriorityProps {
    readonly priority: string
    readonly canEdit: boolean
    readonly isSaving: boolean
    readonly handlePriorityChange: (value: string) => Promise<void>
    readonly t: (key: string) => string
}

/**
 * Renders the task priority selector.
 */
export function TaskPriority({
    priority,
    canEdit,
    isSaving,
    handlePriorityChange,
    t,
}: TaskPriorityProps) {
    const priorityKey = priority as PriorityKey

    return (
        <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                <Flag className="h-3 w-3" />
                {t("tasks.priority")}
            </Label>
            {canEdit ? (
                <Select value={priority} onValueChange={handlePriorityChange} disabled={isSaving}>
                    <SelectTrigger className="h-9">
                        <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                        {Object.entries(priorityConfig).map(([key, config]) => (
                            <SelectItem key={key} value={key}>
                                <div className="flex items-center gap-2">
                                    <div className={cn("w-2 h-2 rounded-full", config.color)} />
                                    {t(`tasks.${key}`)}
                                </div>
                            </SelectItem>
                        ))}
                    </SelectContent>
                </Select>
            ) : (
                <div className="flex items-center gap-2 h-9 px-3 border rounded-md bg-muted/30">
                    <div className={cn("w-2 h-2 rounded-full", priorityConfig[priorityKey]?.color)} />
                    <span className="text-sm">{t(`tasks.${priority}`)}</span>
                </div>
            )}
        </div>
    )
}
