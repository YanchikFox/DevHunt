"use client"

import { useState, useCallback } from "react"
import { useTranslations, useLocale } from "next-intl"
import { formatDistanceToNow } from "date-fns"
import { getDateFnsLocale } from "@/i18n/locale-utils"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Button } from "@/components/ui/button"
import { Textarea } from "@/components/ui/textarea"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { MessageCircle, MoreHorizontal, Pencil, Trash2, Send, Reply, ChevronDown, ChevronUp } from "lucide-react"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import {
  useShowcaseComments,
  useCreateShowcaseComment,
  useUpdateShowcaseComment,
  useDeleteShowcaseComment,
  type ShowcaseComment,
} from "@/lib/api/queries/showcase"
import { useProfile } from "@/lib/api/queries/profile"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"

interface ShowcaseCommentsProps {
  projectId: string
}

interface CommentItemProps {
  comment: ShowcaseComment
  projectId: string
  currentUserId: string | undefined
  isReply?: boolean
  onReply: (commentId: string) => void
  editingId: string | null
  editContent: string
  onStartEdit: (commentId: string, content: string) => void
  onSaveEdit: () => void
  onCancelEdit: () => void
  onEditContentChange: (value: string) => void
  onDelete: (commentId: string) => void
  isUpdatePending: boolean
  formatTime: (dateStr: string) => string
}

