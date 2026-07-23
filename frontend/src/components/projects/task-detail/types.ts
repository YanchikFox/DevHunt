import type { KeyboardEvent } from "react"

export interface Task {
    Id?: string
    id?: string
    Title?: string
    title?: string
    Description?: string
    description?: string
    Priority?: string
    priority?: string
    Deadline?: string
    deadline?: string
    dueDate?: string
    Status?: string
    status?: string
    columnId?: string
    assigneeId?: string
    assigneeName?: string
    assigneeAvatarUrl?: string
    Tags?: string
    tags?: string
}

export interface TeamMember {
    userId?: string
    UserId?: string
    id?: string
    Id?: string
    fullName?: string
    FullName?: string
    email?: string
    Email?: string
    avatarUrl?: string
}

export interface Attachment {
    id: string
    fileName: string
    contentType?: string
    fileSize?: number
    attachedAt: string
    attachedByUserName?: string
}

export interface TaskDetailPanelProps {
    readonly task: Task | null
    readonly isOpen: boolean
    readonly onClose: () => void
    readonly onSave: (taskId: string, updates: Partial<{
        title: string
        description: string
        priority: string
        dueDate: string
        assigneeId: string
        tags: string
    }>) => Promise<void>
    readonly onCreate?: (data: {
        title: string
        description?: string
        priority: string
        dueDate?: string
        assigneeId?: string
        columnId?: string
        tags?: string
    }) => Promise<void>
    readonly onDelete?: (task: Task) => void
    readonly projectId: string
    readonly teamMembers: TeamMember[]
    readonly attachments: Attachment[]
    readonly attachmentsLoading?: boolean
    readonly canEdit: boolean
    readonly onUploadAttachment?: (file: File) => void
    readonly isUploading?: boolean
    readonly onDeleteAttachment?: (attachmentId: string) => void
    readonly isCreateMode?: boolean
    readonly targetColumnId?: string | null
    readonly targetColumnName?: string | null
}

export const priorityConfig = {
    urgent: { color: "bg-red-500", textColor: "text-red-500", label: "Urgent" },
    high: { color: "bg-orange-500", textColor: "text-orange-500", label: "High" },
    medium: { color: "bg-blue-500", textColor: "text-blue-500", label: "Medium" },
    low: { color: "bg-emerald-500", textColor: "text-emerald-500", label: "Low" },
} as const

export type PriorityKey = keyof typeof priorityConfig

// Helper to get normalized task values
export function getTaskValue<T>(primary: T | undefined, fallback: T | undefined, defaultValue: T): T {
    return primary ?? fallback ?? defaultValue
}

export function getTaskId(task: Task | null): string {
    return task?.Id || task?.id || ""
}

export function getTaskTitle(task: Task | null): string {
    return task?.Title || task?.title || ""
}

export function getTaskDescription(task: Task | null): string {
    return task?.Description || task?.description || ""
}

export function getTaskPriority(task: Task | null): string {
    return (task?.Priority || task?.priority || "medium").toLowerCase()
}

export function getTaskDueDate(task: Task | null): string | undefined {
    return task?.Deadline || task?.deadline || task?.dueDate
}

export function getTaskTags(task: Task | null): string[] {
    const tagsStr = task?.Tags || task?.tags || ""
    return tagsStr ? tagsStr.split(",").map(t => t.trim()).filter(Boolean) : []
}

export function getTaskStatus(task: Task | null): string {
    return (task?.Status || task?.status || "").toLowerCase()
}

export interface TaskDetailState {
    editingTitle: boolean
    setEditingTitle: (value: boolean) => void
    editingDescription: boolean
    setEditingDescription: (value: boolean) => void
    editingTags: boolean
    setEditingTags: (value: boolean) => void
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
    isSaving: boolean
    setIsSaving: (value: boolean) => void
    addingDependency: boolean
    setAddingDependency: (value: boolean) => void
    selectedDependency: string
    setSelectedDependency: (value: string) => void
}

export interface TaskDetailHandlers {
    handleCreateTask: () => Promise<void>
    handleSaveTitle: () => Promise<void>
    handleSaveDescription: () => Promise<void>
    handleSaveTags: () => Promise<void>
    handlePriorityChange: (value: string) => Promise<void>
    handleDueDateChange: (value: string) => Promise<void>
    handleAssigneeChange: (value: string) => Promise<void>
    handleAddTag: (tag: string) => void
    handleRemoveTag: (tag: string) => void
    handleTagInputKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
    handleTitleKeyDown: (e: KeyboardEvent<HTMLInputElement>) => void
    handleFileSelect: (e: React.ChangeEvent<HTMLInputElement>) => void
}
