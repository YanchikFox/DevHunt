"use client"

import { useCallback, type KeyboardEvent } from "react"
import { X, Tag } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"

interface TaskTagsProps {
    readonly localTags: string[]
    readonly editingTags: boolean
    readonly canEdit: boolean
    readonly isSaving: boolean
    readonly newTagInput: string
    readonly setNewTagInput: (value: string) => void
    readonly setEditingTags: (value: boolean) => void
    readonly handleRemoveTag: (tag: string) => void
    readonly handleSaveTags: () => Promise<void>
    readonly handleTagInputKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
    readonly t: (key: string) => string
}

/** Read-only display of tags */
function TagsReadOnly({
    localTags,
    t,
}: {
    readonly localTags: string[]
    readonly t: (key: string) => string
}) {
    if (localTags.length > 0) {
        return (
            <>
                {localTags.map(tag => (
                    <Badge key={tag} variant="outline" className="text-xs px-2 py-0.5 bg-primary/5 text-primary border-primary/20">
                        {tag}
                    </Badge>
                ))}
            </>
        )
    }
    return <span className="text-xs text-muted-foreground italic">{t("tasks.noTags")}</span>
}

interface TagItemProps {
    readonly tag: string
    readonly handleRemoveTag: (tag: string) => void
    readonly removeLabel: string
}

function TagItem({ tag, handleRemoveTag, removeLabel }: TagItemProps) {
    const handleClick = useCallback(() => handleRemoveTag(tag), [handleRemoveTag, tag])
    return (
        <Badge
            variant="outline"
            className="text-xs px-2 py-0.5 bg-primary/5 text-primary border-primary/20 cursor-pointer hover:bg-destructive/10 hover:text-destructive hover:border-destructive/20"
            onClick={handleClick}
            title={removeLabel}
        >
            {tag} <X className="h-3 w-3 ml-1" />
        </Badge>
    )
}

/** Editable tags with input for adding new tags */
function TagsEditing({
    localTags,
    isSaving,
    newTagInput,
    setNewTagInput,
    handleRemoveTag,
    handleSaveTags,
    handleTagInputKeyDown,
    t,
}: {
    readonly localTags: string[]
    readonly isSaving: boolean
    readonly newTagInput: string
    readonly setNewTagInput: (value: string) => void
    readonly handleRemoveTag: (tag: string) => void
    readonly handleSaveTags: () => Promise<void>
    readonly handleTagInputKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
    readonly t: (key: string) => string
}) {
    const handleChange = useCallback(
        (e: React.ChangeEvent<HTMLInputElement>) => setNewTagInput(e.target.value),
        [setNewTagInput]
    )

    return (
        <div className="space-y-2">
            <div className="flex flex-wrap gap-1.5">
                {localTags.map(tag => (
                    <TagItem
                        key={tag}
                        tag={tag}
                        handleRemoveTag={handleRemoveTag}
                        removeLabel={t("tasks.removeTag")}
                    />
                ))}
            </div>
            <div className="flex gap-2">
                <Input
                    autoFocus
                    value={newTagInput}
                    onChange={handleChange}
                    onKeyDown={handleTagInputKeyDown}
                    placeholder={t("tasks.addTag")}
                    className="h-8 text-sm"
                    disabled={isSaving}
                />
                <Button
                    size="sm"
                    variant="outline"
                    className="h-8"
                    onClick={handleSaveTags}
                    disabled={isSaving}
                >
                    {t("common.save")}
                </Button>
            </div>
            <p className="text-xs text-muted-foreground">{t("tasks.tagsHint")}</p>
        </div>
    )
}

/**
 * Renders the task tags section with edit capability.
 * Replaces nested ternary at lines 551-579 with clear conditional rendering.
 */
export function TaskTags({
    localTags,
    editingTags,
    canEdit,
    isSaving,
    newTagInput,
    setNewTagInput,
    setEditingTags,
    handleRemoveTag,
    handleSaveTags,
    handleTagInputKeyDown,
    t,
}: TaskTagsProps) {
    const handleStartEditing = useCallback(() => setEditingTags(true), [setEditingTags])

    return (
        <div className="mt-3 space-y-1.5">
            <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                <Tag className="h-3 w-3" />
                {t("tasks.tags")}
            </Label>

            {editingTags && canEdit ? (
                <TagsEditing
                    localTags={localTags}
                    isSaving={isSaving}
                    newTagInput={newTagInput}
                    setNewTagInput={setNewTagInput}
                    handleRemoveTag={handleRemoveTag}
                    handleSaveTags={handleSaveTags}
                    handleTagInputKeyDown={handleTagInputKeyDown}
                    t={t}
                />
            ) : canEdit ? (
                <button
                    type="button"
                    className="flex flex-wrap gap-1.5 min-h-[28px] p-1.5 -m-1.5 rounded-md w-full text-left hover:bg-muted/50 cursor-pointer"
                    onClick={handleStartEditing}
                >
                    <TagsReadOnly localTags={localTags} t={t} />
                </button>
            ) : (
                <div className="flex flex-wrap gap-1.5 min-h-[28px] p-1.5 -m-1.5 rounded-md">
                    <TagsReadOnly localTags={localTags} t={t} />
                </div>
            )}
        </div>
    )
}
