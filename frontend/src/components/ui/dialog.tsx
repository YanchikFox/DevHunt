import {
  forwardRef,
  type ComponentPropsWithoutRef,
  type ElementRef,
  type HTMLAttributes,
} from "react"
import {
  Close as DialogClose,
  Content as DialogContentPrimitive,
  Description as DialogDescriptionPrimitive,
  Overlay as DialogOverlayPrimitive,
  Portal as DialogPortal,
  Root as Dialog,
  Title as DialogTitlePrimitive,
  Trigger as DialogTrigger,
} from "@radix-ui/react-dialog"
import { X } from "lucide-react"

import { cn } from "@/lib/utils"

/**
 * Root component for the Dialog.
 * Manages the open state of the dialog.
 */
/**
 * Overlay that covers the screen behind the Dialog.
 */
const DialogOverlay = forwardRef<
  ElementRef<typeof DialogOverlayPrimitive>,
  ComponentPropsWithoutRef<typeof DialogOverlayPrimitive>
>(({ className, ...props }, ref) => (
  <DialogOverlayPrimitive
    ref={ref}
    className={cn(
      // Dim the page behind and apply a gentle blur so the elevated dialog
      // surface reads as a clear layer above, not a rectangle glued on top.
      "fixed inset-0 z-50 bg-black/55 backdrop-blur-sm data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=closed]:animate-out data-[state=closed]:fade-out-0",
      className
    )}
    {...props}
  />
))
DialogOverlay.displayName = DialogOverlayPrimitive.displayName

/**
 * Content container for the Dialog.
 *
 * Visual direction follows the rest of the app:
 *   * `bg-bg-elevated` — the same token used by project cards / overview
 *     tiles, so modals read as "elevation" rather than a flat rectangle
 *     drawn on the dark background.
 *   * `rounded-[14px]` + `border-border` — matches the board column tiles.
 *   * A diffuse dark shadow + faint top highlight (via a pseudo-layer) give
 *     the surface subtle depth on dark themes where shadows otherwise get
 *     swallowed by the page background.
 */
const DialogContent = forwardRef<
  ElementRef<typeof DialogContentPrimitive>,
  ComponentPropsWithoutRef<typeof DialogContentPrimitive>
>(({ className, children, ...props }, ref) => (
  <DialogPortal>
    <DialogOverlay />
    <DialogContentPrimitive
      ref={ref}
      className={cn(
        // `fixed` already creates a positioning context for the `::before`
        // top-highlight — don't add `relative` here, it would override `fixed`
        // in Tailwind's stylesheet (both map to `position:*` with equal
        // specificity) and the dialog would slide off-screen.
        "fixed left-[50%] top-[50%] z-50 grid w-full max-w-lg max-h-[85vh] translate-x-[-50%] translate-y-[-50%] gap-4",
        "border border-border bg-bg-elevated p-6 overflow-y-auto",
        "shadow-[0_24px_72px_-12px_rgb(0_0_0/0.55),0_2px_8px_-2px_rgb(0_0_0/0.25)]",
        // Subtle inner top highlight — 1px line just inside the border, fading
        // out quickly. Reads as "glass / polished" on dark, invisible on light.
        "before:pointer-events-none before:absolute before:inset-x-0 before:top-0 before:h-px before:rounded-t-[inherit] before:bg-gradient-to-r before:from-transparent before:via-white/10 before:to-transparent",
        "duration-200 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95 data-[state=open]:slide-in-from-left-1/2 data-[state=open]:slide-in-from-top-[48%] data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=closed]:slide-in-to-left-1/2 data-[state=closed]:slide-in-to-top-[48%]",
        "sm:rounded-[14px]",
        className
      )}
      {...props}
    >
      {children}
      <DialogClose className="absolute right-3 top-3 inline-flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground opacity-70 transition-[opacity,background-color,color] hover:bg-muted/60 hover:text-foreground hover:opacity-100 focus:outline-none focus-visible:ring-1 focus-visible:ring-ring/60 disabled:pointer-events-none">
        <X className="h-3.5 w-3.5" />
        <span className="sr-only">Close</span>
      </DialogClose>
    </DialogContentPrimitive>
  </DialogPortal>
))
DialogContent.displayName = DialogContentPrimitive.displayName

/**
 * Header section of the Dialog.
 * Typically contains the DialogTitle and DialogDescription.
 */
const DialogHeader = ({ className, ...props }: HTMLAttributes<HTMLDivElement>) => (
  <div className={cn("flex flex-col space-y-1.5 text-center sm:text-left", className)} {...props} />
)
DialogHeader.displayName = "DialogHeader"

/**
 * Footer section of the Dialog.
 * Useful for action buttons.
 */
const DialogFooter = ({ className, ...props }: HTMLAttributes<HTMLDivElement>) => (
  <div
    className={cn("flex flex-col-reverse sm:flex-row sm:justify-end sm:space-x-2", className)}
    {...props}
  />
)
DialogFooter.displayName = "DialogFooter"

/**
 * Title of the Dialog.
 * Accessible name for the dialog.
 */
const DialogTitle = forwardRef<
  ElementRef<typeof DialogTitlePrimitive>,
  ComponentPropsWithoutRef<typeof DialogTitlePrimitive>
>(({ className, ...props }, ref) => (
  <DialogTitlePrimitive
    ref={ref}
    className={cn("text-lg font-semibold leading-none tracking-tight", className)}
    {...props}
  />
))
DialogTitle.displayName = DialogTitlePrimitive.displayName

/**
 * Description of the Dialog.
 * Accessible description for the dialog.
 */
const DialogDescription = forwardRef<
  ElementRef<typeof DialogDescriptionPrimitive>,
  ComponentPropsWithoutRef<typeof DialogDescriptionPrimitive>
>(({ className, ...props }, ref) => (
  <DialogDescriptionPrimitive
    ref={ref}
    className={cn("text-sm text-muted-foreground", className)}
    {...props}
  />
))
DialogDescription.displayName = DialogDescriptionPrimitive.displayName

export {
  Dialog,
  DialogPortal,
  DialogOverlay,
  DialogClose,
  DialogTrigger,
  DialogContent,
  DialogHeader,
  DialogFooter,
  DialogTitle,
  DialogDescription,
}
