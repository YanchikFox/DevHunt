"use client"

import { User } from "lucide-react"
import { Label } from "@/components/ui/label"
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select"
import type { Task, TeamMember } from "./types"

interface TaskAssigneeProps {
    readonly task: Task | null
    readonly teamMembers: TeamMember[]
    readonly canEdit: boolean
    readonly isSaving: boolean
    readonly handleAssigneeChange: (value: string) => Promise<void>
    readonly t: (key: string) => string
}

/**
 * Renders the task assignee selector.
 */
export function TaskAssignee({
    task,
    teamMembers,
    canEdit,
    isSaving,
    handleAssigneeChange,
    t,
}: TaskAssigneeProps) {
    return (
        <div className="space-y-1.5">
            <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                <User className="h-3 w-3" />
                {t("tasks.assignee")}
            </Label>
            {canEdit ? (
                <Select
                    value={task?.assigneeId || "unassigned"}
                    onValueChange={handleAssigneeChange}
                    disabled={isSaving}
                >
                    <SelectTrigger className="h-9">
                        <SelectValue placeholder={t("tasks.unassigned")} />
                    </SelectTrigger>
                    <SelectContent>
                        <SelectItem value="unassigned">{t("tasks.unassigned")}</SelectItem>
                        {teamMembers.map((member) => {
                            const id = member.userId || member.UserId || member.id || member.Id
                            const name = member.fullName || member.FullName || member.email || member.Email
                            if (!id) return null
                            return (
                                <SelectItem key={id} value={id}>
                                    {name}
                                </SelectItem>
                            )
                        })}
                    </SelectContent>
                </Select>
            ) : (
                <div className="flex items-center gap-2 h-9 px-3 border rounded-md bg-muted/30">
                    <span className="text-sm">
                        {task?.assigneeName || t("tasks.unassigned")}
                    </span>
                </div>
            )}
        </div>
    )
}
