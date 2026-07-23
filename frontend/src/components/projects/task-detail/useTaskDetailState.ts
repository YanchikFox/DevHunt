"use client"

import { useTasks, useTaskLinks, useCreateTaskLink, useDeleteTaskLink } from "@/lib/api/queries/tasks"
import { getTaskId, getTaskPriority, getTaskDueDate, type Task, type TaskDetailState, type TaskDetailHandlers } from "./types";
import { useTaskLocalState, useTaskEditingState } from "./useTaskLocalState"
import { useInputRefs, useTaskSyncEffect, useTitleFocusEffect, useEscapeKeyEffect } from "./useTaskEffects"
import {
    useTaskFieldHandlers,
    useTaskChangeHandlers,
    useTagHandlers,
    useKeyboardHandlers,
    useFileHandler,
    useCreateTaskHandler,
} from "./useTaskHandlers"

export interface UseTaskDetailStateProps {
    task: Task | null
    isCreateMode: boolean
    isOpen: boolean
    canEdit: boolean
    projectId: string
    targetColumnId?: string | null
    onSave: (taskId: string, updates: Partial<{
        title: string
        description: string
        priority: string
        dueDate: string
        assigneeId: string
        tags: string
    }>) => Promise<void>
    onCreate?: (data: {
        title: string
        description?: string
        priority: string
        dueDate?: string
        assigneeId?: string
        columnId?: string
        tags?: string
    }) => Promise<void>
    onClose: () => void
    onUploadAttachment?: (file: File) => void
}

export interface UseTaskDetailStateResult {
    state: TaskDetailState
    handlers: TaskDetailHandlers
    refs: {
        titleInputRef: React.RefObject<HTMLInputElement | null>
        fileInputRef: React.RefObject<HTMLInputElement | null>
    }
    taskQueries: {
        taskId: string
        allTasks: ReturnType<typeof useTasks>["data"]
        links: ReturnType<typeof useTaskLinks>["data"]
        createLink: ReturnType<typeof useCreateTaskLink>
        deleteLink: ReturnType<typeof useDeleteTaskLink>
    }
    computed: {
        priority: string
        dueDate: string | undefined
        status: string
        isOverdue: boolean
    }
}

/**
 * Main hook for TaskDetailPanel state management.
 * Composes smaller focused hooks for better maintainability.
 */
export function useTaskDetailState({
    task,
    isCreateMode,
    isOpen,
    canEdit,
    projectId,
    targetColumnId,
    onSave,
    onCreate,
    onClose,
    onUploadAttachment,
}: UseTaskDetailStateProps): UseTaskDetailStateResult {
    // State hooks
    const localState = useTaskLocalState()
    const editingState = useTaskEditingState()
    const refs = useInputRefs()

    // Task queries
    const taskId = getTaskId(task)
    const { data: allTasks } = useTasks(projectId)
    const { data: links } = useTaskLinks(projectId, taskId)
    const createLink = useCreateTaskLink()
    const deleteLink = useDeleteTaskLink()

    // Computed values
    const priority = isCreateMode ? localState.localPriority : getTaskPriority(task)
    const dueDate = isCreateMode ? localState.localDueDate : getTaskDueDate(task)
    const status = (task?.Status || task?.status || "").toLowerCase()
    const isOverdue = !isCreateMode && !!dueDate && new Date(dueDate) < new Date()

    // Config objects for handlers
    const saveConfig = { isCreateMode, canEdit, task, onSave }
    const createConfig = { onCreate, targetColumnId, onClose }

    // Effects
    useTaskSyncEffect(task, isCreateMode, localState, editingState)
    useTitleFocusEffect(isOpen, isCreateMode, refs.titleInputRef)
    useEscapeKeyEffect(isOpen, onClose)

    // Handler hooks
    const fieldHandlers = useTaskFieldHandlers(localState, editingState, saveConfig)
    const changeHandlers = useTaskChangeHandlers(localState, editingState, saveConfig)
    const tagHandlers = useTagHandlers(localState, task)
    const { handleFileSelect } = useFileHandler(onUploadAttachment)
    const { handleCreateTask } = useCreateTaskHandler(localState, editingState, createConfig)

    const keyboardHandlers = useKeyboardHandlers(
        localState,
        editingState,
        { ...saveConfig, ...createConfig },
        {
            handleAddTag: tagHandlers.handleAddTag,
            handleCreateTask,
            handleSaveTitle: fieldHandlers.handleSaveTitle,
        }
    )

    // Combine state for return (spread from decomposed hooks)
    const state: TaskDetailState = {
        ...localState,
        ...editingState,
    }

    const handlers: TaskDetailHandlers = {
        handleCreateTask,
        handleSaveTitle: fieldHandlers.handleSaveTitle,
        handleSaveDescription: fieldHandlers.handleSaveDescription,
        handleSaveTags: fieldHandlers.handleSaveTags,
        handlePriorityChange: changeHandlers.handlePriorityChange,
        handleDueDateChange: changeHandlers.handleDueDateChange,
        handleAssigneeChange: changeHandlers.handleAssigneeChange,
        handleAddTag: tagHandlers.handleAddTag,
        handleRemoveTag: tagHandlers.handleRemoveTag,
        handleTagInputKeyDown: keyboardHandlers.handleTagInputKeyDown,
        handleTitleKeyDown: keyboardHandlers.handleTitleKeyDown,
        handleFileSelect,
    }

    return {
        state,
        handlers,
        refs: {
            titleInputRef: refs.titleInputRef,
            fileInputRef: refs.fileInputRef,
        },
        taskQueries: {
            taskId,
            allTasks,
            links,
            createLink,
            deleteLink,
        },
        computed: {
            priority,
            dueDate,
            status,
            isOverdue,
        },
    }
}
