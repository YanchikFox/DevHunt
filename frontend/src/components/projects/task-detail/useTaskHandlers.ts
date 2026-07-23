"use client"

import { useCallback, type KeyboardEvent } from "react"
import { getTaskId, getTaskTitle, getTaskDescription, getTaskTags, type Task } from "./types";
import type { TaskLocalState, TaskEditingState } from "./useTaskLocalState"

export interface TaskSaveConfig {
    isCreateMode: boolean
    canEdit: boolean
    task: Task | null
    onSave: (taskId: string, updates: Record<string, unknown>) => Promise<void>
}

export interface CreateTaskConfig {
    onCreate?: (data: {
        title: string
        description?: string
        priority: string
        dueDate?: string
        assigneeId?: string
        columnId?: string
        tags?: string
    }) => Promise<void>
    targetColumnId?: string | null
    onClose: () => void
}

/**
 * Check if we can save (not create mode, can edit, has task ID, value changed).
 */
function canSaveField(
    config: TaskSaveConfig,
    currentValue: string,
    newValue: string
): { canSave: boolean; taskId: string } {
    const taskId = getTaskId(config.task)
    const canSave = !config.isCreateMode && config.canEdit && !!taskId && newValue !== currentValue
    return { canSave, taskId }
}

/**
 * Hook for task field save handlers.
 */
export function useTaskFieldHandlers(
    localState: TaskLocalState,
    editingState: TaskEditingState,
    config: TaskSaveConfig
) {
    const handleSaveTitle = useCallback(async () => {
        if (config.isCreateMode) {
            editingState.setEditingTitle(false)
            return
        }
        const currentTitle = getTaskTitle(config.task)
        const { canSave, taskId } = canSaveField(config, currentTitle, localState.localTitle.trim())
        if (!canSave) {
            editingState.setEditingTitle(false)
            return
        }
        editingState.setIsSaving(true)
        try {
            await config.onSave(taskId, { title: localState.localTitle.trim() })
        } finally {
            editingState.setIsSaving(false)
            editingState.setEditingTitle(false)
        }
    }, [config, localState.localTitle, editingState])

    const handleSaveDescription = useCallback(async () => {
        if (config.isCreateMode) {
            editingState.setEditingDescription(false)
            return
        }
        const currentDesc = getTaskDescription(config.task)
        const { canSave, taskId } = canSaveField(config, currentDesc, localState.localDescription)
        if (!canSave) {
            editingState.setEditingDescription(false)
            return
        }
        editingState.setIsSaving(true)
        try {
            await config.onSave(taskId, { description: localState.localDescription })
        } finally {
            editingState.setIsSaving(false)
            editingState.setEditingDescription(false)
        }
    }, [config, localState.localDescription, editingState])

    const handleSaveTags = useCallback(async () => {
        if (config.isCreateMode) {
            editingState.setEditingTags(false)
            return
        }
        const taskId = getTaskId(config.task)
        if (!config.canEdit || !taskId) {
            editingState.setEditingTags(false)
            return
        }
        const currentTags = getTaskTags(config.task)
        const newTagsStr = localState.localTags.join(",")
        if (newTagsStr === currentTags.join(",")) {
            editingState.setEditingTags(false)
            return
        }
        editingState.setIsSaving(true)
        try {
            await config.onSave(taskId, { tags: newTagsStr })
        } finally {
            editingState.setIsSaving(false)
            editingState.setEditingTags(false)
        }
    }, [config, localState.localTags, editingState])

    return { handleSaveTitle, handleSaveDescription, handleSaveTags }
}

/**
 * Hook for task field change handlers (priority, due date, assignee).
 */
