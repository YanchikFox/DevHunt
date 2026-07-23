"use client"

import { useState, useCallback } from "react"
import {
  useAdminContentNews,
  useAdminDeleteNews,
  type AdminNewsPost,
} from "@/lib/api/queries/admin-content"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Loader2, Trash2, Heart, MessageSquare } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { CenteredLoader } from "@/components/ui/loading"
import { TableEmptyState } from "@/components/ui/empty-state"
import { DataTablePagination } from "@/components/ui/data-table-pagination"

export function NewsContent() {
  const t = useTranslations("admin")
  const { toast } = useToast()

  const [page, setPage] = useState(1)
  const [searchQuery, setSearchQuery] = useState("")
  const [deleteTarget, setDeleteTarget] = useState<AdminNewsPost | null>(null)

  const { data, isLoading } = useAdminContentNews(page, 20, {
    search: searchQuery || undefined,
  })

  const deleteNews = useAdminDeleteNews()

  const handleDelete = useCallback(async () => {
    if (!deleteTarget) return
    try {
      await deleteNews.mutateAsync(deleteTarget.id)
      toast({ title: t("newsDeleted") })
      setDeleteTarget(null)
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [deleteTarget, deleteNews, toast, t])

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value)
    setPage(1)
  }, [])

  const handlePrevPage = useCallback(() => setPage(p => p - 1), [])
  const handleNextPage = useCallback(() => setPage(p => p + 1), [])
  const handleCloseDeleteDialog = useCallback((open: boolean) => { if (!open) setDeleteTarget(null) }, [])
  const handleCancelDelete = useCallback(() => setDeleteTarget(null), [])

  const news = data?.data ?? []
  const pagination = data?.pagination

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex items-center gap-3">
        <Input
          placeholder={t("searchNews")}
          value={searchQuery}
          onChange={handleSearchChange}
          className="w-64"
        />
      </div>

      {/* Table */}
      {isLoading ? (
        <CenteredLoader />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("title")}</TableHead>
              <TableHead>{t("project")}</TableHead>
              <TableHead>{t("author")}</TableHead>
              <TableHead>{t("visibility")}</TableHead>
              <TableHead>{t("engagement")}</TableHead>
              <TableHead>{t("created")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {news.map((post) => (
              <NewsRow key={post.id} post={post} onDelete={setDeleteTarget} t={t} />
            ))}
            {news.length === 0 && (
              <TableEmptyState colSpan={7} description={t("noNews")} />
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
            <DialogTitle>{t("deleteNews")}</DialogTitle>
            <DialogDescription>
              {t("deleteNewsConfirm", { title: deleteTarget?.title ?? "" })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelDelete}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteNews.isPending}>
              {deleteNews.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("delete")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

interface NewsRowProps {
  post: AdminNewsPost
  onDelete: (p: AdminNewsPost) => void
  t: (key: string) => string
}

function NewsRow({ post, onDelete, t: _t }: NewsRowProps) {
  const handleDelete = useCallback(() => onDelete(post), [onDelete, post])

  return (
    <TableRow>
      <TableCell className="font-medium max-w-[200px] truncate">{post.title}</TableCell>
      <TableCell className="text-sm">{post.projectTitle || "—"}</TableCell>
      <TableCell className="text-sm">{post.authorName || "—"}</TableCell>
      <TableCell>
        <Badge variant="outline">{post.visibility}</Badge>
      </TableCell>
      <TableCell>
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <span className="flex items-center gap-1">
            <Heart className="h-3.5 w-3.5" /> {post.likesCount}
          </span>
          <span className="flex items-center gap-1">
            <MessageSquare className="h-3.5 w-3.5" /> {post.commentsCount}
          </span>
        </div>
      </TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {new Date(post.createdAt).toLocaleDateString()}
      </TableCell>
      <TableCell>
        <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleDelete}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      </TableCell>
    </TableRow>
  )
}
