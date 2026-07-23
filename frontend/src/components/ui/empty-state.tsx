import * as React from "react"
import { cn } from "@/lib/utils"
import { TableRow, TableCell } from "@/components/ui/table"

export interface EmptyStateProps extends React.HTMLAttributes<HTMLDivElement> {
  title?: string
  description?: string
  icon?: React.ReactNode
  action?: React.ReactNode
}

/**
 * A generic Empty State component for lists, cards, and sections.
 */
export function EmptyState({
  title,
  description,
  icon,
  action,
  className,
  children,
  ...props
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center py-12 px-4 text-center",
        className
      )}
      {...props}
    >
      {icon && <div className="mb-4 text-muted-foreground/60">{icon}</div>}
      {title && <h3 className="text-lg font-medium text-foreground">{title}</h3>}
      {description && (
        <p className="mt-2 text-sm text-muted-foreground max-w-sm mx-auto">
          {description}
        </p>
      )}
      {children && !description && (
        <div className="mt-2 text-sm text-muted-foreground max-w-sm mx-auto">
          {children}
        </div>
      )}
      {action && <div className="mt-6">{action}</div>}
    </div>
  )
}

interface TableEmptyStateProps extends EmptyStateProps {
  colSpan: number
}

/**
 * Empty State designed specifically for Data Tables.
 * Must be rendered directly inside a `<TableBody>`.
 */
export function TableEmptyState({ colSpan, children, ...props }: TableEmptyStateProps) {
  return (
    <TableRow>
      <TableCell colSpan={colSpan} className="h-32 text-center align-middle hover:bg-transparent">
        {props.title || props.description || props.icon ? (
          <EmptyState {...props} className={cn("py-8", props.className)} />
        ) : (
          <span className={cn("text-muted-foreground", props.className)}>
            {children || "No items found."}
          </span>
        )}
      </TableCell>
    </TableRow>
  )
}