function CommentItem({
  comment,
  currentUserId,
  isReply = false,
  onReply,
  editingId,
  editContent,
  onStartEdit,
  onSaveEdit,
  onCancelEdit,
  onEditContentChange,
  onDelete,
  isUpdatePending,
  formatTime,
}: Readonly<CommentItemProps>) {
  const t = useTranslations()

  const handleEditKeyDown = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      onSaveEdit()
    }
    if (e.key === "Escape") {
      onCancelEdit()
    }
  }, [onSaveEdit, onCancelEdit])

  const handleStartEdit = useCallback(() => {
    onStartEdit(comment.id, comment.content)
  }, [comment.id, comment.content, onStartEdit])

  const handleDelete = useCallback(() => {
    onDelete(comment.id)
  }, [comment.id, onDelete])

  const handleReply = useCallback(() => {
    onReply(comment.id)
  }, [comment.id, onReply])

  const handleEditChange = useCallback((e: { target: { value: string } }) => onEditContentChange(e.target.value), [onEditContentChange])

  const isEditing = editingId === comment.id

  return (
    <div className={`group/comment ${isReply ? "ml-10 border-l-2 border-muted pl-4" : ""}`}>
      {isEditing ? (
        <div className="space-y-2 py-3">
          <Textarea
            value={editContent}
            onChange={handleEditChange}
            onKeyDown={handleEditKeyDown}
            className="min-h-[60px] text-sm resize-none bg-muted/50"
            maxLength={2000}
            autoFocus
          />
          <div className="flex gap-2 justify-end">
            <Button
              variant="ghost"
              size="sm"
              className="h-7 text-xs"
              onClick={onCancelEdit}
            >
              {t("common.cancel")}
            </Button>
            <Button
              size="sm"
              className="h-7 text-xs"
              onClick={onSaveEdit}
              disabled={!editContent.trim() || isUpdatePending}
            >
              {t("common.save")}
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex gap-3 py-3">
          <Avatar className="h-8 w-8 shrink-0">
            {comment.author.avatarUrl ? (
              <AvatarImage src={comment.author.avatarUrl} alt={comment.author.fullName} />
            ) : (
              <AvatarFallback className="text-xs bg-muted">
                {comment.author.fullName.substring(0, 2).toUpperCase()}
              </AvatarFallback>
            )}
          </Avatar>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium">{comment.author.fullName}</span>
              <span className="text-xs text-muted-foreground">{formatTime(comment.createdAt)}</span>
              {comment.isEdited && (
                <span className="text-xs text-muted-foreground italic">{t("common.edited")}</span>
              )}
            </div>
            <p className="text-sm text-muted-foreground mt-1 whitespace-pre-wrap break-words">
              {comment.content}
            </p>
            {!isReply && currentUserId && (
              <Button
                variant="ghost"
                size="sm"
                className="h-6 text-xs text-muted-foreground mt-1 -ml-2 gap-1"
                onClick={handleReply}
              >
                <Reply className="h-3 w-3" />
                {t("showcase.reply")}
              </Button>
            )}
          </div>
          {currentUserId === comment.author.id && (
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
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={handleStartEdit}>
                  <Pencil className="h-3.5 w-3.5 mr-2" />
                  {t("showcase.editComment")}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleDelete} className="text-red-500">
                  <Trash2 className="h-3.5 w-3.5 mr-2" />
                  {t("showcase.deleteComment")}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )}
          {currentUserId && currentUserId !== comment.author.id && (
            <div className="opacity-0 group-hover/comment:opacity-100 transition-opacity shrink-0">
              <ReportDialog targetType={REPORT_TARGET_TYPES.SHOWCASE_COMMENT} targetId={comment.id} />
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function formatCommentTime(dateStr: string, locale: string): string {
  try {
    return formatDistanceToNow(new Date(dateStr), { addSuffix: true, locale: getDateFnsLocale(locale) })
  } catch {
    return ""
  }
}

function NewCommentInput({ value, onChange, onKeyDown, onSubmit, placeholder, isPending }: {
  value: string; onChange: (e: { target: { value: string } }) => void
  onKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void
  onSubmit: () => void; placeholder: string; isPending: boolean
}) {
  return (
    <div className="flex gap-2 items-end mb-6">
      <Textarea value={value} onChange={onChange} onKeyDown={onKeyDown} placeholder={placeholder}
        className="min-h-[40px] max-h-24 text-sm resize-none" maxLength={2000} rows={1} />
      <Button size="icon" className="h-10 w-10 shrink-0" onClick={onSubmit} disabled={!value.trim() || isPending}>
        <Send className="h-4 w-4" />
      </Button>
    </div>
  )
}

export function ShowcaseComments({ projectId }: Readonly<ShowcaseCommentsProps>) {
  const t = useTranslations()
  const locale = useLocale()
  const profileQuery = useProfile()
  const currentUserId = profileQuery.data?.id

  const { data: commentsResponse, isLoading } = useShowcaseComments(projectId)
  const comments = commentsResponse?.data ?? []
  const totalCount = commentsResponse?.totalCount ?? 0

  const createComment = useCreateShowcaseComment()
  const updateComment = useUpdateShowcaseComment()
  const deleteComment = useDeleteShowcaseComment()

  const [newComment, setNewComment] = useState("")
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editContent, setEditContent] = useState("")
  const [replyingTo, setReplyingTo] = useState<string | null>(null)
  const [replyContent, setReplyContent] = useState("")
  const [expandedReplies, setExpandedReplies] = useState<Set<string>>(new Set())

  const handleSubmit = useCallback(() => {
    if (!newComment.trim()) return
    createComment.mutate(
      { projectId, content: newComment.trim() },
      { onSuccess: () => setNewComment("") }
    )
  }, [newComment, projectId, createComment])

  const handleReplySubmit = useCallback(() => {
    if (!replyContent.trim() || !replyingTo) return
    createComment.mutate(
      { projectId, content: replyContent.trim(), parentCommentId: replyingTo },
      {
        onSuccess: () => {
          setReplyContent("")
          setReplyingTo(null)
          setExpandedReplies((prev) => new Set(prev).add(replyingTo))
        },
      }
    )
  }, [replyContent, replyingTo, projectId, createComment])

  const handleStartEdit = useCallback((commentId: string, content: string) => {
    setEditingId(commentId)
    setEditContent(content)
  }, [])

  const handleSaveEdit = useCallback(() => {
    if (!editingId || !editContent.trim()) return
    updateComment.mutate(
      { projectId, commentId: editingId, content: editContent.trim() },
      {
        onSuccess: () => {
          setEditingId(null)
          setEditContent("")
        },
      }
    )
  }, [editingId, editContent, projectId, updateComment])

  const handleCancelEdit = useCallback(() => {
    setEditingId(null)
    setEditContent("")
  }, [])

  const handleEditContentChange = useCallback((value: string) => {
    setEditContent(value)
  }, [])

  const handleDelete = useCallback((commentId: string) => {
    deleteComment.mutate({ projectId, commentId })
  }, [projectId, deleteComment])

  const handleReply = useCallback((commentId: string) => {
    setReplyingTo(commentId)
    setReplyContent("")
  }, [])

  const handleCancelReply = useCallback(() => {
    setReplyingTo(null)
    setReplyContent("")
  }, [])

  const toggleReplies = useCallback((commentId: string) => {
    setExpandedReplies((prev) => {
      const next = new Set(prev)
      if (next.has(commentId)) {
        next.delete(commentId)
      } else {
        next.add(commentId)
      }
      return next
    })
  }, [])

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSubmit()
    }
  }, [handleSubmit])

  const handleReplyKeyDown = useCallback((e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleReplySubmit()
    }
    if (e.key === "Escape") {
      handleCancelReply()
    }
  }, [handleReplySubmit, handleCancelReply])

  const handleNewCommentChange = useCallback((e: { target: { value: string } }) => setNewComment(e.target.value), [])

  const formatTime = useCallback((dateStr: string) => formatCommentTime(dateStr, locale), [locale])

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <MessageCircle className="h-5 w-5" />
          {t("showcase.comments")} ({totalCount})
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-0">
        {currentUserId ? (
          <NewCommentInput value={newComment} onChange={handleNewCommentChange} onKeyDown={handleKeyDown} onSubmit={handleSubmit} placeholder={t("showcase.commentPlaceholder")} isPending={createComment.isPending} />
        ) : (
          <p className="text-sm text-muted-foreground mb-4">{t("showcase.loginToComment")}</p>
        )}

        {/* Comments list */}
        <CommentsListContent
          isLoading={isLoading}
          comments={comments}
          projectId={projectId}
          currentUserId={currentUserId}
          replyingTo={replyingTo}
          replyContent={replyContent}
          expandedReplies={expandedReplies}
          editingId={editingId}
          editContent={editContent}
          onReply={handleReply}
          onStartEdit={handleStartEdit}
          onSaveEdit={handleSaveEdit}
          onCancelEdit={handleCancelEdit}
          onEditContentChange={handleEditContentChange}
          onDelete={handleDelete}
          onReplyContentChange={setReplyContent}
          onReplyKeyDown={handleReplyKeyDown}
          onReplySubmit={handleReplySubmit}
          onCancelReply={handleCancelReply}
          onToggleReplies={toggleReplies}
          isUpdatePending={updateComment.isPending}
          isCreatePending={createComment.isPending}
          formatTime={formatTime}
        />
      </CardContent>
    </Card>
  )
}

