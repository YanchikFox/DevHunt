"use client"

import { useDroppable } from "@dnd-kit/core"
import { cn } from "@/lib/utils"
import type { DroppableColumnProps } from "./types"

export function DroppableColumn({
    dropId,
    children,
    canEdit,
}: DroppableColumnProps) {
    const { setNodeRef, isOver } = useDroppable({
        id: dropId,
        disabled: !canEdit,
    })

    return (
        <div
            ref={setNodeRef}
            className={cn(
                "space-y-1.5 min-h-[60px] rounded-lg transition-colors",
                isOver && canEdit && "bg-primary/10 ring-1 ring-primary/25 ring-inset"
            )}
        >
            {children}
        </div>
    )
}
