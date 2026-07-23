import { cn } from "@/lib/utils"

interface OnlineIndicatorProps {
  isOnline: boolean
  size?: "sm" | "md" | "lg"
  className?: string
}

const sizeClasses = {
  sm: "h-2.5 w-2.5",
  md: "h-3.5 w-3.5",
  lg: "h-5 w-5",
} as const

const borderClasses = {
  sm: "border-[1.5px]",
  md: "border-2",
  lg: "border-[3px]",
} as const

export function OnlineIndicator({ isOnline, size = "sm", className }: OnlineIndicatorProps) {
  if (!isOnline) return null

  return (
    <span
      className={cn(
        "absolute bottom-0 right-0 rounded-full border-background bg-green-500",
        sizeClasses[size],
        borderClasses[size],
        className
      )}
      aria-label="Online"
    >
      <span className="absolute inset-0 rounded-full bg-green-500 animate-ping opacity-40" />
    </span>
  )
}
