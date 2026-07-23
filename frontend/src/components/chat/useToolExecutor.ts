"use client"

import { useCallback } from "react"
import { useCreateTask, useUpdateTask, useDeleteTask, useMoveTask, useTasks } from "@/lib/api/queries/tasks"
import { useColumns } from "@/lib/api/queries/columns"
import { useUpdateProject, useProject } from "@/lib/api/queries/projects"
import type {
    ToolCall,
    ToolExecutionResult,
    CreateTaskArgs,
    UpdateTaskArgs,
    DeleteTaskArgs,
    MoveTaskArgs,
    CreateMultipleTasksArgs,
    DeleteMultipleTasksArgs,
    MoveMultipleTasksArgs,
    UpdateProjectArgs,
    GenerateNewsDraftArgs,
} from "@/lib/api/agent-types"

interface UseToolExecutorOptions {
    projectId: string
    onSuccess?: (result: ToolExecutionResult) => void
    onError?: (result: ToolExecutionResult) => void
}

interface ToolExecutor {
    executeToolCall: (toolCall: ToolCall) => Promise<ToolExecutionResult>
    executeToolCalls: (toolCalls: ToolCall[]) => Promise<ToolExecutionResult[]>
    isExecuting: boolean
}

/**
 * Hook for executing AI tool calls.
 *
 * Uses existing mutations (useCreateTask, useUpdateTask, etc.) to perform actions.
 * Each tool call is executed and results are tracked.
 */
