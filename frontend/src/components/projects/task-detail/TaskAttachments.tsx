"use client"

import { useCallback } from "react"
import { Paperclip, Download, FileText, Image as ImageIcon, File, Trash2 } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import type { Attachment } from "./types"

interface TaskAttachmentsProps {
    readonly projectId: string
    readonly taskId: string
    readonly attachments: Attachment[]
    readonly attachmentsLoading?: boolean
    readonly canEdit: boolean
    readonly isUploading?: boolean
    readonly fileInputRef: React.RefObject<HTMLInputElement | null>
    readonly handleFileSelect: (e: React.ChangeEvent<HTMLInputElement>) => void
    readonly onDeleteAttachment?: (attachmentId: string) => void
    readonly t: (key: string) => string
}

// Helper to get file icon based on content type
function FileTypeIcon({ contentType }: { readonly contentType?: string }) {
    if (contentType?.startsWith("image/")) {
        return <ImageIcon className="h-5 w-5 text-blue-500" />
    }
    if (contentType === "application/pdf") {
        return <FileText className="h-5 w-5 text-red-500" />
    }
    return <File className="h-5 w-5 text-muted-foreground" />
}

// Helper component for attachments list state
function AttachmentsContent({
    isLoading,
    attachments,
    emptyMessage,
    loadingMessage,
    children,
}: {
    readonly isLoading?: boolean
    readonly attachments: Attachment[]
    readonly emptyMessage: string
    readonly loadingMessage: string
    readonly children: React.ReactNode
}) {
    if (isLoading) {
        return <p className="text-sm text-muted-foreground">{loadingMessage}</p>
    }
    if (attachments.length === 0) {
        return (
            <div className="p-4 border border-dashed rounded-lg text-center text-sm text-muted-foreground">
                {emptyMessage}
            </div>
        )
    }
    return <>{children}</>
}

interface AttachmentItemProps {
    readonly att: Attachment
    readonly projectId: string
    readonly taskId: string
    readonly canEdit: boolean
    readonly onDeleteAttachment?: (attachmentId: string) => void
}

function AttachmentItem({ att, projectId, taskId, canEdit, onDeleteAttachment }: AttachmentItemProps) {
    const downloadUrl = `/api/proxy-core/projects/${projectId}/tasks/${taskId}/attachments/${att.id}`
    const handleDelete = useCallback(() => onDeleteAttachment?.(att.id), [onDeleteAttachment, att.id])

    return (
        <div className="flex items-center gap-3 rounded-lg border p-2.5 text-sm group hover:bg-muted/50 transition-colors">
            <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-muted flex items-center justify-center">
                <FileTypeIcon contentType={att.contentType} />
            </div>
            <div className="flex-1 min-w-0">
                <a
                    href={downloadUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="font-medium hover:underline truncate block"
                >
                    {att.fileName}
                </a>
                <span className="text-xs text-muted-foreground">
                    {att.fileSize && `${(att.fileSize / 1024).toFixed(0)} KB | `}
                    {new Date(att.attachedAt).toLocaleDateString()}
                </span>
            </div>
            <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                <Button
                    type="button"
                    size="sm"
                    variant="ghost"
                    className="h-8 w-8 p-0"
                    asChild
                >
                    <a href={downloadUrl} download={att.fileName}>
                        <Download className="h-4 w-4" />
                    </a>
                </Button>
                {canEdit && onDeleteAttachment && (
                    <Button
                        type="button"
                        size="sm"
                        variant="ghost"
                        className="h-8 w-8 p-0 text-destructive hover:text-destructive"
                        onClick={handleDelete}
                    >
                        <Trash2 className="h-4 w-4" />
                    </Button>
                )}
            </div>
        </div>
    )
}

/**
 * Renders the task attachments section.
 */
export function TaskAttachments({
    projectId,
    taskId,
    attachments,
    attachmentsLoading,
    canEdit,
    isUploading,
    fileInputRef,
    handleFileSelect,
    onDeleteAttachment,
    t,
}: TaskAttachmentsProps) {
    const handleUploadClick = useCallback(() => fileInputRef.current?.click(), [fileInputRef])

    return (
        <div className="space-y-3">
            <div className="flex items-center justify-between">
                <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                    <Paperclip className="h-3 w-3" />
                    {t("tasks.attachments")}
                    {attachments.length > 0 && (
                        <Badge variant="secondary" className="ml-1 text-[10px] h-4 px-1">
                            {attachments.length}
                        </Badge>
                    )}
                </Label>
                {canEdit && (
                    <>
                        <input
                            ref={fileInputRef}
                            type="file"
                            className="hidden"
                            onChange={handleFileSelect}
                        />
                        <Button
                            size="sm"
                            variant="outline"
                            className="h-7 text-xs"
                            onClick={handleUploadClick}
                            disabled={isUploading}
                        >
                            {isUploading ? t("common.uploading") : t("common.upload")}
                        </Button>
                    </>
                )}
            </div>

            <AttachmentsContent
                isLoading={attachmentsLoading}
                attachments={attachments}
                loadingMessage={t("common.loading")}
                emptyMessage={t("tasks.noAttachments")}
            >
                <div className="space-y-2">
                    {attachments.map((att) => (
                        <AttachmentItem
                            key={att.id}
                            att={att}
                            projectId={projectId}
                            taskId={taskId}
                            canEdit={canEdit}
                            onDeleteAttachment={onDeleteAttachment}
                        />
                    ))}
                </div>
            </AttachmentsContent>
        </div>
    )
}
