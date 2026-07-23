"use client"

import { forwardRef, type HTMLAttributes } from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { type LucideIcon, TrendingUp, TrendingDown } from "lucide-react"

import { cn } from "@/lib/utils"
import { Card } from "./card"

const statsCardVariants = cva("relative overflow-hidden", {
  variants: {
    variant: {
      default: "",
      gradient: "text-white",
    },
    colorScheme: {
      blue: "",
      green: "",
      orange: "",
      purple: "",
    },
  },
  compoundVariants: [
    {
      variant: "gradient",
      colorScheme: "blue",
      className: "bg-gradient-card-blue",
    },
    {
      variant: "gradient",
      colorScheme: "green",
      className: "bg-gradient-card-green",
    },
    {
      variant: "gradient",
      colorScheme: "orange",
      className: "bg-gradient-card-orange",
    },
    {
      variant: "gradient",
      colorScheme: "purple",
      className: "bg-gradient-to-br from-purple-500 to-pink-500",
    },
  ],
  defaultVariants: {
    variant: "default",
    colorScheme: "blue",
  },
})

const iconContainerVariants = cva(
  "flex h-10 w-10 items-center justify-center rounded-lg",
  {
    variants: {
      variant: {
        default: "",
        gradient: "bg-white/20",
      },
      colorScheme: {
        blue: "bg-blue-50 text-blue-500/80 dark:bg-blue-900/20 dark:text-blue-400/70",
        green: "bg-green-50 text-green-500/80 dark:bg-green-900/20 dark:text-green-400/70",
        orange: "bg-orange-50 text-orange-500/80 dark:bg-orange-900/20 dark:text-orange-400/70",
        purple: "bg-purple-50 text-purple-500/80 dark:bg-purple-900/20 dark:text-purple-400/70",
      },
    },
    compoundVariants: [
      {
        variant: "gradient",
        className: "bg-white/20 text-white",
      },
    ],
    defaultVariants: {
      variant: "default",
      colorScheme: "blue",
    },
  }
)

export interface StatsCardProps
  extends Omit<HTMLAttributes<HTMLDivElement>, "color">,
    VariantProps<typeof statsCardVariants> {
  title: string
  value: string | number
  icon: LucideIcon
  trend?: {
    value: number
    label?: string
  }
  description?: string
}

const StatsCard = forwardRef<HTMLDivElement, StatsCardProps>(
  (
    { className, variant, colorScheme, title, value, icon: Icon, trend, description, ...props },
    ref
  ) => {
    const isPositiveTrend = trend && trend.value >= 0
    const TrendIcon = isPositiveTrend ? TrendingUp : TrendingDown
    const displayValue = typeof value === "number" ? value.toLocaleString() : value

    return (
      <Card
        ref={ref}
        variant="elevated"
        hover="lift"
        className={cn(statsCardVariants({ variant, colorScheme, className }))}
        {...props}
      >
        <div className="p-6">
          <div className="flex items-start justify-between gap-4">
            <div className="space-y-2">
              <p
                className={cn(
                  "text-sm font-medium",
                  variant === "gradient" ? "text-white/80" : "text-muted-foreground"
                )}
              >
                {title}
              </p>
              <p
                className={cn(
                  "text-2xl font-semibold",
                  variant === "gradient" ? "text-white" : "text-foreground"
                )}
              >
                {displayValue}
              </p>
              {description && (
                <p
                  className={cn(
                    "text-sm",
                    variant === "gradient" ? "text-white/70" : "text-muted-foreground"
                  )}
                >
                  {description}
                </p>
              )}
            </div>
            <div className={cn(iconContainerVariants({ variant, colorScheme }))}>
              <Icon className="h-5 w-5" />
            </div>
          </div>
          {trend && (
            <div className="mt-4 flex items-center gap-2">
              <div
                className={cn(
                  "flex items-center gap-1 text-sm font-medium",
                  variant === "gradient"
                    ? "text-white"
                    : isPositiveTrend
                      ? "text-green-600 dark:text-green-400"
                      : "text-red-600 dark:text-red-400"
                )}
              >
                <TrendIcon className="h-4 w-4" />
                <span>
                  {isPositiveTrend ? "+" : ""}
                  {trend.value}
                </span>
              </div>
              {trend.label && (
                <span
                  className={cn(
                    "text-sm",
                    variant === "gradient" ? "text-white/70" : "text-muted-foreground"
                  )}
                >
                  {trend.label}
                </span>
              )}
            </div>
          )}
        </div>
      </Card>
    )
  }
)
StatsCard.displayName = "StatsCard"

export { StatsCard, statsCardVariants }
