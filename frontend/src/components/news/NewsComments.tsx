"use client"

import { useState, useCallback } from "react"
import { useTranslations, useLocale } from "next-intl"
import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { MoreHorizontal, Pencil, Trash2, Send } from "lucide-react"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  useNewsComments,
  useCreateNewsComment,
  useUpdateNewsComment,
  useDeleteNewsComment,
  type NewsComment,
} from "@/lib/api/queries/news"
import { useProfile } from "@/lib/api/queries/profile"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"

interface CommentItemProps {
  comment: NewsComment
  currentUserId: string | undefined
  isEditing: boolean
  editContent: string
  canEditComment: boolean
  formattedTime: string
  isSavePending: boolean
  onStartEdit: (id: string, content: string) => void
  onDelete: (id: string) => void
  onEditContentChange: (e: React.ChangeEvent<HTMLTextAreaElement>) => void
  onEditKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void
  onCancelEdit: () => void
  onSaveEdit: () => void
  t: (key: string) => string
}

function CommentItem({
  comment,
  currentUserId,
  isEditing,
  editContent,
  canEditComment,
  formattedTime,
  isSavePending,
  onStartEdit,
  onDelete,
  onEditContentChange,
  onEditKeyDown,
  onCancelEdit,
  onSaveEdit,
  t,
}: CommentItemProps) {
  const handleStartEdit = useCallback(() => onStartEdit(comment.id, comment.content), [onStartEdit, comment.id, comment.content])
  const handleDelete = useCallback(() => onDelete(comment.id), [onDelete, comment.id])

  return (
    <div className="px-4 py-3 group/comment">
      {isEditing ? (
        <div className="space-y-2">
          <Textarea
            value={editContent}
            onChange={onEditContentChange}
            onKeyDown={onEditKeyDown}
            className="min-h-[60px] text-sm resize-none bg-muted/50"
            maxLength={2000}
            autoFocus
          />
          <div className="flex gap-2 justify-end">
            <Button variant="ghost" size="sm" className="h-7 text-xs" onClick={onCancelEdit}>
              {t("common.cancel")}
            </Button>
            <Button
              size="sm"
              className="h-7 text-xs"
              onClick={onSaveEdit}
              disabled={!editContent.trim() || isSavePending}
            >
              {t("common.save")}
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex gap-3">
          <Avatar className="h-7 w-7 shrink-0">
            {comment.authorAvatarUrl ? (
              <AvatarImage src={comment.authorAvatarUrl} alt={comment.authorName} />
            ) : (
              <AvatarFallback className="text-[10px] bg-muted">
                {(comment.authorName?.charAt(0) || "U").toUpperCase()}
              </AvatarFallback>
            )}
          </Avatar>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-foreground">{comment.authorName}</span>
              <span className="text-xs text-muted-foreground">{formattedTime}</span>
              {comment.updatedAt && (
                <span className="text-xs text-muted-foreground italic">{t("common.edited")}</span>
              )}
            </div>
            <p className="text-sm text-muted-foreground mt-0.5 whitespace-pre-wrap break-words">{comment.content}</p>
          </div>
          {currentUserId === comment.authorId && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-6 w-6 opacity-0 group-hover/comment:opacity-100 transition-opacity text-muted-foreground shrink-0"
                >
                  <MoreHorizontal className="h-3.5 w-3.5" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="bg-card border-border">
                {canEditComment && (
                  <DropdownMenuItem onClick={handleStartEdit} className="text-muted-foreground">
                    <Pencil className="h-3.5 w-3.5 mr-2" />
                    {t("news.editComment")}
                  </DropdownMenuItem>
                )}
                <DropdownMenuItem onClick={handleDelete} className="text-red-400">
                  <Trash2 className="h-3.5 w-3.5 mr-2" />
                  {t("news.deleteComment")}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
          {currentUserId && currentUserId !== comment.authorId && (
            <div className="opacity-0 group-hover/comment:opacity-100 transition-opacity shrink-0">
              <ReportDialog targetType={REPORT_TARGET_TYPES.NEWS_COMMENT} targetId={comment.id} />
            </div>
          )}
        </div>
      )}
    </div>
  )
}

interface NewsCommentsProps {
  projectId: string
  newsId: string
}

export function NewsComments({ projectId, newsId }: NewsCommentsProps) {
  const t = useTranslations()
  const locale = useLocale()
  const profileQuery = useProfile()
  const currentUserId = profileQuery.data?.id

  const { data: comments = [], isLoading } = useNewsComments(projectId, newsId)
  const createComment = useCreateNewsComment()
  const updateComment = useUpdateNewsComment()
  const deleteComment = useDeleteNewsComment()

  const [newComment, setNewComment] = useState("")
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editContent, setEditContent] = useState("")

  const handleSubmit = useCallback(() => {
    if (!newComment.trim()) return
    createComment.mutate(
      { projectId, newsId, content: newComment.trim() },
      { onSuccess: () => setNewComment("") }
    )
  }, [newComment, projectId, newsId, createComment])

  const handleStartEdit = useCallback((commentId: string, content: string) => {
    setEditingId(commentId)
    setEditContent(content)
  }, [])

  const handleSaveEdit = useCallback(() => {
    if (!editingId || !editContent.trim()) return
    updateComment.mutate(
      { projectId, newsId, commentId: editingId, content: editContent.trim() },
      { onSuccess: () => { setEditingId(null); setEditContent("") } }
    )
  }, [editingId, editContent, projectId, newsId, updateComment])

  const handleDelete = useCallback((commentId: string) => {
    deleteComment.mutate({ projectId, newsId, commentId })
  }, [projectId, newsId, deleteComment])

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSubmit()
    }
  }, [handleSubmit])

  const handleEditKeyDown = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSaveEdit()
    }
    if (e.key === "Escape") {
      setEditingId(null)
      setEditContent("")
    }
  }, [handleSaveEdit])

  const handleEditContentChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setEditContent(e.target.value), [])
  const handleNewCommentChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => setNewComment(e.target.value), [])
  const handleCancelEdit = useCallback(() => { setEditingId(null); setEditContent("") }, [])

  const canEdit = useCallback((createdAt: string) => {
    const created = new Date(createdAt)
    return (Date.now() - created.getTime()) < 60 * 60 * 1000
  }, [])

  const formatTime = useCallback((dateStr: string) => {
    try {
      return formatDistanceToNow(new Date(dateStr), {
        addSuffix: true,
        locale: getDateFnsLocale(locale),
      })
    } catch {
      return ""
    }
  }, [locale])

  return (
    <div className="border-t border-border">
      {/* Comments list */}
      <div className="max-h-80 overflow-y-auto">
        {isLoading ? (
          <div className="px-4 py-3 text-sm text-muted-foreground">{t("common.loading")}</div>
        ) : comments.length === 0 ? (
          <div className="px-4 py-4 text-sm text-muted-foreground text-center">{t("news.noComments")}</div>
        ) : (
          <div className="divide-y divide-border">
            {comments.map((comment) => (
              <CommentItem
                key={comment.id}
                comment={comment}
                currentUserId={currentUserId}
                isEditing={editingId === comment.id}
                editContent={editContent}
                canEditComment={canEdit(comment.createdAt)}
                formattedTime={formatTime(comment.createdAt)}
                isSavePending={updateComment.isPending}
                onStartEdit={handleStartEdit}
                onDelete={handleDelete}
                onEditContentChange={handleEditContentChange}
                onEditKeyDown={handleEditKeyDown}
                onCancelEdit={handleCancelEdit}
                onSaveEdit={handleSaveEdit}
                t={t}
              />
            ))}
          </div>
        )}
      </div>

      {/* New comment input */}
      {currentUserId && (
        <div className="px-4 py-3 border-t border-border">
          <div className="flex gap-2 items-end">
            <Textarea
              value={newComment}
              onChange={handleNewCommentChange}
              onKeyDown={handleKeyDown}
              placeholder={t("news.commentPlaceholder")}
              className="min-h-[36px] max-h-24 text-sm resize-none bg-muted/30 border-muted"
              maxLength={2000}
              rows={1}
            />
            <Button
              size="icon"
              className="h-9 w-9 shrink-0"
              onClick={handleSubmit}
              disabled={!newComment.trim() || createComment.isPending}
            >
              <Send className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
