"use client"

import { useState, useEffect, useCallback } from "react"
import {
  useAdminContentProjectDetail,
  useAdminUpdateProject,
} from "@/lib/api/queries/admin-content"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Badge } from "@/components/ui/badge"
import { Loader2, Calendar, Users, ClipboardList, Newspaper } from "lucide-react"
import { useToast } from "@/hooks/use-toast"
import { useTranslations } from "next-intl"

interface EditProjectDialogProps {
  projectId: string
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function EditProjectDialog({ projectId, open, onOpenChange }: EditProjectDialogProps) {
  const t = useTranslations("admin")
  const { toast } = useToast()

  const { data: project, isLoading } = useAdminContentProjectDetail(open ? projectId : null)
  const updateProject = useAdminUpdateProject()

  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [status, setStatus] = useState("")
  const [visibility, setVisibility] = useState("")
  const [featured, setFeatured] = useState("false")

  useEffect(() => {
    if (project) {
      setTitle(project.title)
      setDescription(project.description)
      setStatus(project.status)
      setVisibility(project.visibility)
      setFeatured(project.featured ? "true" : "false")
    }
  }, [project])

  const handleSave = useCallback(async () => {
    try {
      await updateProject.mutateAsync({
        projectId,
        title,
        description,
        status,
        visibility,
        featured: featured === "true",
      })
      toast({ title: t("projectUpdated") })
      onOpenChange(false)
    } catch {
      toast({ title: t("error"), variant: "destructive" })
    }
  }, [projectId, title, description, status, visibility, featured, updateProject, toast, t, onOpenChange])

  const handleTitleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setTitle(e.target.value)
  }, [])

  const handleDescriptionChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setDescription(e.target.value)
  }, [])

  const handleCancel = useCallback(() => {
    onOpenChange(false)
  }, [onOpenChange])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{t("editProject")}</DialogTitle>
        </DialogHeader>

        {isLoading ? (
          <div className="flex justify-center py-8">
            <Loader2 className="h-6 w-6 animate-spin" />
          </div>
        ) : project ? (
          <div className="space-y-4">
            {/* Stats */}
            <div className="grid grid-cols-4 gap-3">
              <StatBox icon={<Users className="h-4 w-4" />} label={t("team")} value={project.teamCount} />
              <StatBox icon={<ClipboardList className="h-4 w-4" />} label={t("tasks")} value={project.taskCount} />
              <StatBox icon={<Newspaper className="h-4 w-4" />} label={t("news")} value={project.newsCount} />
              <StatBox icon={<Calendar className="h-4 w-4" />} label={t("created")} value={new Date(project.createdAt).toLocaleDateString()} />
            </div>

            {/* Owner info */}
            {project.owner && (
              <div className="text-sm text-muted-foreground">
                {t("owner")}: <span className="font-medium text-foreground">{project.owner.fullName || project.owner.email}</span>
              </div>
            )}

            {/* Tech stack */}
            {project.techStack.length > 0 && (
              <div className="flex flex-wrap gap-1">
                {project.techStack.map((tech) => (
                  <Badge key={tech} variant="secondary" className="text-xs">{tech}</Badge>
                ))}
              </div>
            )}

            {/* Form fields */}
            <div className="space-y-3">
              <div>
                <label className="text-sm font-medium mb-1 block">{t("title")}</label>
                <Input value={title} onChange={handleTitleChange} />
              </div>
              <div>
                <label className="text-sm font-medium mb-1 block">{t("description")}</label>
                <Textarea value={description} onChange={handleDescriptionChange} className="min-h-[100px]" />
              </div>
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="text-sm font-medium mb-1 block">{t("status")}</label>
                  <Select value={status} onValueChange={setStatus}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="active">{t("active")}</SelectItem>
                      <SelectItem value="draft">{t("draft")}</SelectItem>
                      <SelectItem value="archived">{t("archived")}</SelectItem>
                      <SelectItem value="completed">{t("completed")}</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <label className="text-sm font-medium mb-1 block">{t("visibility")}</label>
                  <Select value={visibility} onValueChange={setVisibility}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="public">{t("public")}</SelectItem>
                      <SelectItem value="private">{t("private")}</SelectItem>
                      <SelectItem value="unlisted">{t("unlisted")}</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <label className="text-sm font-medium mb-1 block">{t("featured")}</label>
                  <Select value={featured} onValueChange={setFeatured}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="true">{t("yes")}</SelectItem>
                      <SelectItem value="false">{t("no")}</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>
            </div>
          </div>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={handleCancel}>
            {t("cancel")}
          </Button>
          <Button onClick={handleSave} disabled={updateProject.isPending || !title.trim()}>
            {updateProject.isPending && <Loader2 className="h-4 w-4 animate-spin mr-1" />}
            {t("save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function StatBox({ icon, label, value }: { icon: React.ReactNode; label: string; value: number | string }) {
  return (
    <div className="text-center p-2 rounded-lg bg-muted/50">
      <div className="flex items-center justify-center gap-1 mb-1">{icon}</div>
      <div className="text-sm font-bold">{value}</div>
      <div className="text-xs text-muted-foreground">{label}</div>
    </div>
  )
}
