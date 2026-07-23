"use client"

import { useState, useEffect, useCallback } from "react"

export interface GalleryImage {
  id: string
  url: string
  name: string
  description?: string
  index: number
}

export interface ProjectMediaItem {
  id?: string
  name?: string
  description?: string
  url?: string
  type?: string
  uploadedAt?: string
}

export interface GalleryLightboxState {
  selectedImage: GalleryImage | null
  setSelectedImage: (image: GalleryImage | null) => void
  openImage: (media: ProjectMediaItem, index: number) => void
  navigatePrev: () => void
  navigateNext: () => void
  close: () => void
}

export function useGalleryLightbox(projectMedia: ProjectMediaItem[]): GalleryLightboxState {
  const [selectedImage, setSelectedImage] = useState<GalleryImage | null>(null)

  // Open specific image
  const openImage = useCallback((media: ProjectMediaItem, index: number) => {
    setSelectedImage({
      id: media.id || '',
      url: media.url || '',
      name: media.name || '',
      description: media.description,
      index
    })
  }, [])

  // Navigate to previous image
  const navigatePrev = useCallback(() => {
    if (!selectedImage || projectMedia.length <= 1) return

    const newIndex = selectedImage.index === 0
      ? projectMedia.length - 1
      : selectedImage.index - 1
    const newMedia = projectMedia[newIndex]

    setSelectedImage({
      id: newMedia.id || '',
      url: newMedia.url || '',
      name: newMedia.name || '',
      description: newMedia.description,
      index: newIndex
    })
  }, [selectedImage, projectMedia])

  // Navigate to next image
  const navigateNext = useCallback(() => {
    if (!selectedImage || projectMedia.length <= 1) return

    const newIndex = selectedImage.index === projectMedia.length - 1
      ? 0
      : selectedImage.index + 1
    const newMedia = projectMedia[newIndex]

    setSelectedImage({
      id: newMedia.id || '',
      url: newMedia.url || '',
      name: newMedia.name || '',
      description: newMedia.description,
      index: newIndex
    })
  }, [selectedImage, projectMedia])

  // Close lightbox
  const close = useCallback(() => {
    setSelectedImage(null)
  }, [])

  // Keyboard navigation
  useEffect(() => {
    if (!selectedImage) return

    const handleKeyDown = (e: KeyboardEvent) => {
      switch (e.key) {
        case 'Escape':
          close()
          break
        case 'ArrowLeft':
          navigatePrev()
          break
        case 'ArrowRight':
          navigateNext()
          break
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [selectedImage, close, navigatePrev, navigateNext])

  return {
    selectedImage,
    setSelectedImage,
    openImage,
    navigatePrev,
    navigateNext,
    close,
  }
}
