"use client"

import { useState } from "react"
import { getTaskTitle, getTaskDescription, getTaskPriority, getTaskDueDate, getTaskTags, type Task } from "./types";

export interface TaskLocalState {
    localTitle: string
    setLocalTitle: (value: string) => void
    localDescription: string
    setLocalDescription: (value: string) => void
    localPriority: string
    setLocalPriority: (value: string) => void
    localDueDate: string
    setLocalDueDate: (value: string) => void
    localAssigneeId: string
    setLocalAssigneeId: (value: string) => void
    localTags: string[]
    setLocalTags: React.Dispatch<React.SetStateAction<string[]>>
    newTagInput: string
    setNewTagInput: (value: string) => void
}

/**
 * Hook for managing local form state for task editing.
 */
export function useTaskLocalState(): TaskLocalState {
    const [localTitle, setLocalTitle] = useState("")
    const [localDescription, setLocalDescription] = useState("")
    const [localPriority, setLocalPriority] = useState<string>("medium")
    const [localDueDate, setLocalDueDate] = useState<string>("")
    const [localAssigneeId, setLocalAssigneeId] = useState<string>("")
    const [localTags, setLocalTags] = useState<string[]>([])
    const [newTagInput, setNewTagInput] = useState("")

    return {
        localTitle,
        setLocalTitle,
        localDescription,
        setLocalDescription,
        localPriority,
        setLocalPriority,
        localDueDate,
        setLocalDueDate,
        localAssigneeId,
        setLocalAssigneeId,
        localTags,
        setLocalTags,
        newTagInput,
        setNewTagInput,
    }
}

export interface TaskEditingState {
    editingTitle: boolean
    setEditingTitle: (value: boolean) => void
    editingDescription: boolean
    setEditingDescription: (value: boolean) => void
    editingTags: boolean
    setEditingTags: (value: boolean) => void
    isSaving: boolean
    setIsSaving: (value: boolean) => void
    addingDependency: boolean
    setAddingDependency: (value: boolean) => void
    selectedDependency: string
    setSelectedDependency: (value: string) => void
}

/**
 * Hook for managing editing state flags.
 */
export function useTaskEditingState(): TaskEditingState {
    const [editingTitle, setEditingTitle] = useState(false)
    const [editingDescription, setEditingDescription] = useState(false)
    const [editingTags, setEditingTags] = useState(false)
    const [isSaving, setIsSaving] = useState(false)
    const [addingDependency, setAddingDependency] = useState(false)
    const [selectedDependency, setSelectedDependency] = useState<string>("")

    return {
        editingTitle,
        setEditingTitle,
        editingDescription,
        setEditingDescription,
        editingTags,
        setEditingTags,
        isSaving,
        setIsSaving,
        addingDependency,
        setAddingDependency,
        selectedDependency,
        setSelectedDependency,
    }
}

/**
 * Resets local state for create mode.
 */
export function resetStateForCreate(
    localState: TaskLocalState,
    editingState: TaskEditingState
): void {
    localState.setLocalTitle("")
    localState.setLocalDescription("")
    localState.setLocalPriority("medium")
    localState.setLocalDueDate("")
    localState.setLocalAssigneeId("")
    localState.setLocalTags([])
    editingState.setEditingTitle(true)
    editingState.setEditingTags(false)
}

/**
 * Syncs local state with task data for edit mode.
 */
export function syncStateFromTask(
    task: Task,
    localState: TaskLocalState,
    editingState: TaskEditingState
): void {
    localState.setLocalTitle(getTaskTitle(task))
    localState.setLocalDescription(getTaskDescription(task))
    localState.setLocalPriority(getTaskPriority(task))
    const dueDate = getTaskDueDate(task)
    localState.setLocalDueDate(dueDate ? new Date(dueDate).toISOString().split("T")[0] : "")
    localState.setLocalAssigneeId(task.assigneeId || "")
    localState.setLocalTags(getTaskTags(task))
    editingState.setEditingTitle(false)
    editingState.setEditingTags(false)
}
