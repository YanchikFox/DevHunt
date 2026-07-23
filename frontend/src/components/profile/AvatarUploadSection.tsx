"use client"

import { useState, useRef, useCallback } from "react"
import { useTranslations } from "next-intl"
import { useUploadAvatar, useDeleteAvatar } from "@/lib/api/queries/profile"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { useToast } from "@/hooks/use-toast"
import { Upload, Trash2, Save } from "lucide-react"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"

/**
 * Props for the AvatarUploadSection component.
 */
export interface AvatarUploadSectionProps {
  /** User profile data */
  profile: {
    fullName?: string
    email: string
    avatarUrl?: string
  }
}

function getAvatarErrorMessage(error: unknown): string | null {
  if (typeof error !== "object" || error === null) return null

  if ("userMessage" in error && typeof error.userMessage === "string") return error.userMessage
  if ("message" in error && typeof error.message === "string") return error.message

  return null
}

/**
 * A component for uploading and managing user avatar.
 * Allows selecting an image file, previewing it, uploading, and deleting the current avatar.
 *
 * @example
 * ```tsx
 * <AvatarUploadSection
 *   profile={{
 *     fullName: "John Doe",
 *     email: "john@example.com",
 *     avatarUrl: "https://example.com/avatar.jpg"
 *   }}
 * />
 * ```
 */
export function AvatarUploadSection({ profile }: AvatarUploadSectionProps) {
  const t = useTranslations()
  const tAvatar = useTranslations("avatarUpload")
  const { toast } = useToast()
  const uploadAvatar = useUploadAvatar()
  const deleteAvatar = useDeleteAvatar()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [avatarPreview, setAvatarPreview] = useState<string | null>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)

  const handleChooseImage = useCallback(() => {
    fileInputRef.current?.click()
  }, [])

  const handleFileSelect = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return

    if (!file.type.startsWith("image/")) {
      toast({
        title: t("common.error"),
        description: tAvatar("selectImageFile"),
        variant: "destructive",
      })
      return
    }

    // Validate file size (5MB max)
    if (file.size > 5 * 1024 * 1024) {
      toast({
        title: t("common.error"),
        description: tAvatar("imageTooLarge"),
        variant: "destructive",
      })
      return
    }

    setSelectedFile(file)

    const reader = new FileReader()
    reader.onloadend = () => {
      if (typeof reader.result === "string") {
        setAvatarPreview(reader.result)
      }
    }
    reader.readAsDataURL(file)
  }, [toast, t, tAvatar])

  const handleUploadAvatar = useCallback(async () => {
    if (!selectedFile) return

    try {
      await uploadAvatar.mutateAsync(selectedFile)
      toast({
        title: t("common.success"),
        description: tAvatar("avatarUploaded"),
      })
      setSelectedFile(null)
      setAvatarPreview(null)
    } catch (error: unknown) {
      toast({
        title: t("common.error"),
        description: getAvatarErrorMessage(error) || tAvatar("avatarUploadFailed"),
        variant: "destructive",
      })
    }
  }, [selectedFile, uploadAvatar, toast, t, tAvatar])

  const handleDeleteAvatar = useCallback(async () => {
    try {
      await deleteAvatar.mutateAsync()
      toast({
        title: t("common.success"),
        description: tAvatar("avatarDeleted"),
      })
      setAvatarPreview(null)
      setSelectedFile(null)
    } catch (error: unknown) {
      toast({
        title: t("common.error"),
        description: getAvatarErrorMessage(error) || tAvatar("avatarDeleteFailed"),
        variant: "destructive",
      })
    }
  }, [deleteAvatar, toast, t, tAvatar])

  const getInitials = () => {
    if (profile?.fullName) {
      return profile.fullName
        .split(" ")
        .map((n) => n[0])
        .join("")
        .toUpperCase()
        .slice(0, 2)
    }
    return profile?.email[0].toUpperCase() || "U"
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{tAvatar("profilePicture")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <div className="flex items-center gap-6">
          <Avatar className="h-24 w-24">
            <AvatarImage
              src={avatarPreview || profile.avatarUrl}
              alt={profile.fullName || profile.email}
            />
            <AvatarFallback className="bg-primary/10 text-2xl font-bold text-primary">
              {getInitials()}
            </AvatarFallback>
          </Avatar>

          <div className="flex gap-2">
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              className="hidden"
              onChange={handleFileSelect}
            />
            <Button type="button" variant="outline" onClick={handleChooseImage}>
              <Upload className="h-4 w-4 mr-2" />
              {tAvatar("chooseImage")}
            </Button>

            {selectedFile && (
              <Button
                type="button"
                onClick={handleUploadAvatar}
                disabled={uploadAvatar.isPending}
              >
                <Save className="h-4 w-4 mr-2" />
                {uploadAvatar.isPending ? tAvatar("uploading") : tAvatar("upload")}
              </Button>
            )}

            {(profile.avatarUrl || avatarPreview) && (
              <Button
                type="button"
                variant="destructive"
                onClick={handleDeleteAvatar}
                disabled={deleteAvatar.isPending}
              >
                <Trash2 className="h-4 w-4 mr-2" />
                {t("common.delete")}
              </Button>
            )}
          </div>
        </div>
        <p className="text-sm text-muted-foreground">
          {tAvatar("recommendation")}
        </p>
      </CardContent>
    </Card>
  )
}
