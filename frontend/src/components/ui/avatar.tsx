"use client"

import {
  Fallback as AvatarFallbackPrimitive,
  Image as AvatarImagePrimitive,
  Root as AvatarRootPrimitive,
} from "@radix-ui/react-avatar"
import { forwardRef, type ComponentPropsWithoutRef, type ElementRef } from "react"

import { cn } from "@/lib/utils"

const Avatar = forwardRef<
  ElementRef<typeof AvatarRootPrimitive>,
  ComponentPropsWithoutRef<typeof AvatarRootPrimitive>
>(({ className, ...props }, ref) => (
  <AvatarRootPrimitive
    ref={ref}
    className={cn("relative flex h-10 w-10 shrink-0 overflow-hidden rounded-full", className)}
    {...props}
  />
))
Avatar.displayName = AvatarRootPrimitive.displayName

const AvatarImage = forwardRef<
  ElementRef<typeof AvatarImagePrimitive>,
  ComponentPropsWithoutRef<typeof AvatarImagePrimitive>
>(({ className, ...props }, ref) => (
  <AvatarImagePrimitive
    ref={ref}
    className={cn("aspect-square h-full w-full", className)}
    {...props}
  />
))
AvatarImage.displayName = AvatarImagePrimitive.displayName

const AvatarFallback = forwardRef<
  ElementRef<typeof AvatarFallbackPrimitive>,
  ComponentPropsWithoutRef<typeof AvatarFallbackPrimitive>
>(({ className, ...props }, ref) => (
  <AvatarFallbackPrimitive
    ref={ref}
    className={cn(
      "flex h-full w-full items-center justify-center rounded-full bg-muted",
      className
    )}
    {...props}
  />
))
AvatarFallback.displayName = AvatarFallbackPrimitive.displayName

export { Avatar, AvatarImage, AvatarFallback }
