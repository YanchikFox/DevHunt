import { Button } from "@/components/ui/button"

interface PaginationMetadata {
  total: number
  totalPages: number
}

interface DataTablePaginationProps {
  page: number
  pageSize?: number
  pagination: PaginationMetadata | null | undefined
  onNextPage: () => void
  onPrevPage: () => void
  t: (key: string) => string
}

/**
 * Standardized Database Pagination Controls
 */
export function DataTablePagination({
  page,
  pageSize = 20,
  pagination,
  onNextPage,
  onPrevPage,
  t
}: DataTablePaginationProps) {
  if (!pagination || pagination.totalPages <= 1) {
    return null
  }

  return (
    <div className="flex flex-col sm:flex-row items-center justify-between gap-4 mt-4">
      <span className="text-sm text-muted-foreground">
        {t("showing")} {(page - 1) * pageSize + 1}-
        {Math.min(page * pageSize, pagination.total)} {t("of")} {pagination.total}
      </span>
      <div className="flex gap-2">
        <Button
          size="sm"
          variant="outline"
          disabled={page <= 1}
          onClick={onPrevPage}
        >
          {t("prev") ?? "Previous"}
        </Button>
        <Button
          size="sm"
          variant="outline"
          disabled={page >= pagination.totalPages}
          onClick={onNextPage}
        >
          {t("next") ?? "Next"}
        </Button>
      </div>
    </div>
  )
}
