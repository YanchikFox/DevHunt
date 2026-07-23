"use client"

import { useCallback, useMemo, useState } from "react"
import { useProjectNews, useDeleteNews, useUpdateNews } from "@/lib/api/queries/news"
import { useProjectGallery, useDeleteFile } from "@/lib/api/queries/files"
import type { GalleryImage, ProjectMediaItem, ProjectNewsItem, ProjectWithExtras, ToastFn, TranslateFn } from "./types"

interface UseProjectDetailMediaOptions {
  projectId: string
  project: ProjectWithExtras | undefined
  t: TranslateFn
  toast: ToastFn
  setIsAddNewsDialogOpen: (open: boolean) => void
  setIsUploadMediaDialogOpen: (open: boolean) => void
  deletingGalleryImageId: string | null
  setDeletingGalleryImageId: (id: string | null) => void
  deletingNewsId: string | null
  setDeletingNewsId: (id: string | null) => void
}

export function useProjectDetailMedia({
  projectId,
  project,
  t,
  toast,
  setIsAddNewsDialogOpen,
  setIsUploadMediaDialogOpen,
  deletingGalleryImageId,
  setDeletingGalleryImageId,
  deletingNewsId,
  setDeletingNewsId,
}: UseProjectDetailMediaOptions) {
  const [selectedGalleryImage, setSelectedGalleryImage] = useState<GalleryImage | null>(null)

  const {
    data: projectNewsData,
    isLoading: newsLoading,
    refetch: refetchNews,
    isRefetching: newsRefetching,
  } = useProjectNews(projectId)

  const {
    data: galleryResponse,
    isLoading: galleryLoading,
  } = useProjectGallery(projectId)

  const deleteFile = useDeleteFile()
  const deleteNews = useDeleteNews()
  const updateNews = useUpdateNews()

  const projectMedia: ProjectMediaItem[] = useMemo(() => {
    const galleryData = galleryResponse?.data
    if (Array.isArray(galleryData) && galleryData.length > 0) {
      return galleryData.map((item) => ({
        id: item.id,
        name: item.fileName,
        description: item.description,
        url: `/api/proxy-core/projects/${projectId}/files/${item.id}`,
        type: item.contentType?.startsWith("video/") ? "video" : "image",
        uploadedAt: item.uploadedAt,
      }))
    }
    return Array.isArray(project?.media) ? project.media : []
  }, [galleryResponse, project, projectId])

  const handleOpenGalleryImage = useCallback((media: ProjectMediaItem, index: number) => {
    setSelectedGalleryImage({
      id: media.id || "",
      url: media.url || "",
      name: media.name || "",
      description: media.description,
      index,
    })
  }, [])

  const handleOpenUploadMediaDialog = useCallback(() => {
    setIsUploadMediaDialogOpen(true)
  }, [setIsUploadMediaDialogOpen])

  const handleOpenAddNewsDialog = useCallback(() => {
    setIsAddNewsDialogOpen(true)
  }, [setIsAddNewsDialogOpen])

  const handleRefreshNews = useCallback(() => {
    refetchNews()
  }, [refetchNews])

  const handleRequestDeleteNews = useCallback((id: string) => {
    setDeletingNewsId(id)
  }, [setDeletingNewsId])

  const handleRequestDeleteGalleryImage = useCallback((imageId: string) => {
    setDeletingGalleryImageId(imageId)
  }, [setDeletingGalleryImageId])

  const handleCloseGalleryLightbox = useCallback(() => {
    setSelectedGalleryImage(null)
  }, [])

  const handleNavigateGalleryLightbox = useCallback((image: GalleryImage) => {
    setSelectedGalleryImage(image)
  }, [])

  const handleDeleteGalleryImage = useCallback(async () => {
    if (!deletingGalleryImageId || !projectId) return
    try {
      await deleteFile.mutateAsync({ projectId, fileId: deletingGalleryImageId })
      toast({
        title: t("projects.imageDeleted"),
        description: t("projects.imageDeleteSuccess"),
      })
      setSelectedGalleryImage(null)
    } catch {
      toast({
        title: t("common.error"),
        description: t("projects.imageDeleteFailed"),
        variant: "destructive",
      })
    }
    setDeletingGalleryImageId(null)
  }, [deletingGalleryImageId, projectId, deleteFile, toast, t, setDeletingGalleryImageId])

  const handleDeleteNewsItem = useCallback(async () => {
    if (!deletingNewsId || !projectId) return
    try {
      await deleteNews.mutateAsync({ projectId, newsId: deletingNewsId })
      toast({
        title: t("news.newsDeleted"),
        description: t("news.newsDeleteSuccess"),
      })
    } catch {
      toast({
        title: t("common.error"),
        description: t("news.newsDeleteFailed"),
        variant: "destructive",
      })
    }
    setDeletingNewsId(null)
  }, [deletingNewsId, projectId, deleteNews, toast, t, setDeletingNewsId])

  const handleTogglePinNews = useCallback(async (newsId: string, nextValue: boolean) => {
    if (!projectId) return
    try {
      await updateNews.mutateAsync({
        projectId,
        newsId,
        data: { isPinned: nextValue },
      })
      toast({
        title: nextValue ? t("news.newsPinned") : t("news.newsUnpinned"),
      })
    } catch {
      toast({
        title: t("common.error"),
        description: t("news.newsUpdateFailed"),
        variant: "destructive",
      })
    }
  }, [projectId, updateNews, toast, t])

  const handleChangeNewsVisibility = useCallback(
    async (newsId: string, visibility: ProjectNewsItem["visibility"] | undefined) => {
      if (!projectId || !visibility) return
      try {
        await updateNews.mutateAsync({
          projectId,
          newsId,
          data: { visibility },
        })
        toast({
          title: t("news.visibilityUpdated"),
          description: visibility === "public"
            ? t("news.newsNowVisibleToAll")
            : t("news.newsHiddenFromPublic"),
        })
      } catch {
        toast({
          title: t("common.error"),
          description: t("news.visibilityChangeFailed"),
          variant: "destructive",
        })
      }
    },
    [projectId, updateNews, toast, t]
  )

  return {
    projectNewsData,
    newsLoading,
    newsRefetching,
    refetchNews,
    galleryLoading,
    projectMedia,
    selectedGalleryImage,
    handleOpenGalleryImage,
    handleOpenUploadMediaDialog,
    handleOpenAddNewsDialog,
    handleRefreshNews,
    handleRequestDeleteNews,
    handleRequestDeleteGalleryImage,
    handleCloseGalleryLightbox,
    handleNavigateGalleryLightbox,
    handleDeleteGalleryImage,
    handleDeleteNewsItem,
    handleTogglePinNews,
    handleChangeNewsVisibility,
    deleteFilePending: deleteFile.isPending,
    deleteNewsPending: deleteNews.isPending,
  }
}
