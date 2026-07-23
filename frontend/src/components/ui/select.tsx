import { forwardRef, type ComponentPropsWithoutRef, type ElementRef } from "react";
import {
  Content as SelectContentPrimitive,
  Group as SelectGroup,
  Icon as SelectIcon,
  Item as SelectItemPrimitive,
  ItemIndicator,
  ItemText,
  Label as SelectLabelPrimitive,
  Portal as SelectPortal,
  Root as Select,
  ScrollDownButton as SelectScrollDownButtonPrimitive,
  ScrollUpButton as SelectScrollUpButtonPrimitive,
  Separator as SelectSeparatorPrimitive,
  Trigger as SelectTriggerPrimitive,
  Value as SelectValue,
  Viewport as SelectViewport,
} from "@radix-ui/react-select"
import { Check, ChevronDown, ChevronUp } from "lucide-react"

import { cn } from "@/lib/utils"

const SelectTrigger = forwardRef<
  ElementRef<typeof SelectTriggerPrimitive>,
  ComponentPropsWithoutRef<typeof SelectTriggerPrimitive>
>(({ className, children, ...props }, ref) => (
  <SelectTriggerPrimitive
    ref={ref}
    className={cn(
      "flex h-10 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 [&>span]:line-clamp-1",
      className
    )}
    {...props}
  >
    {children}
    <SelectIcon asChild>
      <ChevronDown className="h-4 w-4 opacity-50" />
    </SelectIcon>
  </SelectTriggerPrimitive>
))
SelectTrigger.displayName = SelectTriggerPrimitive.displayName

const SelectScrollUpButton = forwardRef<
  ElementRef<typeof SelectScrollUpButtonPrimitive>,
  ComponentPropsWithoutRef<typeof SelectScrollUpButtonPrimitive>
>(({ className, ...props }, ref) => (
  <SelectScrollUpButtonPrimitive
    ref={ref}
    className={cn("flex cursor-default items-center justify-center py-1", className)}
    {...props}
  >
    <ChevronUp className="h-4 w-4" />
  </SelectScrollUpButtonPrimitive>
))
SelectScrollUpButton.displayName = SelectScrollUpButtonPrimitive.displayName

const SelectScrollDownButton = forwardRef<
  ElementRef<typeof SelectScrollDownButtonPrimitive>,
  ComponentPropsWithoutRef<typeof SelectScrollDownButtonPrimitive>
>(({ className, ...props }, ref) => (
  <SelectScrollDownButtonPrimitive
    ref={ref}
    className={cn("flex cursor-default items-center justify-center py-1", className)}
    {...props}
  >
    <ChevronDown className="h-4 w-4" />
  </SelectScrollDownButtonPrimitive>
))
SelectScrollDownButton.displayName = SelectScrollDownButtonPrimitive.displayName

const SelectContent = forwardRef<
  ElementRef<typeof SelectContentPrimitive>,
  ComponentPropsWithoutRef<typeof SelectContentPrimitive>
>(({ className, children, position = "popper", ...props }, ref) => (
  <SelectPortal>
    <SelectContentPrimitive
      ref={ref}
      className={cn(
        "relative z-50 max-h-96 min-w-[8rem] overflow-hidden rounded-md border bg-popover text-popover-foreground shadow-md data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2",
        position === "popper" &&
          "data-[side=bottom]:translate-y-1 data-[side=left]:-translate-x-1 data-[side=right]:translate-x-1 data-[side=top]:-translate-y-1",
        className
      )}
      position={position}
      {...props}
    >
      <SelectScrollUpButton />
      <SelectViewport
        className={cn(
          "p-1",
          position === "popper" &&
            "h-[var(--radix-select-trigger-height)] w-full min-w-[var(--radix-select-trigger-width)]"
        )}
      >
        {children}
      </SelectViewport>
      <SelectScrollDownButton />
    </SelectContentPrimitive>
  </SelectPortal>
))
SelectContent.displayName = SelectContentPrimitive.displayName

const SelectLabel = forwardRef<
  ElementRef<typeof SelectLabelPrimitive>,
  ComponentPropsWithoutRef<typeof SelectLabelPrimitive>
>(({ className, ...props }, ref) => (
  <SelectLabelPrimitive
    ref={ref}
    className={cn("py-1.5 pl-8 pr-2 text-sm font-semibold", className)}
    {...props}
  />
))
SelectLabel.displayName = SelectLabelPrimitive.displayName

const SelectItem = forwardRef<
  ElementRef<typeof SelectItemPrimitive>,
  ComponentPropsWithoutRef<typeof SelectItemPrimitive>
>(({ className, children, ...props }, ref) => (
  <SelectItemPrimitive
    ref={ref}
    className={cn(
      "relative flex w-full cursor-default select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none focus:bg-accent focus:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50",
      className
    )}
    {...props}
  >
    <span className="absolute left-2 flex h-3.5 w-3.5 items-center justify-center">
      <ItemIndicator>
        <Check className="h-4 w-4" />
      </ItemIndicator>
    </span>

    <ItemText>{children}</ItemText>
  </SelectItemPrimitive>
))
SelectItem.displayName = SelectItemPrimitive.displayName

const SelectSeparator = forwardRef<
  ElementRef<typeof SelectSeparatorPrimitive>,
  ComponentPropsWithoutRef<typeof SelectSeparatorPrimitive>
>(({ className, ...props }, ref) => (
  <SelectSeparatorPrimitive
    ref={ref}
    className={cn("-mx-1 my-1 h-px bg-muted", className)}
    {...props}
  />
))
SelectSeparator.displayName = SelectSeparatorPrimitive.displayName

export {
  Select,
  SelectGroup,
  SelectValue,
  SelectTrigger,
  SelectContent,
  SelectLabel,
  SelectItem,
  SelectSeparator,
  SelectScrollUpButton,
  SelectScrollDownButton,
}
