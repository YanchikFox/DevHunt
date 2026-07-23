"use client"

import React, { useState, useEffect, useCallback } from "react"
import Image from "next/image"
import { ChevronLeft, ChevronRight, X, ImageOff, Flag } from "lucide-react"
import { Button } from "@/components/ui/button"
import {
    Dialog,
    DialogContent,
    DialogTitle,
} from "@/components/ui/dialog"
function VisuallyHidden({ children }: { children: React.ReactNode }) {
    return (
        <span style={{ position: "absolute", width: 1, height: 1, padding: 0, margin: -1, overflow: "hidden", clip: "rect(0,0,0,0)", whiteSpace: "nowrap", border: 0 }}>
            {children}
        </span>
    )
}
import { normalizeImageUrl } from "./helpers"
import { ReportDialog } from "@/components/moderation/ReportDialog"
import { REPORT_TARGET_TYPES } from "@/lib/api/queries/moderation"

/**
 * Props for ImageLightbox component
 */
export interface ImageLightboxProps {
    images: string[]
    /** Optional array of IDs matching images array — enables image reporting */
    imageIds?: string[]
    initialIndex: number
    isOpen: boolean
    onClose: () => void
    t: (key: string, values?: Record<string, string | number>) => string
}

/**
 * Image Lightbox component for viewing images in fullscreen
 */
export function ImageLightbox({
    images,
    imageIds,
    initialIndex,
    isOpen,
    onClose,
    t
}: Readonly<ImageLightboxProps>) {
    const [currentIndex, setCurrentIndex] = useState(initialIndex)
    const [imageErrors, setImageErrors] = useState<Set<number>>(new Set())

    // Sync currentIndex with initialIndex when lightbox opens
    useEffect(() => {
        if (isOpen) {
            setCurrentIndex(initialIndex)
        }
    }, [isOpen, initialIndex])

    const goNext = useCallback(() => setCurrentIndex((i) => (i + 1) % images.length), [images.length])
    const goPrev = useCallback(() => setCurrentIndex((i) => (i - 1 + images.length) % images.length), [images.length])

    const handleImageError = useCallback(() => {
        setImageErrors(prev => new Set(prev).add(currentIndex))
    }, [currentIndex])

    const handleDialogOpenChange = useCallback((open: boolean) => { if (!open) onClose() }, [onClose])

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === "ArrowRight") goNext()
        if (e.key === "ArrowLeft") goPrev()
        if (e.key === "Escape") onClose()
    }

    if (!isOpen || images.length === 0) return null

    const currentImage = normalizeImageUrl(images[currentIndex])

    return (
        <Dialog open={isOpen} onOpenChange={handleDialogOpenChange}>
            <DialogContent
                className="max-w-[95vw] max-h-[95vh] w-auto h-auto p-0 bg-black/95 border-none"
                onKeyDown={handleKeyDown}
            >
                <VisuallyHidden>
                    <DialogTitle>{t("activity.viewImage")}</DialogTitle>
                </VisuallyHidden>

                {/* Close button */}
                <Button
                    variant="ghost"
                    size="icon"
                    className="absolute right-2 top-2 z-50 h-10 w-10 rounded-full bg-black/50 text-white hover:bg-black/70"
                    onClick={onClose}
                >
                    <X className="h-6 w-6" />
                </Button>

                {/* Report image button */}
                {imageIds?.[currentIndex] && (
                    <div className="absolute right-14 top-2 z-50">
                        <ReportDialog
                            targetType={REPORT_TARGET_TYPES.IMAGE}
                            targetId={imageIds[currentIndex]}
                            trigger={
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-10 w-10 rounded-full bg-black/50 text-white hover:bg-black/70"
                                >
                                    <Flag className="h-5 w-5" />
                                </Button>
                            }
                        />
                    </div>
                )}

                {/* Image container */}
                <div className="relative flex items-center justify-center min-h-[50vh] max-h-[90vh]">
                    {imageErrors.has(currentIndex) ? (
                        <div className="flex flex-col items-center justify-center gap-4 text-muted-foreground">
                            <ImageOff className="h-16 w-16" />
                            <span>{t("activity.imageLoadError")}</span>
                        </div>
                    ) : (
                        <Image
                            src={currentImage}
                            alt={t("activity.imageAlt", { current: currentIndex + 1, total: images.length })}
                            width={1200}
                            height={800}
                            className="object-contain max-h-[90vh] w-auto"
                            unoptimized
                            onError={handleImageError}
                        />
                    )}
                </div>

                {/* Navigation arrows */}
                {images.length > 1 && (
                    <>
                        <Button
                            variant="ghost"
                            size="icon"
                            className="absolute left-2 top-1/2 -translate-y-1/2 z-50 h-12 w-12 rounded-full bg-black/50 text-white hover:bg-black/70"
                            onClick={goPrev}
                        >
                            <ChevronLeft className="h-8 w-8" />
                        </Button>
                        <Button
                            variant="ghost"
                            size="icon"
                            className="absolute right-2 top-1/2 -translate-y-1/2 z-50 h-12 w-12 rounded-full bg-black/50 text-white hover:bg-black/70"
                            onClick={goNext}
                        >
                            <ChevronRight className="h-8 w-8" />
                        </Button>

                        {/* Image counter */}
                        <div className="absolute bottom-4 left-1/2 -translate-x-1/2 z-50 px-4 py-2 rounded-full bg-black/50 text-white text-sm">
                            {currentIndex + 1} / {images.length}
                        </div>
                    </>
                )}
            </DialogContent>
        </Dialog>
    )
}
