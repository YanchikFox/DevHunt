"use client"

import { ChevronLeft, MoreVertical, Search } from "lucide-react"
import { Button } from "@/components/ui/button"

interface ChatHeaderProps {
  readonly title: string
  readonly statusText: string
  readonly avatarUrl?: string | null
  readonly isOnline: boolean
  readonly isNewChat: boolean
  readonly showBackButton?: boolean
  readonly onBack?: () => void
  readonly onClose?: () => void
}

export function ChatHeader({
  title,
  statusText,
  showBackButton = true,
  onBack,
}: ChatHeaderProps) {
  return (
    <div className="flex shrink-0 items-center gap-2 border-b border-border bg-background px-[18px] py-3">
      {showBackButton && onBack && (
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 rounded-[8px] text-muted-foreground hover:bg-bg-hover hover:text-foreground md:hidden"
          onClick={onBack}
          aria-label="Back"
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
      )}

      <div className="min-w-0 flex-1">
        <h3 className="truncate text-[14px] font-semibold leading-tight text-foreground">
          {title}
        </h3>
        {statusText && (
          <p className="truncate font-mono text-[10px] text-muted-foreground">
            {statusText}
          </p>
        )}
      </div>

      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-8 rounded-[8px] text-muted-foreground hover:bg-bg-hover hover:text-foreground"
        aria-label="Search"
      >
        <Search className="h-3.5 w-3.5" />
      </Button>
      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-8 rounded-[8px] text-muted-foreground hover:bg-bg-hover hover:text-foreground"
        aria-label="More"
      >
        <MoreVertical className="h-3.5 w-3.5" />
      </Button>
    </div>
  )
}
