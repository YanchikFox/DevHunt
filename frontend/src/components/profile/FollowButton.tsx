"use client"

import { Button } from "@/components/ui/button"
import { Loader2, UserPlus, Check } from "lucide-react"
import { apiClient } from "@/lib/api/client"
import { useToast } from "@/hooks/use-toast"
import { useCallback, useState } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { cn } from "@/lib/utils"
import { useTranslations } from "next-intl"

/**
 * Props for the FollowButton component.
 */
export type FollowButtonProps = {
  /** ID of the user to follow/unfollow */
  userId: string
  /** Whether the current user is already following the target user */
  initialIsFollowing: boolean
  /** Whether the target user is following the current user */
  initialIsFollower: boolean
  /** Visibility setting of the relationship (e.g., "public", "private") */
  relationVisibility: string
  /** Callback function triggered when follow state changes */
  onChange?: (state: { isFollowing: boolean }) => void
  /** Additional CSS classes */
  className?: string
  /** Optional size variant for rendering */
  size?: "default" | "sm" | "icon"
  /** Optional visual variant */
  variant?: "default" | "destructive" | "outline" | "secondary" | "ghost" | "link"
}

/**
 * A button component for following or unfollowing a user.
 * Handles the API call and updates the local state.
 *
 * @example
 * ```tsx
 * <FollowButton
 *   userId="123"
 *   initialIsFollowing={false}
 *   initialIsFollower={false}
 *   relationVisibility="public"
 *   onChange={(state) => console.log(state)}
 * />
 * ```
 */
export function FollowButton({
  userId,
  initialIsFollowing,
  initialIsFollower,
  relationVisibility,
  onChange,
  className,
  size = "default",
  variant,
}: FollowButtonProps) {
  const t = useTranslations("followButton")
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [isFollowing, setIsFollowing] = useState(initialIsFollowing)
  const [isPending, setIsPending] = useState(false)

  const isPrivate = relationVisibility === "private"
  const canInteract = !isPrivate || initialIsFollowing || initialIsFollower
  const label = isFollowing ? t("following") : initialIsFollower ? t("followBack") : t("follow")

  const handleToggle = useCallback(async () => {
    if (!canInteract || isPending) return
    setIsPending(true)
    try {
      if (isFollowing) {
        await apiClient.delete(`/users/${userId}/follow`)
        setIsFollowing(false)
        toast({ title: t("unfollowed"), description: t("unfollowedDescription") })
      } else {
        await apiClient.post(`/users/${userId}/follow`)
        setIsFollowing(true)
        toast({
          title: t("nowFollowing"),
          description: t("nowFollowingDescription"),
        })
      }
      onChange?.({ isFollowing: !isFollowing })
      // Invalidate profile & stats queries so counters update
      queryClient.invalidateQueries({ queryKey: ["profile"] })
      queryClient.invalidateQueries({ queryKey: ["userStats", userId] })
    } catch (error) {
      const userMessage =
        (typeof error === "object" && error && "userMessage" in error && (error as { userMessage?: string }).userMessage) ||
        (error instanceof Error ? error.message : null) ||
        t("pleaseRetry")
      toast({ title: t("actionFailed"), description: userMessage })
    } finally {
      setIsPending(false)
    }
  }, [canInteract, isFollowing, isPending, onChange, toast, userId, queryClient, t])

  const baseLabel = isPrivate && !canInteract ? t("privateProfile") : label
  const buttonSizeClass =
    size === "icon"
      ? "h-8 w-8 p-0 justify-center"
      : size === "sm"
        ? "h-8 px-2 text-xs"
        : "h-8 px-3 text-sm"
  const gapClass = size === "icon" ? "gap-0" : "gap-2"

  return (
    <Button
      onClick={handleToggle}
      disabled={!canInteract || isPending}
      variant={isFollowing ? "ghost" : variant || "default"}
      aria-label={baseLabel}
      className={cn("flex items-center", gapClass, buttonSizeClass, className)}
    >
      {isPending ? (
        <Loader2 className="h-4 w-4 animate-spin" />
      ) : isFollowing ? (
        <Check className="h-4 w-4" />
      ) : (
        <UserPlus className="h-4 w-4" />
      )}
      {size !== "icon" ? <span>{baseLabel}</span> : <span className="sr-only">{baseLabel}</span>}
    </Button>
  )
}
