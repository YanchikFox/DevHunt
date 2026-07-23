"use client"

import { useState, useCallback } from "react"
import {
  useAdminContentShowcase,
  useAdminDeleteShowcase,
  type AdminShowcaseEntry,
} from "@/lib/api/queries/admin-content"
import { Button } from "@/components/ui/button"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog"
import { Loader2, Trash2, MessageSquare } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"
import { CenteredLoader } from "@/components/ui/loading"
import { TableEmptyState } from "@/components/ui/empty-state"
import { DataTablePagination } from "@/components/ui/data-table-pagination"

export function ShowcaseContent() {
  const t = useTranslations("admin")
  const { toast } = useToast()

  const [page, setPage] = useState(1)
  const [deleteTarget, setDeleteTarget] = useState<AdminShowcaseEntry | null>(null)

  const { data, isLoading } = useAdminContentShowcase(page, 20)
  const deleteShowcase = useAdminDeleteShowcase()

  const handleDelete = useCallback(async () => {
    if (!deleteTarget) return
    try {
      await deleteShowcase.mutateAsync(deleteTarget.id)
      toast({ title: t("showcaseRemoved") })
      setDeleteTarget(null)
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [deleteTarget, deleteShowcase, toast, t])

  const handlePrevPage = useCallback(() => setPage(p => p - 1), [])
  const handleNextPage = useCallback(() => setPage(p => p + 1), [])
  const handleCloseDeleteDialog = useCallback((open: boolean) => { if (!open) setDeleteTarget(null) }, [])
  const handleCancelDelete = useCallback(() => setDeleteTarget(null), [])

  const entries = data?.data ?? []
  const pagination = data?.pagination

  return (
    <div className="space-y-4">
      {/* Table */}
      {isLoading ? (
        <CenteredLoader />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t("project")}</TableHead>
              <TableHead>{t("publishedAt")}</TableHead>
              <TableHead>{t("comments")}</TableHead>
              <TableHead>{t("actions")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.map((entry) => (
              <ShowcaseRow key={entry.id} entry={entry} onDelete={setDeleteTarget} t={t} />
            ))}
            {entries.length === 0 && (
              <TableEmptyState colSpan={4} description={t("noShowcase")} />
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
            <DialogTitle>{t("removeFromShowcase")}</DialogTitle>
            <DialogDescription>
              {t("removeShowcaseConfirm", { title: deleteTarget?.projectTitle ?? "" })}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={handleCancelDelete}>
              {t("cancel")}
            </Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleteShowcase.isPending}>
              {deleteShowcase.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
              {t("remove")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

interface ShowcaseRowProps {
  entry: AdminShowcaseEntry
  onDelete: (e: AdminShowcaseEntry) => void
  t: (key: string) => string
}

function ShowcaseRow({ entry, onDelete, t: _t }: ShowcaseRowProps) {
  const handleDelete = useCallback(() => onDelete(entry), [onDelete, entry])

  return (
    <TableRow>
      <TableCell className="font-medium">{entry.projectTitle || entry.projectId.slice(0, 8) + "..."}</TableCell>
      <TableCell className="text-sm text-muted-foreground">
        {new Date(entry.publishedAt).toLocaleDateString()}
      </TableCell>
      <TableCell>
        <span className="flex items-center gap-1 text-sm text-muted-foreground">
          <MessageSquare className="h-3.5 w-3.5" /> {entry.commentsCount}
        </span>
      </TableCell>
      <TableCell>
        <Button variant="ghost" size="icon" className="h-8 w-8" onClick={handleDelete}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      </TableCell>
    </TableRow>
  )
}
