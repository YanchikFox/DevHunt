"use client"

import Image from "next/image"
import { useState, useRef, useCallback } from "react"
import { useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { z } from "zod"
import { Loader2, Upload, X, FileText } from "lucide-react";
import { useTranslations } from "next-intl"

import type { UseFormReturn } from "react-hook-form"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { useToast } from "@/hooks/use-toast"
import { useUploadFile } from "@/lib/api/queries/files"

const uploadSchema = z.object({
  description: z.string().optional(),
  category: z.string().min(1),
})

type UploadFormValues = z.infer<typeof uploadSchema>

interface UploadMediaDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  projectId: string
}

function FileInputSection({
  selectedFile, previewUrl, fileInputRef, onOpenFileInput, onClearFileClick, onFileSelect, tUpload,
}: {
  selectedFile: File | null; previewUrl: string | null
  fileInputRef: React.RefObject<HTMLInputElement | null>
  onOpenFileInput: () => void; onClearFileClick: (e: React.MouseEvent) => void
  onFileSelect: (e: React.ChangeEvent<HTMLInputElement>) => void
  tUpload: ReturnType<typeof useTranslations>
}) {
  return (
    <div className="space-y-2">
      <FormLabel>{tUpload("file")}</FormLabel>
      {!selectedFile ? (
        <div className="border-2 border-dashed rounded-lg p-8 text-center hover:bg-muted/50 cursor-pointer transition-colors" onClick={onOpenFileInput}>
          <Upload className="mx-auto h-8 w-8 text-muted-foreground mb-2" />
          <p className="text-sm text-muted-foreground">{tUpload("clickToSelect")}</p>
        </div>
      ) : (
        <div className="relative border rounded-lg p-4 flex items-center gap-4">
          {previewUrl ? (
            <div className="h-16 w-16 relative rounded overflow-hidden bg-muted">
              <Image fill src={previewUrl} alt="Preview" className="object-cover" />
            </div>
          ) : (
            <div className="h-16 w-16 flex items-center justify-center bg-muted rounded">
              <FileText className="h-8 w-8 text-muted-foreground" />
            </div>
          )}
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium truncate">{selectedFile.name}</p>
            <p className="text-xs text-muted-foreground">{(selectedFile.size / 1024 / 1024).toFixed(2)} MB</p>
          </div>
          <Button type="button" variant="ghost" size="icon" onClick={onClearFileClick}>
            <X className="h-4 w-4" />
          </Button>
        </div>
      )}
      <input type="file" ref={fileInputRef} className="hidden" onChange={onFileSelect} accept="image/*,video/*,.pdf,.doc,.docx" />
    </div>
  )
}

interface UploadMediaFormBodyProps {
  form: UseFormReturn<UploadFormValues>
  tUpload: ReturnType<typeof useTranslations>
  t: ReturnType<typeof useTranslations>
  selectedFile: File | null
  previewUrl: string | null
  fileInputRef: React.RefObject<HTMLInputElement | null>
  onOpenFileInput: () => void
  onClearFileClick: (e: React.MouseEvent) => void
  onFileSelect: (e: React.ChangeEvent<HTMLInputElement>) => void
  onClose: () => void
  onSubmit: (data: UploadFormValues) => Promise<void>
  isPending: boolean
}

function UploadMediaFormBody({ form, tUpload, t, selectedFile, previewUrl, fileInputRef, onOpenFileInput, onClearFileClick, onFileSelect, onClose, onSubmit, isPending }: UploadMediaFormBodyProps) {
  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
        <FileInputSection
          selectedFile={selectedFile}
          previewUrl={previewUrl}
          fileInputRef={fileInputRef}
          onOpenFileInput={onOpenFileInput}
          onClearFileClick={onClearFileClick}
          onFileSelect={onFileSelect}
          tUpload={tUpload}
        />
        <FormField
          control={form.control}
          name="category"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{tUpload("category")}</FormLabel>
              <Select onValueChange={field.onChange} defaultValue={field.value}>
                <FormControl>
                  <SelectTrigger>
                    <SelectValue placeholder={tUpload("selectCategory")} />
                  </SelectTrigger>
                </FormControl>
                <SelectContent>
                  <SelectItem value="gallery">{tUpload("galleryImage")}</SelectItem>
                  <SelectItem value="screenshot">{tUpload("screenshot")}</SelectItem>
                  <SelectItem value="design">{tUpload("designAsset")}</SelectItem>
                  <SelectItem value="document">{tUpload("document")}</SelectItem>
                </SelectContent>
              </Select>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="description"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{tUpload("descriptionOptional")}</FormLabel>
              <FormControl>
                <Input placeholder={tUpload("describeFile")} {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose} disabled={isPending}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={isPending || !selectedFile}>
            {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {t("common.upload")}
          </Button>
        </DialogFooter>
      </form>
    </Form>
  )
}

export function UploadMediaDialog({ open, onOpenChange, projectId }: UploadMediaDialogProps) {
  const t = useTranslations()
  const tUpload = useTranslations("uploadMedia")
  const { toast } = useToast()
  const uploadFile = useUploadFile()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)

  const form = useForm<UploadFormValues>({
    resolver: zodResolver(uploadSchema),
    defaultValues: {
      description: "",
      category: "gallery",
    },
  })

  const handleFileSelect = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    if (file.size > 100 * 1024 * 1024) {
      toast({ title: tUpload("fileTooLarge"), description: tUpload("maxFileSize"), variant: "destructive" })
      return
    }
    setSelectedFile(file)
    setPreviewUrl(file.type.startsWith("image/") ? URL.createObjectURL(file) : null)
  }, [toast, tUpload])

  const handleClearFile = useCallback(() => {
    setSelectedFile(null)
    if (previewUrl) {
      URL.revokeObjectURL(previewUrl)
      setPreviewUrl(null)
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = ""
    }
  }, [previewUrl])

  const handleOpenFileInput = useCallback(() => fileInputRef.current?.click(), [])
  const handleClearFileClick = useCallback((e: React.MouseEvent) => {
    e.stopPropagation()
    handleClearFile()
  }, [handleClearFile])
  const handleClose = useCallback(() => onOpenChange(false), [onOpenChange])

  const onSubmit = async (data: UploadFormValues) => {
    if (!selectedFile) {
      toast({
        title: tUpload("noFileSelected"),
        description: tUpload("pleaseSelectFile"),
        variant: "destructive",
      })
      return
    }

    try {
      await uploadFile.mutateAsync({
        projectId,
        data: {
          file: selectedFile,
          description: data.description,
          category: data.category,
        },
      })

      toast({
        title: t("common.success"),
        description: tUpload("fileUploaded"),
      })
      
      handleClearFile()
      form.reset()
      onOpenChange(false)
    } catch (error) {
      console.error("Failed to upload file:", error)
      toast({
        title: t("common.error"),
        description: tUpload("fileUploadFailed"),
        variant: "destructive",
      })
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>{tUpload("title")}</DialogTitle>
          <DialogDescription>{tUpload("description")}</DialogDescription>
        </DialogHeader>
        <UploadMediaFormBody
          form={form} tUpload={tUpload} t={t}
          selectedFile={selectedFile} previewUrl={previewUrl}
          fileInputRef={fileInputRef} onOpenFileInput={handleOpenFileInput}
          onClearFileClick={handleClearFileClick} onFileSelect={handleFileSelect}
          onClose={handleClose} onSubmit={onSubmit} isPending={uploadFile.isPending}
        />
      </DialogContent>
    </Dialog>
  )
}
