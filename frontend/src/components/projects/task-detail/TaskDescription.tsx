"use client"

import { useCallback } from "react"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { cn } from "@/lib/utils"
import { getTaskDescription, type Task } from "./types";

interface TaskDescriptionProps {
    readonly task: Task | null
    readonly localDescription: string
    readonly editingDescription: boolean
    readonly canEdit: boolean
    readonly isSaving: boolean
    readonly setLocalDescription: (value: string) => void
    readonly setEditingDescription: (value: boolean) => void
    readonly handleSaveDescription: () => Promise<void>
    readonly t: (key: string) => string
}

/** Read-only description display */
function DescriptionReadOnly({
    localDescription,
    t,
}: {
    readonly localDescription: string
    readonly t: (key: string) => string
}) {
    return (
        <div
            className={cn(
                "min-h-[80px] p-3 rounded-md border text-sm whitespace-pre-wrap",
                !localDescription && "text-muted-foreground italic"
            )}
        >
            {localDescription || t("tasks.addDescription")}
        </div>
    )
}

/** Editable description display */
function DescriptionEditable({
    localDescription,
    setEditingDescription,
    t,
}: {
    readonly localDescription: string
    readonly setEditingDescription: (value: boolean) => void
    readonly t: (key: string) => string
}) {
    const handleClick = useCallback(() => setEditingDescription(true), [setEditingDescription])

    return (
        <button
            type="button"
            className={cn(
                "min-h-[80px] p-3 rounded-md border text-sm whitespace-pre-wrap w-full text-left hover:bg-muted/50 cursor-pointer",
                !localDescription && "text-muted-foreground italic"
            )}
            onClick={handleClick}
        >
            {localDescription || t("tasks.addDescription")}
        </button>
    )
}

/** Description editing form */
function DescriptionEditing({
    task,
    localDescription,
    isSaving,
    setLocalDescription,
    setEditingDescription,
    handleSaveDescription,
    t,
}: {
    readonly task: Task | null
    readonly localDescription: string
    readonly isSaving: boolean
    readonly setLocalDescription: (value: string) => void
    readonly setEditingDescription: (value: boolean) => void
    readonly handleSaveDescription: () => Promise<void>
    readonly t: (key: string) => string
}) {
    const handleChange = useCallback(
        (e: React.ChangeEvent<HTMLTextAreaElement>) => setLocalDescription(e.target.value),
        [setLocalDescription]
    )
    const handleCancel = useCallback(() => {
        setLocalDescription(getTaskDescription(task))
        setEditingDescription(false)
    }, [task, setLocalDescription, setEditingDescription])

    return (
        <div className="space-y-2">
            <Textarea
                autoFocus
                value={localDescription}
                onChange={handleChange}
                rows={5}
                className="resize-none"
                disabled={isSaving}
            />
            <div className="flex items-center gap-2">
                <Button
                    size="sm"
                    onClick={handleSaveDescription}
                    disabled={isSaving}
                >
                    {t("common.save")}
                </Button>
                <Button
                    size="sm"
                    variant="ghost"
                    onClick={handleCancel}
                    disabled={isSaving}
                >
                    {t("common.cancel")}
                </Button>
            </div>
        </div>
    )
}

/**
 * Renders the task description section with edit capability.
 * Replaces nested ternary at lines 717-737 with clear conditional rendering.
 */
export function TaskDescription({
    task,
    localDescription,
    editingDescription,
    canEdit,
    isSaving,
    setLocalDescription,
    setEditingDescription,
    handleSaveDescription,
    t,
}: TaskDescriptionProps) {
    return (
        <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground">
                {t("projects.description")}
            </Label>

            {editingDescription && canEdit ? (
                <DescriptionEditing
                    task={task}
                    localDescription={localDescription}
                    isSaving={isSaving}
                    setLocalDescription={setLocalDescription}
                    setEditingDescription={setEditingDescription}
                    handleSaveDescription={handleSaveDescription}
                    t={t}
                />
            ) : canEdit ? (
                <DescriptionEditable
                    localDescription={localDescription}
                    setEditingDescription={setEditingDescription}
                    t={t}
                />
            ) : (
                <DescriptionReadOnly
                    localDescription={localDescription}
                    t={t}
                />
            )}
        </div>
    )
}
