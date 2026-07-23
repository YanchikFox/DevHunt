"use client"

import { memo, useMemo, useCallback } from "react"
import { useLocale, useTranslations } from "next-intl"
import { Check, X, AlertTriangle, Loader2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/ui/dialog"
import { getToolLabel, getToolIcon, formatToolArgsForDisplay, type ToolCall } from "@/lib/api/agent-types";
import { getToolDetails, type ToolDetail } from "@/lib/api/agent-tool-details";

interface ToolConfirmDialogProps {
    open: boolean
    toolCalls: ToolCall[]
    isExecuting?: boolean
    onConfirm: () => void
    onCancel: () => void
}

/**
 * Renders structured key-value details for a tool call
 */
function ToolDetailsSection({ details }: Readonly<{ details: ToolDetail[] }>) {
    if (details.length === 0) return null

    return (
        <div className="mt-2 space-y-1 border-t border-border/40 pt-2">
            {details.map((d, i) => (
                <div key={`${d.label}-${i}`} className="text-xs flex gap-1.5">
                    <span className="text-muted-foreground shrink-0">
                        {d.label === "•" ? "•" : `${d.label}:`}
                    </span>
                    <span className="text-foreground break-words min-w-0">{d.value}</span>
                </div>
            ))}
        </div>
    )
}

/**
 * Dialog to confirm AI tool calls before execution.
 *
 * Shows what actions the AI wants to perform and requires user confirmation.
 * This is the "human-in-the-loop" for agentic AI.
 */
export const ToolConfirmDialog = memo(function ToolConfirmDialog({
    open,
    toolCalls,
    isExecuting = false,
    onConfirm,
    onCancel,
}: ToolConfirmDialogProps) {
    const locale = useLocale()
    const t = useTranslations()

    const toolDisplayList = useMemo(() => {
        return toolCalls.map((tc) => {
            const args = tc.function.arguments_parsed || {}
            return {
                id: tc.id,
                name: tc.function.name,
                icon: getToolIcon(tc.function.name),
                label: getToolLabel(tc.function.name, locale),
                description: formatToolArgsForDisplay(tc.function.name, args, locale),
                details: getToolDetails(tc.function.name, args, locale),
                args,
            }
        })
    }, [toolCalls, locale])

    const hasDestructiveAction = useMemo(() => {
        return toolCalls.some((tc) => tc.function.name === "delete_task")
    }, [toolCalls])

    const handleOpenChange = useCallback((isOpen: boolean) => { if (!isOpen) onCancel() }, [onCancel])

    return (
        <Dialog open={open} onOpenChange={handleOpenChange}>
            <DialogContent className="sm:max-w-md">
                <DialogHeader>
                    <DialogTitle className="flex items-center gap-2">
                        {hasDestructiveAction && (
                            <AlertTriangle className="h-5 w-5 text-amber-500" />
                        )}
                        {t("ai.wantsToPerformActions")}
                    </DialogTitle>
                    <DialogDescription>
                        {t("ai.reviewActions")}
                    </DialogDescription>
                </DialogHeader>

                <div className="space-y-3 py-4 max-h-[400px] overflow-y-auto">
                    {toolDisplayList.map((tool) => (
                        <div
                            key={tool.id}
                            className="flex items-start gap-3 p-3 bg-muted/50 rounded-lg"
                        >
                            <span className="text-xl shrink-0">{tool.icon}</span>
                            <div className="min-w-0 flex-1">
                                <div className="font-medium text-sm">{tool.label}</div>
                                <div className="text-xs text-muted-foreground">
                                    {tool.description}
                                </div>
                                <ToolDetailsSection details={tool.details} />
                            </div>
                        </div>
                    ))}
                </div>

                {hasDestructiveAction && (
                    <div className="flex items-center gap-2 px-3 py-2 bg-amber-50 dark:bg-amber-950/30 rounded-lg text-amber-700 dark:text-amber-300 text-xs">
                        <AlertTriangle className="h-4 w-4 shrink-0" />
                        {t("ai.warningDeleteData")}
                    </div>
                )}

                <DialogFooter className="gap-2 sm:gap-0">
                    <Button
                        variant="outline"
                        onClick={onCancel}
                        disabled={isExecuting}
                    >
                        <X className="h-4 w-4 mr-1.5" />
                        {t("common.cancel")}
                    </Button>
                    <Button
                        onClick={onConfirm}
                        disabled={isExecuting}
                        className="bg-primary text-primary-foreground hover:bg-primary/90"
                    >
                        {isExecuting ? (
                            <>
                                <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />
                                {t("ai.executing")}
                            </>
                        ) : (
                            <>
                                <Check className="h-4 w-4 mr-1.5" />
                                {t("common.confirm")}
                            </>
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
})
