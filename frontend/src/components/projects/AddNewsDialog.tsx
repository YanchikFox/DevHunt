"use client"

import { useState, useRef, useCallback } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Globe, Image as ImageIcon, Loader2, Lock, Pin, Users, X } from "lucide-react"
import { useTranslations } from "next-intl"
import Image from "next/image"

import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormMessage,
} from "@/components/ui/form"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { useToast } from "@/hooks/use-toast"
import { useCreateNews } from "@/lib/api/queries/news"
import { apiClient } from "@/lib/api/client"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { useProfile } from "@/lib/api/queries/profile"
import { cn } from "@/lib/utils"
import type { UseFormReturn } from "react-hook-form"

interface VisibilityButtonProps {
  opt: { value: string; label: string; icon: React.ElementType; color: string }
  isSelected: boolean
  onChange: (value: string) => void
}

function VisibilityButton({ opt, isSelected, onChange }: VisibilityButtonProps) {
  const handleClick = useCallback(() => onChange(opt.value), [onChange, opt.value])
  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        "flex items-center gap-1 px-2 py-0.5 rounded text-xs transition-all",
        isSelected ? `${opt.color} bg-background/50` : "text-muted-foreground hover:text-foreground"
      )}
    >
      <opt.icon className="h-3 w-3" />
      <span>{opt.label}</span>
    </button>
  )
}

interface PinToggleButtonProps {
  value: boolean
  onChange: (v: boolean) => void
  label: string
}

function PinToggleButton({ value, onChange, label }: PinToggleButtonProps) {
  const handleClick = useCallback(() => onChange(!value), [onChange, value])
  return (
    <Button
      type="button"
      variant="ghost"
      size="sm"
      onClick={handleClick}
      className={cn(
        "transition-colors",
        value
          ? "text-amber-400 bg-amber-500/10 hover:bg-amber-500/20"
          : "text-muted-foreground hover:text-amber-400 hover:bg-amber-500/10"
      )}
    >
      <Pin className="h-5 w-5 mr-1" />
      <span className="text-sm">{label}</span>
    </Button>
  )
}

interface ImagePreviewItemProps {
  url: string
  index: number
  total: number
  onRemove: (index: number) => void
}

function ImagePreviewItem({ url, index, total, onRemove }: ImagePreviewItemProps) {
  const handleRemove = useCallback(() => onRemove(index), [onRemove, index])
  return (
    <div
      className={cn(
        "relative group bg-muted",
        total === 1 ? "aspect-video" : total === 3 && index === 0 ? "row-span-2 aspect-[3/4]" : "aspect-square"
      )}
    >
      <Image src={url} alt={`Preview ${index + 1}`} fill className="object-cover" />
      <button
        type="button"
        onClick={handleRemove}
        className="absolute top-2 right-2 p-1.5 rounded-full bg-black/60 text-white opacity-0 group-hover:opacity-100 transition-opacity hover:bg-black/80"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  )
}

const createNewsSchema = (t: (key: string) => string) => z.object({
  title: z.string().min(3, t("news.titleMinLength")),
  content: z.string().min(10, t("news.contentMinLength")),
  visibility: z.enum(["public", "subscribers", "members"]),
  isPinned: z.boolean(),
})

type NewsFormValues = z.infer<ReturnType<typeof createNewsSchema>>

async function uploadNewsImages(selectedImages: File[], projectId: string): Promise<string[]> {
  if (selectedImages.length === 0) return []
  const results = await Promise.allSettled(
    selectedImages.map(async (file) => {
      const formData = new FormData()
      formData.append("file", file)
      formData.append("Category", "news")
      const response = await apiClient.post(`/projects/${projectId}/files`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
      })
      return response.data?.id as string
    })
  )
  const uploadedIds: string[] = []
  for (const result of results) {
    if (result.status === "fulfilled" && result.value) {
      uploadedIds.push(result.value)
    } else if (result.status === "rejected") {
      console.error("Failed to upload image:", result.reason)
    }
  }
  return uploadedIds
}

type ProfileData = ReturnType<typeof useProfile>["data"]

