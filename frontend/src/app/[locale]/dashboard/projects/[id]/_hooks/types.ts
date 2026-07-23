import type { Task as TaskType } from "@/hooks/use-project-task-handlers"
import type { toast as toastFn } from "@/hooks/use-toast"
import type { ProjectNewsItem } from "@/lib/api/queries/news"
import type { PaginationMetadata, UserActivityFeedItem } from "@/lib/api/queries/profile"
import type { ProjectMediaItem, GalleryImage } from "@/hooks/use-gallery-lightbox"

export type TranslateFn = (key: string, values?: Record<string, string | number>) => string
export type ToastFn = (options: Parameters<typeof toastFn>[0]) => ReturnType<typeof toastFn>

export type Task = TaskType
export type { ProjectNewsItem, ProjectMediaItem, GalleryImage }

export interface TeamMember {
  Id?: string
  id?: string
  UserId?: string
  userId?: string
  FullName?: string
  fullName?: string
  Email?: string
  email?: string
  Role?: string
  role?: string
  canPublishNews?: boolean
  CanPublishNews?: boolean
  canManageTasks?: boolean
  CanManageTasks?: boolean
  canManageFiles?: boolean
  CanManageFiles?: boolean
  canManageGallery?: boolean
  CanManageGallery?: boolean
}

export interface ApiError {
  userMessage?: string
  message?: string
}

export interface Project {
  id: string
  title: string
  description: string
  detailedDescription?: string
  status: string
  visibility: string
  ownerId: string
  technologies: string[]
  requiredRoles: string[]
  createdAt: string
  updatedAt?: string
  maxTeamSize?: number | null
  team?: TeamMember[]
  tasks?: TaskType[]
}

export type ProjectWithExtras = Project & {
  news?: ProjectNewsItem[]
  media?: ProjectMediaItem[]
}

export type ProjectActivityPage = {
  data?: UserActivityFeedItem[]
  pagination?: Partial<PaginationMetadata> & { hasNext?: boolean; page?: number }
}

export interface ProjectPermissions {
  canView: boolean
  canEdit: boolean
  canDelete: boolean
  canManageTeam: boolean
  canViewTasks: boolean
  canPublishNews: boolean
  canManageTasks: boolean
  canManageFiles: boolean
  canManageGallery: boolean
  role: "owner" | "leader" | "member" | null
}