interface CommentInteractionProps {
  projectId: string
  currentUserId: string | undefined
  replyingTo: string | null
  replyContent: string
  expandedReplies: Set<string>
  editingId: string | null
  editContent: string
  onReply: (commentId: string) => void
  onStartEdit: (commentId: string, content: string) => void
  onSaveEdit: () => void
  onCancelEdit: () => void
  onEditContentChange: (value: string) => void
  onDelete: (commentId: string) => void
  onReplyContentChange: (value: string) => void
  onReplyKeyDown: (e: React.KeyboardEvent<HTMLTextAreaElement>) => void
  onReplySubmit: () => void
  onCancelReply: () => void
  onToggleReplies: (commentId: string) => void
  isUpdatePending: boolean
  isCreatePending: boolean
  formatTime: (dateStr: string) => string
}

interface CommentThreadProps extends CommentInteractionProps {
  comment: ShowcaseComment
}

function CommentThread({
  comment, projectId, currentUserId, replyingTo, replyContent, expandedReplies,
  editingId, editContent, onReply, onStartEdit, onSaveEdit, onCancelEdit,
  onEditContentChange, onDelete, onReplyContentChange, onReplyKeyDown,
  onReplySubmit, onCancelReply, onToggleReplies, isUpdatePending, isCreatePending, formatTime,
}: Readonly<CommentThreadProps>) {
  const t = useTranslations()
  const replies = comment.replies ?? []
  const hasReplies = replies.length > 0
  const isExpanded = expandedReplies.has(comment.id)

  const handleToggleReplies = useCallback(() => onToggleReplies(comment.id), [onToggleReplies, comment.id])
  const handleReplyContentChange = useCallback((e: { target: { value: string } }) => onReplyContentChange(e.target.value), [onReplyContentChange])

  const sharedItemProps = { projectId, currentUserId, onReply, editingId, editContent, onStartEdit, onSaveEdit, onCancelEdit, onEditContentChange, onDelete, isUpdatePending, formatTime }

  return (
    <div>
      <CommentItem comment={comment} {...sharedItemProps} />

      {replyingTo === comment.id && (
        <div className="ml-10 pl-4 border-l-2 border-primary/30 pb-3">
          <div className="flex gap-2 items-end">
            <Textarea
              value={replyContent}
              onChange={handleReplyContentChange}
              onKeyDown={onReplyKeyDown}
              placeholder={t("showcase.replyPlaceholder")}
              className="min-h-[36px] max-h-20 text-sm resize-none"
              maxLength={2000}
              rows={1}
              autoFocus
            />
            <Button size="icon" className="h-9 w-9 shrink-0" onClick={onReplySubmit} disabled={!replyContent.trim() || isCreatePending}>
              <Send className="h-3.5 w-3.5" />
            </Button>
            <Button variant="ghost" size="icon" className="h-9 w-9 shrink-0" onClick={onCancelReply}>✕</Button>
          </div>
        </div>
      )}

      {hasReplies && (
        <div className="ml-10">
          <Button
            variant="ghost"
            size="sm"
            className="h-7 text-xs text-muted-foreground gap-1 -ml-2"
            onClick={handleToggleReplies}
          >
            {isExpanded ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
            {replies.length} {replies.length === 1 ? t("showcase.replyCount") : t("showcase.repliesCount")}
          </Button>

          {isExpanded && (
            <div className="space-y-0">
              {replies.map((reply) => (
                <CommentItem
                  key={reply.id}
                  comment={reply}
                  isReply
                  {...sharedItemProps}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}

interface CommentsListContentProps extends CommentInteractionProps {
  isLoading: boolean
  comments: ShowcaseComment[]
}

function CommentsListContent({
  isLoading,
  comments,
  projectId,
  currentUserId,
  replyingTo,
  replyContent,
  expandedReplies,
  editingId,
  editContent,
  onReply,
  onStartEdit,
  onSaveEdit,
  onCancelEdit,
  onEditContentChange,
  onDelete,
  onReplyContentChange,
  onReplyKeyDown,
  onReplySubmit,
  onCancelReply,
  onToggleReplies,
  isUpdatePending,
  isCreatePending,
  formatTime,
}: Readonly<CommentsListContentProps>) {
  const t = useTranslations()

  if (isLoading) {
    return (
      <div className="text-sm text-muted-foreground text-center py-6">{t("common.loading")}</div>
    )
  }

  if (comments.length === 0) {
    return (
      <div className="text-sm text-muted-foreground text-center py-6">
        {t("showcase.noComments")}
      </div>
    )
  }

  return (
    <div className="divide-y divide-border">
      {comments.map((comment) => (
        <CommentThread
          key={comment.id}
          comment={comment}
          projectId={projectId}
          currentUserId={currentUserId}
          replyingTo={replyingTo}
          replyContent={replyContent}
          expandedReplies={expandedReplies}
          editingId={editingId}
          editContent={editContent}
          onReply={onReply}
          onStartEdit={onStartEdit}
          onSaveEdit={onSaveEdit}
          onCancelEdit={onCancelEdit}
          onEditContentChange={onEditContentChange}
          onDelete={onDelete}
          onReplyContentChange={onReplyContentChange}
          onReplyKeyDown={onReplyKeyDown}
          onReplySubmit={onReplySubmit}
          onCancelReply={onCancelReply}
          onToggleReplies={onToggleReplies}
          isUpdatePending={isUpdatePending}
          isCreatePending={isCreatePending}
          formatTime={formatTime}
        />
      ))}
    </div>
  )
}
