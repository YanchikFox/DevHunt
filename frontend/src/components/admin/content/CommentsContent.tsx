"use client"

import { useState, useCallback } from "react"
import {
  useAdminContentComments,
  useAdminDeleteComment,
  type AdminComment,
} from "@/lib/api/queries/admin-content"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Trash2 } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { CenteredLoader } from "@/components/ui/loading"
import { TableEmptyState } from "@/components/ui/empty-state"
import { DataTablePagination } from "@/components/ui/data-table-pagination"

const typeColors: Record<string, string> = {
  showcase: "bg-purple-50 text-purple-700 border-purple-200",
  news: "bg-blue-50 text-blue-700 border-blue-200",
}

export function CommentsContent() {
  const t = useTranslations("admin")
  const { toast } = useToast()

  const [page, setPage] = useState(1)
  const [typeFilter, setTypeFilter] = useState("all")
  const [searchQuery, setSearchQuery] = useState("")
  const [deleteTarget, setDeleteTarget] = useState<AdminComment | null>(null)

  const { data, isLoading } = useAdminContentComments(page, 20, {
    type: typeFilter !== "all" ? typeFilter : undefined,
    search: searchQuery || undefined,
  })

  const deleteComment = useAdminDeleteComment()

  const handleDelete = useCallback(async () => {
    if (!deleteTarget) return
    try {
      await deleteComment.mutateAsync({ commentId: deleteTarget.id, type: deleteTarget.type })
      toast({ title: t("commentDeleted") })
      setDeleteTarget(null)
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [deleteTarget, deleteComment, toast, t])

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
    setPage(1)
  }, [])

  const handleTypeChange = useCallback((value: string) => {
    setTypeFilter(value)
    setPage(1)
  }, [])

  const handlePrevPage = useCallback(() => setPage(p => p - 1), [])
  const handleNextPage = useCallback(() => setPage(p => p + 1), [])
  const handleCloseDeleteDialog = useCallback((open: boolean) => { if (!open) setDeleteTarget(null) }, [])
  const handleCancelDelete = useCallback(() => setDeleteTarget(null), [])

  const comments = data?.data ?? []
  const pagination = data?.pagination

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex items-center gap-3">
        <Input
          placeholder={t("searchComments")}
          value={searchQuery}
          onChange={handleSearchChange}
          className="w-64"
        />
        <Select value={typeFilter} onValueChange={handleTypeChange}>
          <SelectTrigger className="w-40">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t("allTypes")}</SelectItem>
            <SelectItem value="showcase">{t("showcase")}</SelectItem>
            <SelectItem value="news">{t("news")}</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Table */}
      {isLoading ? (
        <CenteredLoader />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("author")}</TableHead>
              <TableHead>{t("type")}</TableHead>
              <TableHead className="w-[40%]">{t("content")}</TableHead>
              <TableHead>{t("date")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {comments.map((comment) => (
              <CommentRow key={`${comment.type}-${comment.id}`} comment={comment} onDelete={setDeleteTarget} t={t} />
            ))}
            {comments.length === 0 && (
              <TableEmptyState colSpan={5} description={t("noComments")} />
            )}
          </TableBody>
        </Table>
      )}

      {/* Pagination */}
      <DataTablePagination 
        page={page} 
        pagination={pagination} 
        onPrevPage={handlePrevPage} 
        onNextPage={handleNextPage} 
        t={t} 
      />

      {/* Delete Confirmation */}
      <Dialog open={deleteTarget !== null} onOpenChange={handleCloseDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("deleteComment")}</DialogTitle>
            <DialogDescription>{t("deleteCommentConfirm")}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelDelete}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteComment.isPending}>
              {deleteComment.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("delete")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

interface CommentRowProps {
  comment: AdminComment
  onDelete: (c: AdminComment) => void
  t: (key: string) => string
}

function CommentRow({ comment, onDelete, t: _t }: CommentRowProps) {
  const handleDelete = useCallback(() => onDelete(comment), [onDelete, comment])

  return (
    <TableRow>
      <TableCell className="text-sm font-medium">{comment.authorName}</TableCell>
      <TableCell>
        <Badge variant="outline" className={typeColors[comment.type] || ""}>
          {comment.type}
        </Badge>
      </TableCell>
      <TableCell className="text-sm max-w-[300px] truncate">{comment.content}</TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {new Date(comment.createdAt).toLocaleDateString()}
      </TableCell>
      <TableCell>
        <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleDelete}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      </TableCell>
    </TableRow>
  )
}
