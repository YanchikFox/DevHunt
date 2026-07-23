"use client"

import { useState, useCallback } from "react"

type ConfirmActionState = {
  type: "publish" | "activate" | "complete" | "archive" | "unarchive" | "unpublish" | null
  projectId: string | null
}

export interface ProjectDialogsState {
  // Task dialogs
  isTaskDialogOpen: boolean
  setIsTaskDialogOpen: (open: boolean) => void

  // Invite dialogs
  isInviteDialogOpen: boolean
  setIsInviteDialogOpen: (open: boolean) => void

  // Request dialog
  isRequestDialogOpen: boolean
  setIsRequestDialogOpen: (open: boolean) => void

  // Edit dialog
  isEditDialogOpen: boolean
  setIsEditDialogOpen: (open: boolean) => void

  isDeleteDialogOpen: boolean
  setIsDeleteDialogOpen: (open: boolean) => void
  deleteConfirmText: string
  setDeleteConfirmText: (text: string) => void

  isSettingsDialogOpen: boolean
  setIsSettingsDialogOpen: (open: boolean) => void

  // News dialog
  isAddNewsDialogOpen: boolean
  setIsAddNewsDialogOpen: (open: boolean) => void

  // Media dialog
  isUploadMediaDialogOpen: boolean
  setIsUploadMediaDialogOpen: (open: boolean) => void

  // Confirm action dialog
  confirmAction: ConfirmActionState
  setConfirmAction: (action: ConfirmActionState) => void

  deletingGalleryImageId: string | null
  setDeletingGalleryImageId: (id: string | null) => void
  deletingNewsId: string | null
  setDeletingNewsId: (id: string | null) => void

  // Handlers
  handleOpenSettingsDialog: () => void
  handleOpenEditDialog: () => void
  handleOpenDeleteDialog: () => void
  handleCloseEditDialog: () => void
  handleCloseInviteDialog: () => void
  handleCloseDeleteProjectDialog: (open: boolean) => void
  handleDeleteConfirmTextChange: (e: React.ChangeEvent<HTMLInputElement>) => void
  handleCancelDeleteProject: () => void
  handleCloseConfirmActionDialog: (open: boolean) => void
}

function useConfirmActionState() {
  const [confirmAction, setConfirmAction] = useState<ConfirmActionState>({ type: null, projectId: null })
  const handleCloseConfirmActionDialog = useCallback((open: boolean) => {
    if (!open) setConfirmAction({ type: null, projectId: null })
  }, [])
  return { confirmAction, setConfirmAction, handleCloseConfirmActionDialog }
}

function useDeletionTargetState() {
  const [deletingGalleryImageId, setDeletingGalleryImageId] = useState<string | null>(null)
  const [deletingNewsId, setDeletingNewsId] = useState<string | null>(null)
  return { deletingGalleryImageId, setDeletingGalleryImageId, deletingNewsId, setDeletingNewsId }
}

export function useProjectDialogs(): ProjectDialogsState {
  const [isTaskDialogOpen, setIsTaskDialogOpen] = useState(false)
  const [isInviteDialogOpen, setIsInviteDialogOpen] = useState(false)
  const [isRequestDialogOpen, setIsRequestDialogOpen] = useState(false)
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false)
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false)
  const [deleteConfirmText, setDeleteConfirmText] = useState("")
  const [isSettingsDialogOpen, setIsSettingsDialogOpen] = useState(false)
  const [isAddNewsDialogOpen, setIsAddNewsDialogOpen] = useState(false)
  const [isUploadMediaDialogOpen, setIsUploadMediaDialogOpen] = useState(false)

  const { confirmAction, setConfirmAction, handleCloseConfirmActionDialog } = useConfirmActionState()
  const { deletingGalleryImageId, setDeletingGalleryImageId, deletingNewsId, setDeletingNewsId } = useDeletionTargetState()

  const handleOpenSettingsDialog = useCallback(() => setIsSettingsDialogOpen(true), [])
  const handleOpenEditDialog = useCallback(() => setIsEditDialogOpen(true), [])
  const handleOpenDeleteDialog = useCallback(() => setIsDeleteDialogOpen(true), [])
  const handleCloseEditDialog = useCallback(() => setIsEditDialogOpen(false), [])
  const handleCloseInviteDialog = useCallback(() => setIsInviteDialogOpen(false), [])

  const handleCloseDeleteProjectDialog = useCallback((open: boolean) => {
    setIsDeleteDialogOpen(open)
    if (!open) setDeleteConfirmText("")
  }, [])

  const handleDeleteConfirmTextChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => setDeleteConfirmText(e.target.value),
    []
  )

  const handleCancelDeleteProject = useCallback(() => setDeleteConfirmText(""), [])

  return {
    // Task
    isTaskDialogOpen,
    setIsTaskDialogOpen,

    // Invite
    isInviteDialogOpen,
    setIsInviteDialogOpen,

    // Request
    isRequestDialogOpen,
    setIsRequestDialogOpen,

    // Edit
    isEditDialogOpen,
    setIsEditDialogOpen,

    isDeleteDialogOpen,
    setIsDeleteDialogOpen,
    deleteConfirmText,
    setDeleteConfirmText,

    isSettingsDialogOpen,
    setIsSettingsDialogOpen,

    // News
    isAddNewsDialogOpen,
    setIsAddNewsDialogOpen,

    // Media
    isUploadMediaDialogOpen,
    setIsUploadMediaDialogOpen,

    // Confirm action
    confirmAction,
    setConfirmAction,

    deletingGalleryImageId,
    setDeletingGalleryImageId,
    deletingNewsId,
    setDeletingNewsId,

    // Handlers
    handleOpenSettingsDialog,
    handleOpenEditDialog,
    handleOpenDeleteDialog,
    handleCloseEditDialog,
    handleCloseInviteDialog,
    handleCloseDeleteProjectDialog,
    handleDeleteConfirmTextChange,
    handleCancelDeleteProject,
    handleCloseConfirmActionDialog,
  }
}