export function useTaskChangeHandlers(
    localState: TaskLocalState,
    editingState: TaskEditingState,
    config: TaskSaveConfig
) {
    const handlePriorityChange = useCallback(async (value: string) => {
        if (config.isCreateMode) {
            localState.setLocalPriority(value)
            return
        }
        const taskId = getTaskId(config.task)
        if (!config.canEdit || !taskId) return
        editingState.setIsSaving(true)
        try {
            await config.onSave(taskId, { priority: value })
        } finally {
            editingState.setIsSaving(false)
        }
    }, [config, localState, editingState])

    const handleDueDateChange = useCallback(async (value: string) => {
        if (config.isCreateMode) {
            localState.setLocalDueDate(value)
            return
        }
        const taskId = getTaskId(config.task)
        if (!config.canEdit || !taskId) return
        editingState.setIsSaving(true)
        try {
            await config.onSave(taskId, { dueDate: value || undefined })
        } finally {
            editingState.setIsSaving(false)
        }
    }, [config, localState, editingState])

    const handleAssigneeChange = useCallback(async (value: string) => {
        if (config.isCreateMode) {
            localState.setLocalAssigneeId(value === "unassigned" ? "" : value)
            return
        }
        const taskId = getTaskId(config.task)
        if (!config.canEdit || !taskId) return
        editingState.setIsSaving(true)
        try {
            await config.onSave(taskId, { assigneeId: value === "unassigned" ? undefined : value })
        } finally {
            editingState.setIsSaving(false)
        }
    }, [config, localState, editingState])

    return { handlePriorityChange, handleDueDateChange, handleAssigneeChange }
}

/**
 * Hook for tag management handlers.
 */
export function useTagHandlers(localState: TaskLocalState, _task: Task | null) {
    const handleAddTag = useCallback((tag: string) => {
        const trimmed = tag.trim().toLowerCase()
        if (!trimmed || localState.localTags.includes(trimmed)) return
        localState.setLocalTags(prev => [...prev, trimmed])
        localState.setNewTagInput("")
    }, [localState])

    const handleRemoveTag = useCallback((tagToRemove: string) => {
        localState.setLocalTags(prev => prev.filter(t => t !== tagToRemove))
    }, [localState])

    return { handleAddTag, handleRemoveTag }
}

/**
 * Hook for keyboard event handlers.
 */
export function useKeyboardHandlers(
    localState: TaskLocalState,
    editingState: TaskEditingState,
    config: TaskSaveConfig & CreateTaskConfig,
    handlers: {
        handleAddTag: (tag: string) => void
        handleCreateTask: () => Promise<void>
        handleSaveTitle: () => Promise<void>
    }
) {
    const handleTagInputKeyDown = useCallback((e: KeyboardEvent<HTMLInputElement>) => {
        if (e.key === "Enter") {
            e.preventDefault()
            handlers.handleAddTag(localState.newTagInput)
        } else if (e.key === "Escape") {
            editingState.setEditingTags(false)
            localState.setLocalTags(getTaskTags(config.task))
        }
    }, [localState, editingState, config.task, handlers])

    const handleTitleKeyDown = useCallback((e: KeyboardEvent<HTMLInputElement>) => {
        if (e.key === "Enter") {
            if (config.isCreateMode) {
                handlers.handleCreateTask()
            } else {
                handlers.handleSaveTitle()
            }
        } else if (e.key === "Escape") {
            if (config.isCreateMode) {
                config.onClose()
            } else {
                localState.setLocalTitle(getTaskTitle(config.task))
                editingState.setEditingTitle(false)
            }
        }
    }, [config, localState, editingState, handlers])

    return { handleTagInputKeyDown, handleTitleKeyDown }
}

/**
 * Hook for file upload handling.
 */
export function useFileHandler(onUploadAttachment?: (file: File) => void) {
    const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0]
        if (file && onUploadAttachment) {
            onUploadAttachment(file)
        }
        e.target.value = ""
    }, [onUploadAttachment])

    return { handleFileSelect }
}

/**
 * Hook for create task handling.
 */
export function useCreateTaskHandler(
    localState: TaskLocalState,
    editingState: TaskEditingState,
    config: CreateTaskConfig
) {
    const handleCreateTask = useCallback(async () => {
        if (!config.onCreate || !localState.localTitle.trim()) return
        editingState.setIsSaving(true)
        try {
            await config.onCreate({
                title: localState.localTitle.trim(),
                description: localState.localDescription || undefined,
                priority: localState.localPriority,
                dueDate: localState.localDueDate || undefined,
                assigneeId: localState.localAssigneeId || undefined,
                columnId: config.targetColumnId || undefined,
                tags: localState.localTags.length > 0 ? localState.localTags.join(",") : undefined,
            })
            config.onClose()
        } finally {
            editingState.setIsSaving(false)
        }
    }, [localState, editingState, config])

    return { handleCreateTask }
}
