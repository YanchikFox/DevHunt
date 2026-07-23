"use client"

/**
 * Standardized loading components for DevHunt
 * UX: Consistent loading states across all pages (FRONTEND_UX_IMPROVEMENT_PLAN 1.3)
 */

import { useMemo } from "react"
import { Skeleton } from "@/components/ui/skeleton"
import { Loader2 } from "lucide-react"

/**
 * Loading skeleton for lists/cards
 */
export function LoadingSkeleton({ count = 3 }: { count?: number }) {
  const skeletonKeys = useMemo(
    () => Array.from({ length: count }, () => crypto.randomUUID()),
    [count]
  )

  return (
    <div className="space-y-4">
      {skeletonKeys.map((key) => (
        <div key={key} className="border rounded-lg p-4 space-y-3">
          <Skeleton className="h-6 w-3/4" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-2/3" />
          <div className="flex gap-2">
            <Skeleton className="h-6 w-20" />
            <Skeleton className="h-6 w-20" />
          </div>
        </div>
      ))}
    </div>
  )
}

/**
 * Loading spinner for buttons/actions
 */
export function LoadingSpinner({ className }: { className?: string }) {
  return <Loader2 className={`animate-spin ${className || "h-4 w-4"}`} />
}

/**
 * Centered loader for sections, tabs, or containers
 */
export function CenteredLoader({ 
  className, 
  size = "h-8 w-8" 
}: { 
  className?: string
  size?: string 
}) {
  return (
    <div className={`flex justify-center items-center py-8 ${className || ""}`}>
      <Loader2 className={`animate-spin text-muted-foreground ${size}`} />
    </div>
  )
}

/**
 * Full page loading skeleton
 */
export function LoadingPage() {
  const pageSkeletonKeys = useMemo(() => Array.from({ length: 6 }, () => crypto.randomUUID()), [])

  return (
    <div className="flex min-h-screen flex-col">
      <div className="border-b">
        <div className="container mx-auto px-4 py-4">
          <Skeleton className="h-10 w-32" />
        </div>
      </div>
      <main className="flex-1 container mx-auto px-4 py-8 space-y-6">
        <Skeleton className="h-10 w-64" />
        <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
          {pageSkeletonKeys.map((key) => (
            <div key={key} className="border rounded-lg p-4 space-y-3">
              <Skeleton className="h-6 w-3/4" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-4 w-2/3" />
            </div>
          ))}
        </div>
      </main>
    </div>
  )
}

/**
 * Loading state for cards
 */
export function LoadingCard() {
  return (
    <div className="border rounded-lg p-6 space-y-4">
      <Skeleton className="h-6 w-1/2" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-3/4" />
      <div className="flex gap-2">
        <Skeleton className="h-8 w-24" />
        <Skeleton className="h-8 w-24" />
      </div>
    </div>
  )
}
