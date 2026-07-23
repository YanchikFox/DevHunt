"use client"

import { forwardRef, type ComponentPropsWithoutRef, type ElementRef } from "react";
import {
  Corner as ScrollAreaCorner,
  Root as ScrollAreaRoot,
  Scrollbar as ScrollAreaScrollbar,
  Thumb as ScrollAreaThumb,
  Viewport as ScrollAreaViewport,
} from "@radix-ui/react-scroll-area"

import { cn } from "@/lib/utils"

const ScrollBar = forwardRef<
  ElementRef<typeof ScrollAreaScrollbar>,
  ComponentPropsWithoutRef<typeof ScrollAreaScrollbar>
>(({ className, orientation = "vertical", ...props }, ref) => (
  <ScrollAreaScrollbar
    ref={ref}
    orientation={orientation}
    className={cn(
      "flex touch-none select-none transition-colors",
      orientation === "vertical" && "h-full w-2.5 border-l border-l-transparent p-[1px]",
      orientation === "horizontal" && "h-2.5 flex-col border-t border-t-transparent p-[1px]",
      className
    )}
    {...props}
  >
    <ScrollAreaThumb className="relative flex-1 rounded-full bg-border" />
  </ScrollAreaScrollbar>
))
ScrollBar.displayName = ScrollAreaScrollbar.displayName

const ScrollArea = forwardRef<
  ElementRef<typeof ScrollAreaRoot>,
  ComponentPropsWithoutRef<typeof ScrollAreaRoot>
>(({ className, children, ...props }, ref) => (
  <ScrollAreaRoot
    ref={ref}
    className={cn("relative overflow-hidden", className)}
    {...props}
  >
    <ScrollAreaViewport className="h-full w-full rounded-[inherit]">
      {children}
    </ScrollAreaViewport>
    <ScrollBar />
    <ScrollAreaCorner />
  </ScrollAreaRoot>
))
ScrollArea.displayName = ScrollAreaRoot.displayName

export { ScrollArea, ScrollBar }
