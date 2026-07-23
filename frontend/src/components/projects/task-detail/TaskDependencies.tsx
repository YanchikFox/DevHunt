"use client"

import { useCallback } from "react"
import { Link2, Plus, X } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select"
import { useToast } from "@/hooks/use-toast"
import type { useCreateTaskLink, useDeleteTaskLink } from "@/lib/api/queries/tasks"

interface TaskLink {
    id: string
    linkType: string
    targetTaskId?: string
    targetTaskTitle?: string
}

interface TaskItem {
    id: string
    title: string
}

interface TaskDependenciesProps {
    readonly projectId: string
    readonly taskId: string
    readonly canEdit: boolean
    readonly addingDependency: boolean
    readonly setAddingDependency: (value: boolean) => void
    readonly selectedDependency: string
    readonly setSelectedDependency: (value: string) => void
    readonly allTasks: TaskItem[] | undefined
    readonly links: { outgoing?: TaskLink[] } | undefined
    readonly createLink: ReturnType<typeof useCreateTaskLink>
    readonly deleteLink: ReturnType<typeof useDeleteTaskLink>
    readonly t: (key: string) => string
}

interface DependencyLinkItemProps {
    link: TaskLink
    canEdit: boolean
    projectId: string
    taskId: string
    deleteLink: ReturnType<typeof useDeleteTaskLink>
    t: (key: string) => string
}

function DependencyLinkItem({ link, canEdit, projectId, taskId, deleteLink, t }: DependencyLinkItemProps) {
    const handleDelete = useCallback(() => {
        deleteLink.mutate({ projectId, taskId, linkId: link.id })
    }, [deleteLink, projectId, taskId, link.id])

    return (
        <div className="flex items-center gap-2 p-2 rounded-md bg-muted/30 border border-muted text-xs group">
            <Badge variant="outline" className="text-[10px] uppercase bg-orange-500/10 text-orange-500 border-none px-1 h-4">
                {t("tasks.blocks_this")}
            </Badge>
            <span className="font-medium truncate flex-1">{link.targetTaskTitle}</span>
            {canEdit && (
                <Button
                    variant="ghost"
                    size="icon"
                    className="h-5 w-5 opacity-0 group-hover:opacity-100 transition-opacity"
                    onClick={handleDelete}
                    disabled={deleteLink.isPending}
                >
                    <X className="h-3 w-3" />
                </Button>
            )}
        </div>
    )
}

/**
 * Renders the task dependencies section.
 */
export function TaskDependencies({
    projectId,
    taskId,
    canEdit,
    addingDependency,
    setAddingDependency,
    selectedDependency,
    setSelectedDependency,
    allTasks,
    links,
    createLink,
    deleteLink,
    t,
}: TaskDependenciesProps) {
    const { toast } = useToast()
    const dependsOnLinks = links?.outgoing?.filter(l => l.linkType === 'depends_on') || []

    const handleCancelAddDependency = useCallback(() => {
        setAddingDependency(false)
        setSelectedDependency("")
    }, [setAddingDependency, setSelectedDependency])

    const handleStartAddDependency = useCallback(() => setAddingDependency(true), [setAddingDependency])

    const handleCreateDependency = () => {
        if (!selectedDependency) return

        createLink.mutate({
            projectId,
            taskId,
            targetTaskId: selectedDependency,
            linkType: "depends_on",
        }, {
            onSuccess: () => {
                setSelectedDependency("")
                setAddingDependency(false)
            },
            onError: (error) => {
                const userMessage = (error as { userMessage?: string })?.userMessage || ""
                const isCycleError = userMessage.toLowerCase().includes("cycle")
                toast({
                    title: t("common.error"),
                    description: isCycleError
                        ? t("tasks.dependencyCycleError")
                        : userMessage || t("common.error"),
                    variant: "destructive",
                })
            },
        })
    }

    const availableTasks = allTasks?.filter(t => {
        const tId = t.id
        // Exclude current task and already linked tasks
        if (tId === taskId) return false
        return !dependsOnLinks.some(l => l.targetTaskId === tId)
    }) || []

    return (
        <div className="space-y-3 pt-6 mt-6 border-t">
            <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                <Link2 className="h-3 w-3" />
                {t("tasks.dependencies")}
            </Label>

            <div className="space-y-2">
                {dependsOnLinks.map(link => (
                    <DependencyLinkItem
                        key={link.id}
                        link={link}
                        canEdit={canEdit}
                        projectId={projectId}
                        taskId={taskId}
                        deleteLink={deleteLink}
                        t={t}
                    />
                ))}
                {dependsOnLinks.length === 0 && !addingDependency && (
                    <p className="text-xs text-muted-foreground italic">{t("tasks.noDependencies")}</p>
                )}
            </div>

            {/* Add dependency UI */}
            {canEdit && (
                addingDependency ? (
                    <div className="flex gap-2 items-center">
                        <Select value={selectedDependency} onValueChange={setSelectedDependency}>
                            <SelectTrigger className="h-8 text-xs flex-1">
                                <SelectValue placeholder={t("tasks.selectTask")} />
                            </SelectTrigger>
                            <SelectContent>
                                {availableTasks.map(t => (
                                    <SelectItem key={t.id} value={t.id}>
                                        {t.title}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                        <Button
                            size="sm"
                            className="h-8"
                            onClick={handleCreateDependency}
                            disabled={!selectedDependency || createLink.isPending}
                        >
                            {createLink.isPending ? t("common.loading") : t("common.save")}
                        </Button>
                        <Button
                            size="sm"
                            variant="ghost"
                            className="h-8"
                            onClick={handleCancelAddDependency}
                        >
                            {t("common.cancel")}
                        </Button>
                    </div>
                ) : (
                    <Button
                        variant="outline"
                        size="sm"
                        className="h-7 text-xs"
                        onClick={handleStartAddDependency}
                    >
                        <Plus className="h-3 w-3 mr-1" />
                        {t("tasks.addDependency")}
                    </Button>
                )
            )}
        </div>
    )
}