function NewsFormContent({
  form, profile, avatarInitial, visibilityOptions, imagePreviewUrls, removeImage, t,
}: {
  form: UseFormReturn<NewsFormValues>
  profile: ProfileData; avatarInitial: string
  visibilityOptions: { value: string; label: string; icon: React.ElementType; color: string }[]
  imagePreviewUrls: string[]; removeImage: (index: number) => void
  t: ReturnType<typeof useTranslations>
}) {
  return (
    <>
      <div className="flex items-center gap-3 px-4 py-3">
        <Avatar className="h-10 w-10">
          {profile?.avatarUrl ? (
            <AvatarImage src={profile.avatarUrl} alt={profile.fullName} />
          ) : (
            <AvatarFallback className="bg-primary/10 font-bold text-primary">{avatarInitial}</AvatarFallback>
          )}
        </Avatar>
        <div>
          <div className="font-semibold text-foreground text-sm">{profile?.fullName || t("news.user")}</div>
          <div className="flex items-center gap-2">
            <FormField
              control={form.control}
              name="visibility"
              render={({ field }) => (
                <div className="flex items-center gap-1 bg-muted rounded-md px-2 py-1">
                  {visibilityOptions.map((opt) => (
                    <VisibilityButton key={opt.value} opt={opt} isSelected={field.value === opt.value} onChange={field.onChange} />
                  ))}
                </div>
              )}
            />
          </div>
        </div>
      </div>
      <div className="px-4">
        <FormField
          control={form.control}
          name="title"
          render={({ field }) => (
            <FormItem>
              <FormControl>
                <Input
                  placeholder={t("news.titlePlaceholder")}
                  className="border-0 bg-transparent text-lg font-semibold text-foreground placeholder:text-muted-foreground focus-visible:ring-0 px-0"
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
      <div className="px-4">
        <FormField
          control={form.control}
          name="content"
          render={({ field }) => (
            <FormItem>
              <FormControl>
                <Textarea
                  placeholder={t("news.contentPlaceholder")}
                  className="border-0 bg-transparent text-foreground placeholder:text-muted-foreground focus-visible:ring-0 px-0 min-h-[120px] resize-none"
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </div>
      {imagePreviewUrls.length > 0 && (
        <div className="px-4 pb-3">
          <div className={cn(
            "grid gap-1 rounded-lg overflow-hidden",
            imagePreviewUrls.length === 1 ? "grid-cols-1" : "grid-cols-2"
          )}>
            {imagePreviewUrls.map((url, index) => (
              <ImagePreviewItem key={index} url={url} index={index} total={imagePreviewUrls.length} onRemove={removeImage} />
            ))}
          </div>
        </div>
      )}
    </>
  )
}

interface AddNewsDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  projectId: string
}

export function AddNewsDialog({ open, onOpenChange, projectId }: AddNewsDialogProps) {
  const t = useTranslations()
  const { toast } = useToast()
  const createNews = useCreateNews()
  const { data: profile } = useProfile()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [selectedImages, setSelectedImages] = useState<File[]>([])
  const [imagePreviewUrls, setImagePreviewUrls] = useState<string[]>([])
  const [isUploading, setIsUploading] = useState(false)

  const newsSchema = createNewsSchema(t)
  const form = useForm<NewsFormValues>({
    resolver: zodResolver(newsSchema),
    defaultValues: { title: "", content: "", visibility: "public", isPinned: false },
  })

  const handleOpenFileInput = useCallback(() => fileInputRef.current?.click(), [])

  const handleImageSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || [])
    if (files.length === 0) return
    const newImages = [...selectedImages, ...files].slice(0, 10)
    setSelectedImages(newImages)
    const newPreviews = newImages.map(file => URL.createObjectURL(file))
    imagePreviewUrls.forEach(url => URL.revokeObjectURL(url))
    setImagePreviewUrls(newPreviews)
    if (fileInputRef.current) fileInputRef.current.value = ""
  }

  const removeImage = (index: number) => {
    URL.revokeObjectURL(imagePreviewUrls[index])
    setSelectedImages(prev => prev.filter((_, i) => i !== index))
    setImagePreviewUrls(prev => prev.filter((_, i) => i !== index))
  }

  const onSubmit = async (data: NewsFormValues) => {
    try {
      setIsUploading(true)
      const attachmentIds = await uploadNewsImages(selectedImages, projectId)
      await createNews.mutateAsync({
        projectId,
        data: {
          title: data.title, content: data.content,
          visibility: data.visibility, isPinned: data.isPinned,
          attachmentIds: attachmentIds.length > 0 ? attachmentIds : undefined,
        },
      })
      toast({ title: t("news.success"), description: t("news.publishedSuccess") })
      imagePreviewUrls.forEach(url => URL.revokeObjectURL(url))
      setSelectedImages([])
      setImagePreviewUrls([])
      form.reset()
      onOpenChange(false)
    } catch (error) {
      console.error("Failed to publish news:", error)
      toast({ title: t("news.error"), description: t("news.publishFailed"), variant: "destructive" })
    } finally {
      setIsUploading(false)
    }
  }

  const visibilityOptions = [
    { value: "public", label: t("news.all"), icon: Globe, color: "text-emerald-400" },
    { value: "subscribers", label: t("news.subscribers"), icon: Users, color: "text-blue-400" },
    { value: "members", label: t("news.members"), icon: Lock, color: "text-amber-400" },
  ]
  const avatarInitial = (profile?.fullName?.charAt(0) || "U").toUpperCase()

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[600px] bg-card border-border p-0 overflow-hidden">
        <DialogHeader className="p-4 pb-0">
          <DialogTitle className="text-lg font-semibold text-foreground">{t("news.newPublication")}</DialogTitle>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col">
            <NewsFormContent
              form={form} profile={profile} avatarInitial={avatarInitial}
              visibilityOptions={visibilityOptions} imagePreviewUrls={imagePreviewUrls}
              removeImage={removeImage} t={t}
            />
            <div className="flex items-center justify-between px-4 py-3 border-t border-border bg-muted/50">
              <div className="flex items-center gap-2">
                <input type="file" ref={fileInputRef} accept="image/*" multiple onChange={handleImageSelect} className="hidden" />
                <Button
                  type="button" variant="ghost" size="sm" onClick={handleOpenFileInput}
                  className="text-muted-foreground hover:text-primary hover:bg-primary/10"
                  disabled={selectedImages.length >= 10}
                >
                  <ImageIcon className="h-5 w-5 mr-1" />
                  <span className="text-sm">{t("news.photo")}</span>
                  {selectedImages.length > 0 && (
                    <span className="ml-1 text-xs text-muted-foreground">({selectedImages.length}/10)</span>
                  )}
                </Button>
                <FormField
                  control={form.control}
                  name="isPinned"
                  render={({ field }) => (
                    <PinToggleButton value={field.value} onChange={field.onChange} label={t("news.pin")} />
                  )}
                />
              </div>
              <Button
                type="submit"
                disabled={createNews.isPending || isUploading}
                className="bg-primary hover:bg-primary/90 text-primary-foreground px-6"
              >
                {(createNews.isPending || isUploading) && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                {t("news.publishButton")}
              </Button>
            </div>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
