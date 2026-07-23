"use client"

import { useCallback } from "react"
import { Button } from "@/components/ui/button"
import { Badge } from "@/components/ui/badge"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuCheckboxItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Filter } from "lucide-react"
import { cn } from "@/lib/utils"
import type { useTranslations } from "next-intl"

export interface FilterDropdownProps<T extends string> {
  readonly filter: T[]
  readonly setFilter: (filter: T[]) => void
  readonly items: T[]
  readonly getLabel: (item: T) => string
  readonly getColor?: (item: T) => string
  readonly title: string
  readonly filterLabel: string
  readonly contentWidth?: string
  readonly t: ReturnType<typeof useTranslations>
}

function FilterCheckboxItem<T extends string>({ item, checked, onToggle, getColor, getLabel }: {
  item: T
  checked: boolean
  onToggle: (item: T, checked: boolean) => void
  getColor?: (item: T) => string
  getLabel: (item: T) => string
}) {
  const handleChange = useCallback((c: boolean) => onToggle(item, c), [onToggle, item])
  return (
    <DropdownMenuCheckboxItem checked={checked} onCheckedChange={handleChange}>
      {getColor ? (
        <div className="flex items-center gap-2">
          <div className={cn("w-2 h-2 rounded-full", getColor(item))} />
          {getLabel(item)}
        </div>
      ) : (
        getLabel(item)
      )}
    </DropdownMenuCheckboxItem>
  )
}

export function FilterDropdown<T extends string>({
  filter,
  setFilter,
  items,
  getLabel,
  getColor,
  title,
  filterLabel,
  contentWidth = "w-40",
  t,
}: FilterDropdownProps<T>) {
  const handleClearFilter = useCallback(() => setFilter([]), [setFilter])
  const toggleItem = useCallback((item: T, checked: boolean) => {
    setFilter(checked ? [...filter, item] : filter.filter((i) => i !== item))
  }, [filter, setFilter])

  if (items.length === 0) return null

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="sm"
          className={cn(
            "h-7 gap-1.5 px-2 text-[12px] font-normal text-muted-foreground hover:text-foreground rounded-md",
            filter.length > 0 && "bg-muted/60 text-foreground"
          )}
        >
          <Filter className="h-3 w-3" />
          {title}
          {filter.length > 0 && (
            <Badge variant="secondary" className="ml-0.5 h-4 px-1 text-[9px] tabular-nums">
              {filter.length}
            </Badge>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className={contentWidth}>
        <DropdownMenuLabel className="text-xs">{filterLabel}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {items.map((item) => (
          <FilterCheckboxItem
            key={item}
            item={item}
            checked={filter.includes(item)}
            onToggle={toggleItem}
            getColor={getColor}
            getLabel={getLabel}
          />
        ))}
        {filter.length > 0 && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleClearFilter}>
              {t("common.clearFilters") ?? "Clear filters"}
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
