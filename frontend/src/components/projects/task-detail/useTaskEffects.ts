"use client"

import { useEffect, useRef } from "react"
import type { Task } from "./types"
import { resetStateForCreate, syncStateFromTask, type TaskLocalState, type TaskEditingState } from "./useTaskLocalState";

/**
 * Effect to sync local state with task prop or reset for create mode.
 */
export function useTaskSyncEffect(
    task: Task | null,
    isCreateMode: boolean,
    localState: TaskLocalState,
    editingState: TaskEditingState
): void {
    useEffect(() => {
        if (isCreateMode) {
            resetStateForCreate(localState, editingState)
        } else if (task) {
            syncStateFromTask(task, localState, editingState)
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [task, isCreateMode])
}

/**
 * Effect to focus title input in create mode.
 */
export function useTitleFocusEffect(
    isOpen: boolean,
    isCreateMode: boolean,
    titleInputRef: React.RefObject<HTMLInputElement | null>
): void {
    useEffect(() => {
        if (isOpen && isCreateMode) {
            setTimeout(() => titleInputRef.current?.focus(), 100)
        }
    }, [isOpen, isCreateMode, titleInputRef])
}

/**
 * Effect to handle escape key to close panel.
 */
export function useEscapeKeyEffect(isOpen: boolean, onClose: () => void): void {
    useEffect(() => {
        const handleKeyDown = (e: globalThis.KeyboardEvent) => {
            if (e.key === "Escape" && isOpen) {
                onClose()
            }
        }
        globalThis.addEventListener("keydown", handleKeyDown)
        return () => globalThis.removeEventListener("keydown", handleKeyDown)
    }, [isOpen, onClose])
}

/**
 * Hook to create and manage input refs.
 */
export function useInputRefs() {
    const titleInputRef = useRef<HTMLInputElement>(null)
    const fileInputRef = useRef<HTMLInputElement>(null)
    return { titleInputRef, fileInputRef }
}
