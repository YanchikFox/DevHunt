"use client"

import { useTranslations } from "next-intl"
import { useEffect, useCallback, type MouseEvent } from "react"
import { X, ChevronLeft, ChevronRight, Trash2 } from "lucide-react"

interface GalleryImage {
  id: string
  url: string
  name: string
  description?: string
  index: number
}

interface MediaItem {
  id?: string
  url?: string
  name?: string
  description?: string
}

interface GalleryLightboxProps {
  selectedImage: GalleryImage | null
  media: MediaItem[]
  canDelete?: boolean
  onClose: () => void
  onNavigate: (image: GalleryImage) => void
  onDelete?: (imageId: string) => void
}

export function GalleryLightbox({
  selectedImage,
  media,
  canDelete = false,
  onClose,
  onNavigate,
  onDelete,
}: GalleryLightboxProps) {
  const t = useTranslations()

  const navigateToIndex = useCallback((newIndex: number) => {
    const newMedia = media[newIndex]
    onNavigate({
      id: newMedia.id || '',
      url: newMedia.url || '',
      name: newMedia.name || '',
      description: newMedia.description,
      index: newIndex
    })
  }, [media, onNavigate])

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    if (!selectedImage) return
    if (e.key === 'Escape') {
      onClose()
    } else if (e.key === 'ArrowLeft') {
      navigateToIndex(selectedImage.index === 0 ? media.length - 1 : selectedImage.index - 1)
    } else if (e.key === 'ArrowRight') {
      navigateToIndex(selectedImage.index === media.length - 1 ? 0 : selectedImage.index + 1)
    }
  }, [selectedImage, media, onClose, navigateToIndex])

  useEffect(() => {
    if (!selectedImage) return
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [selectedImage, handleKeyDown])

  const handleContainerClick = useCallback((e: MouseEvent) => e.stopPropagation(), [])
  const handleDeleteImage = useCallback(() => {
    if (selectedImage && onDelete) {
      onDelete(selectedImage.id)
      onClose()
    }
  }, [selectedImage, onDelete, onClose])

  if (!selectedImage) return null

  const navigatePrev = () => navigateToIndex(selectedImage.index === 0 ? media.length - 1 : selectedImage.index - 1)
  const navigateNext = () => navigateToIndex(selectedImage.index === media.length - 1 ? 0 : selectedImage.index + 1)

  return (
    <div
      className="fixed inset-0 z-[100] bg-black/90 backdrop-blur-sm flex items-center justify-center"
      onClick={onClose}
    >
      <div
        className="relative max-w-[90vw] max-h-[90vh] flex flex-col"
        onClick={handleContainerClick}
      >
        {/* Top buttons */}
        <div className="absolute -top-10 right-0 flex items-center gap-2">
          {canDelete && selectedImage.id && onDelete && (
            <button
              onClick={handleDeleteImage}
              className="text-red-400 hover:text-red-300 transition-colors p-2"
              title={t("projects.deleteImage")}
            >
              <Trash2 className="h-5 w-5" />
            </button>
          )}
          <button
            onClick={onClose}
            className="text-white/70 hover:text-white transition-colors p-2"
          >
            <X className="h-6 w-6" />
          </button>
        </div>

        {/* Navigation buttons */}
        {media.length > 1 && (
          <>
            <button
              onClick={navigatePrev}
              className="absolute left-0 top-1/2 -translate-y-1/2 -translate-x-12 text-white/70 hover:text-white transition-colors p-2 bg-black/50 rounded-full"
            >
              <ChevronLeft className="h-8 w-8" />
            </button>
            <button
              onClick={navigateNext}
              className="absolute right-0 top-1/2 -translate-y-1/2 translate-x-12 text-white/70 hover:text-white transition-colors p-2 bg-black/50 rounded-full"
            >
              <ChevronRight className="h-8 w-8" />
            </button>
          </>
        )}

        {/* Image */}
        <img
          src={selectedImage.url}
          alt={selectedImage.name}
          className="max-w-full max-h-[75vh] object-contain rounded-lg"
        />

        {/* Info panel */}
        <div className="mt-4 px-2 text-center">
          <h3 className="text-white font-medium text-lg">{selectedImage.name}</h3>
          {selectedImage.description && (
            <p className="text-white/70 mt-1 text-sm">{selectedImage.description}</p>
          )}
          <p className="text-white/50 text-xs mt-2">
            {selectedImage.index + 1} / {media.length}
          </p>
        </div>
      </div>
    </div>
  )
}

