"use client"

import { useCallback } from "react"
import { X, Trash2, CheckCircle2, Plus } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { priorityConfig, type Task, type PriorityKey } from "./types";
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"

interface TaskDetailHeaderProps {
    readonly isCreateMode: boolean
    readonly targetColumnName?: string | null
    readonly status: string
    readonly priority: string
    readonly canEdit: boolean
    readonly task: Task | null
    readonly onDelete?: (task: Task) => void
    readonly onClose: () => void
    readonly t: (key: string) => string
}

export function TaskDetailHeader({
    isCreateMode,
    targetColumnName,
    status,
    priority,
    canEdit,
    task,
    onDelete,
    onClose,
    t,
}: TaskDetailHeaderProps) {
    const priorityKey = priority as PriorityKey
    const handleDelete = useCallback(() => { if (task && onDelete) onDelete(task) }, [onDelete, task])

    return (
        <div className="flex items-center justify-between border-b border-border px-4 py-3 bg-muted/40 dark:bg-muted/25">
            <div className="flex items-center gap-2">
                {isCreateMode ? (
                    <>
                        <Plus className="h-5 w-5 text-primary" />
                        <span className="text-sm font-medium">{t("tasks.create")}</span>
                        {targetColumnName && (
                            <Badge variant="secondary" className="text-xs">{targetColumnName}</Badge>
                        )}
                    </>
                ) : (
                    <>
                        {status === "done" || status === "completed" ? (
                            <CheckCircle2 className="h-5 w-5 text-green-500" />
                        ) : (
                            <div className={cn("w-3 h-3 rounded-full", priorityConfig[priorityKey]?.color || "bg-blue-500")} />
                        )}
                        <span className="text-sm text-muted-foreground capitalize">{status.replace("_", " ")}</span>
                    </>
                )}
            </div>
            <div className="flex items-center gap-1">
                {!isCreateMode && task && (
                    <ReportDialog targetType={REPORT_TARGET_TYPES.TASK} targetId={task.id ?? task.Id ?? ""} />
                )}
                {!isCreateMode && canEdit && onDelete && task && (
                    <Button variant="ghost" size="sm" className="h-8 w-8 p-0 text-destructive hover:text-destructive" onClick={handleDelete}>
                        <Trash2 className="h-4 w-4" />
                    </Button>
                )}
                <Button variant="ghost" size="sm" className="h-8 w-8 p-0" onClick={onClose}>
                    <X className="h-5 w-5" />
                </Button>
            </div>
        </div>
    )
}
