"use client"

import { useCallback, type KeyboardEvent } from "react"
import { Input } from "@/components/ui/input"

interface TaskTitleProps {
    readonly isCreateMode: boolean
    readonly editingTitle: boolean
    readonly canEdit: boolean
    readonly localTitle: string
    readonly isSaving: boolean
    readonly titleInputRef: React.RefObject<HTMLInputElement | null>
    readonly setLocalTitle: (value: string) => void
    readonly setEditingTitle: (value: boolean) => void
    readonly handleSaveTitle: () => Promise<void>
    readonly handleTitleKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
    readonly t: (key: string) => string
}

/**
 * Renders the task title with edit capability.
 * Replaces nested ternary with explicit conditional rendering.
 */
export function TaskTitle({
    isCreateMode,
    editingTitle,
    canEdit,
    localTitle,
    isSaving,
    titleInputRef,
    setLocalTitle,
    setEditingTitle,
    handleSaveTitle,
    handleTitleKeyDown,
    t,
}: TaskTitleProps) {
    const isEditing = (isCreateMode || editingTitle) && (isCreateMode || canEdit)

    const handleChange = useCallback(
        (e: React.ChangeEvent<HTMLInputElement>) => setLocalTitle(e.target.value),
        [setLocalTitle]
    )
    const handleStartEditing = useCallback(() => setEditingTitle(true), [setEditingTitle])

    if (isEditing) {
        return (
            <Input
                ref={titleInputRef}
                autoFocus
                value={localTitle}
                onChange={handleChange}
                onBlur={isCreateMode ? undefined : handleSaveTitle}
                onKeyDown={handleTitleKeyDown}
                placeholder={t("tasks.taskTitle")}
                className="text-xl font-semibold h-auto py-1 px-2"
                disabled={isSaving}
            />
        )
    }

    if (canEdit) {
        return (
            <button
                type="button"
                className="text-xl font-semibold py-1 px-2 -mx-2 rounded-md text-left w-full hover:bg-muted/50 cursor-pointer"
                onClick={handleStartEditing}
            >
                {localTitle || t("tasks.untitled")}
            </button>
        )
    }

    return (
        <h2 className="text-xl font-semibold py-1 px-2 -mx-2 rounded-md">
            {localTitle || t("tasks.untitled")}
        </h2>
    )
}