export function useToolExecutor({
    projectId,
    onSuccess,
    onError,
}: UseToolExecutorOptions): ToolExecutor {
    // Get current project data for update_project (backend requires Title and Description)
    const { data: currentProject } = useProject(projectId)
    const { data: currentTasks } = useTasks(projectId)
    const { data: currentColumns } = useColumns(projectId)

    const createTaskMutation = useCreateTask()
    const updateTaskMutation = useUpdateTask()
    const deleteTaskMutation = useDeleteTask()
    const moveTaskMutation = useMoveTask()
    const updateProjectMutation = useUpdateProject()

    const isExecuting =
        createTaskMutation.isPending ||
        updateTaskMutation.isPending ||
        deleteTaskMutation.isPending ||
        moveTaskMutation.isPending ||
        updateProjectMutation.isPending

    /**
     * Execute a single tool call
     */
    const executeToolCall = useCallback(
        async (toolCall: ToolCall): Promise<ToolExecutionResult> => {
            const { id: toolCallId, function: fn } = toolCall
            const toolName = fn.name
            const args = fn.arguments_parsed || {}

            try {
                let result: unknown

                switch (toolName) {
                    // -----------------------------------------------------------------
                    // Task Tools
                    // -----------------------------------------------------------------
                    case "create_task": {
                        const createArgs = args as unknown as CreateTaskArgs
                        const priority = createArgs.priority === "critical" ? "urgent" : createArgs.priority
                        result = await createTaskMutation.mutateAsync({
                            projectId,
                            title: createArgs.title,
                            description: createArgs.description,
                            priority: priority || "medium",
                            columnId: createArgs.columnId,
                            assigneeId: createArgs.assigneeId,
                            tags: createArgs.tags?.join(","),
                            estimatedHours: createArgs.estimatedHours,
                        })
                        break
                    }

                    case "update_task": {
                        const updateArgs = args as unknown as UpdateTaskArgs
                        const priority = updateArgs.priority === "critical" ? "urgent" : updateArgs.priority
                        await updateTaskMutation.mutateAsync({
                            projectId,
                            taskId: updateArgs.taskId,
                            updates: {
                                title: updateArgs.title,
                                description: updateArgs.description,
                                priority: priority,
                                assigneeId: updateArgs.assigneeId,
                                tags: updateArgs.tags?.join(","),
                                estimatedHours: updateArgs.estimatedHours,
                                actualHours: updateArgs.actualHours,
                            },
                        })
                        result = { updated: true, taskId: updateArgs.taskId }
                        break
                    }

                    case "delete_task": {
                        const deleteArgs = args as unknown as DeleteTaskArgs
                        if (!deleteArgs.taskId) {
                            throw new Error("taskId is required for delete_task")
                        }
                        await deleteTaskMutation.mutateAsync({
                            projectId,
                            taskId: deleteArgs.taskId,
                        })
                        result = { deleted: true, taskId: deleteArgs.taskId }
                        break
                    }

                    case "move_task": {
                        const moveArgs = args as unknown as MoveTaskArgs
                        if (!moveArgs.columnId && !moveArgs.columnName) {
                            throw new Error("columnId or columnName is required for move_task")
                        }
                        await moveTaskMutation.mutateAsync({
                            projectId,
                            taskId: moveArgs.taskId,
                            columnId: moveArgs.columnId || "",
                            positionInColumn: moveArgs.position,
                        })
                        result = { moved: true, taskId: moveArgs.taskId, columnId: moveArgs.columnId }
                        break
                    }

                    case "create_multiple_tasks": {
                        const multiArgs = args as unknown as CreateMultipleTasksArgs
                        const createdTasks = []
                        for (const taskDef of multiArgs.tasks) {
                            const priority = taskDef.priority === "critical" ? "urgent" : taskDef.priority
                            const created = await createTaskMutation.mutateAsync({
                                projectId,
                                title: taskDef.title,
                                description: taskDef.description,
                                priority: priority || "medium",
                                columnId: multiArgs.columnId,
                                tags: taskDef.tags?.join(","),
                            })
                            createdTasks.push(created)
                        }
                        result = { created: createdTasks.length, tasks: createdTasks }
                        break
                    }

                    case "delete_multiple_tasks": {
                        const bulkArgs = args as DeleteMultipleTasksArgs
                        let idsToDelete: string[] = []

                        if (bulkArgs.taskIds && bulkArgs.taskIds.length > 0) {
                            idsToDelete = bulkArgs.taskIds
                        } else if (bulkArgs.filter === "all") {
                            idsToDelete = (currentTasks ?? []).map(t => t.id)
                        } else if (bulkArgs.filter === "column" && bulkArgs.columnName) {
                            // Find column by name, then filter tasks by columnId
                            const col = (currentColumns ?? []).find(
                                c => c.name.toLowerCase() === bulkArgs.columnName!.toLowerCase()
                            )
                            if (col) {
                                idsToDelete = (currentTasks ?? [])
                                    .filter(t => t.columnId === col.id)
                                    .map(t => t.id)
                            }
                        }

                        if (idsToDelete.length === 0) {
                            result = { deleted: 0, message: "No tasks found to delete" }
                            break
                        }

                        let deletedCount = 0
                        for (const taskId of idsToDelete) {
                            try {
                                await deleteTaskMutation.mutateAsync({ projectId, taskId })
                                deletedCount++
                            } catch (err) {
                                console.warn(`Failed to delete task ${taskId}:`, err)
                            }
                        }
                        result = { deleted: deletedCount, total: idsToDelete.length }
                        break
                    }

                    case "move_multiple_tasks": {
                        const mvArgs = args as unknown as MoveMultipleTasksArgs
                        let idsToMove: string[] = []

                        if (mvArgs.taskIds && mvArgs.taskIds.length > 0) {
                            idsToMove = mvArgs.taskIds
                        } else if (mvArgs.filter === "all") {
                            idsToMove = (currentTasks ?? []).map(t => t.id)
                        } else if (mvArgs.filter === "column" && mvArgs.sourceColumnName) {
                            const srcCol = (currentColumns ?? []).find(
                                c => c.name.toLowerCase() === mvArgs.sourceColumnName!.toLowerCase()
                            )
                            if (srcCol) {
                                idsToMove = (currentTasks ?? [])
                                    .filter(t => t.columnId === srcCol.id)
                                    .map(t => t.id)
                            }
                        }

                        // Resolve target column ID from name
                        let targetColId = mvArgs.targetColumnId || ""
                        if (!targetColId && mvArgs.targetColumnName) {
                            const tgtCol = (currentColumns ?? []).find(
                                c => c.name.toLowerCase() === mvArgs.targetColumnName.toLowerCase()
                            )
                            if (tgtCol) targetColId = tgtCol.id
                        }

                        if (idsToMove.length === 0 || !targetColId) {
                            result = { moved: 0, message: "No tasks found to move or target column not found" }
                            break
                        }

                        let movedCount = 0
                        for (const taskId of idsToMove) {
                            try {
                                await moveTaskMutation.mutateAsync({
                                    projectId,
                                    taskId,
                                    columnId: targetColId,
                                })
                                movedCount++
                            } catch (err) {
                                console.warn(`Failed to move task ${taskId}:`, err)
                            }
                        }
                        result = { moved: movedCount, total: idsToMove.length, targetColumnId: targetColId }
                        break
                    }

                    // -----------------------------------------------------------------
                    // Project Tools
                    // -----------------------------------------------------------------
                    case "update_project": {
                        const projArgs = args as UpdateProjectArgs

                        // Backend requires Title and Description - use current values
                        if (!currentProject) {
                            throw new Error("Cannot update project: project data not loaded")
                        }

                        // Map tool values to project schema values
                        const statusMap: Record<string, string> = {
                            "on_hold": "archived",
                            // Other statuses map 1:1
                        }
                        const visibilityMap: Record<string, string> = {
                            "team_only": "members",
                            // Other visibilities map 1:1
                        }

                        const updateData: Record<string, unknown> = {
                            title: currentProject.title,
                            description: currentProject.description || "",
                            // Backend requires these bool fields
                            showcasePublished: currentProject.showcasePublished ?? false,
                            featured: currentProject.featured ?? false,
                        }

                        // Apply changes from AI
                        if (projArgs.description) {
                            updateData.description = projArgs.description
                        }
                        if (projArgs.shortDescription) {
                            updateData.detailedDescription = projArgs.shortDescription
                        }
                        if (projArgs.techStack && projArgs.techStack.length > 0) {
                            updateData.technologies = projArgs.techStack
                        }
                        if (projArgs.status) {
                            updateData.status = (statusMap[projArgs.status] || projArgs.status)
                        }
                        if (projArgs.visibility) {
                            updateData.visibility = (visibilityMap[projArgs.visibility] || projArgs.visibility)
                        }
                        if (projArgs.maxTeamSize) {
                            updateData.maxTeamSize = projArgs.maxTeamSize
                        }

                        await updateProjectMutation.mutateAsync({
                            id: projectId,
                            data: updateData,
                        })
                        result = { updated: true }
                        break
                    }

                    case "update_looking_for": {
                        // TODO: Implement when looking_for API is available
                        result = { skipped: true, reason: "Not implemented yet" }
                        break
                    }

                    // -----------------------------------------------------------------
                    // Content Tools
                    // -----------------------------------------------------------------
                    case "generate_news_draft": {
                        const newsArgs = args as unknown as GenerateNewsDraftArgs
                        // This tool returns content, not an action - the draft is in the AI's response
                        result = {
                            type: "content_generated",
                            newsType: newsArgs.type,
                            topic: newsArgs.topic,
                        }
                        break
                    }

                    // -----------------------------------------------------------------
                    // Query Tools (no mutation needed)
                    // -----------------------------------------------------------------
                    case "suggest_next_task": {
                        // Suggestions are returned in AI response, no action needed
                        result = { type: "suggestion" }
                        break
                    }

                    default:
                        throw new Error(`Unknown tool: ${toolName}`)
                }

                const successResult: ToolExecutionResult = {
                    toolCallId,
                    toolName,
                    success: true,
                    result,
                }
                onSuccess?.(successResult)
                return successResult

            } catch (error) {
                const errorResult: ToolExecutionResult = {
                    toolCallId,
                    toolName,
                    success: false,
                    error: error instanceof Error ? error.message : "Unknown error",
                }
                onError?.(errorResult)
                return errorResult
            }
        },
        [
            projectId,
            currentProject,
            currentTasks,
            currentColumns,
            createTaskMutation,
            updateTaskMutation,
            deleteTaskMutation,
            moveTaskMutation,
            updateProjectMutation,
            onSuccess,
            onError,
        ]
    )

    /**
     * Execute multiple tool calls sequentially
     */
    const executeToolCalls = useCallback(
        async (toolCalls: ToolCall[]): Promise<ToolExecutionResult[]> => {
            const results: ToolExecutionResult[] = []
            for (const toolCall of toolCalls) {
                const result = await executeToolCall(toolCall)
                results.push(result)
            }
            return results
        },
        [executeToolCall]
    )

    return {
        executeToolCall,
        executeToolCalls,
        isExecuting,
    }
}
