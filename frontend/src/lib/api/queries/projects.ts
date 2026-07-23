import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { USE_MOCKS } from "@/lib/feature-flags"
import { mockProjectsApi } from "../adapters/mock"
import { apiClient } from "../client"
import type { Project, ProjectStatus } from "../schema"
import { mapProject } from "../adapters/backend-mappers"

type BackendProjectDto = {
  Id: string
  Title: string
  ShortDescription: string | null
  Description: string
  TechStack: string | string[]
  Status: string
  Visibility: string
  OwnerId: string
  DifficultyLevel: string | null
  ExpectedDurationDays: number | null
  ShowcasePublished: boolean
  Featured: boolean
  TeamSize: number
  MaxTeamSize: number | null
  Rating: number | null
  CreatedAt: string
  UpdatedAt: string
}

export function useProjectsList(params?: {
  status?: string
  /** @deprecated Prefer `query` (matches backend parameter name). */
  search?: string
  /** Backend: `query` */
  query?: string
  /** Backend: `tech` (supports comma-separated values) */
  tech?: string | string[]
  showcase?: boolean
  featured?: boolean
  myProjects?: boolean
  ownerId?: string
  excludeDrafts?: boolean
  sortBy?: "createdAt" | "updatedAt" | "rating" | "title" | "teamSize" | "featured"
  sortOrder?: "asc" | "desc"
}) {
  const normalizeCsv = (value?: string | string[]) => {
    if (!value) return undefined
    if (Array.isArray(value)) {
      const normalized = value
        .map((v) => (typeof v === "string" ? v.trim() : ""))
        .filter(Boolean)
      return normalized.length > 0 ? normalized.join(",") : undefined
    }
    const normalized = value
      .split(",")
      .map((v) => v.trim())
      .filter(Boolean)
    return normalized.length > 0 ? normalized.join(",") : undefined
  }

  return useQuery({
    queryKey: ["projects", "list", params],
    queryFn: async () => {
      if (USE_MOCKS) {
        // For showcase, filter projects that are published
        let allProjects = await mockProjectsApi.list(params)

        // Filter by showcase
        if (params?.showcase) {
          allProjects = allProjects.filter(
            (p) => p.status === "completed" || p.status === "in_progress"
          )
        }

        // Filter by ownerId
        if (params?.ownerId) {
          allProjects = allProjects.filter((p) => p.ownerId === params.ownerId)
        }

        // Search by query
        const q = (params?.query || params?.search || "").trim().toLowerCase()
        if (q) {
          allProjects = allProjects.filter((p) => {
            const title = (p.title || "").toLowerCase()
            const description = (p.description || "").toLowerCase()
            const detailed = (p.detailedDescription || "").toLowerCase()
            return title.includes(q) || description.includes(q) || detailed.includes(q)
          })
        }

        // Filter by tech
        const techCsv = normalizeCsv(params?.tech)
        if (techCsv) {
          const techSet = new Set(
            techCsv
              .split(",")
              .map((t) => t.trim().toLowerCase())
              .filter(Boolean)
          )
          allProjects = allProjects.filter((p) => {
            const technologies = Array.isArray(p.technologies) ? p.technologies : []
            return technologies.some((t) => techSet.has(String(t).trim().toLowerCase()))
          })
        }

        // Exclude drafts
        if (params?.excludeDrafts) {
          allProjects = allProjects.filter((p) => p.status !== "draft")
        }

        // Sorting
        const sortBy = params?.sortBy || "createdAt"
        const ascending = (params?.sortOrder || "desc") === "asc"
        allProjects = allProjects.slice().sort((a, b) => {
          const aDate = (sortBy === "updatedAt" ? a.updatedAt : a.createdAt) || ""
          const bDate = (sortBy === "updatedAt" ? b.updatedAt : b.createdAt) || ""
          if (sortBy === "createdAt" || sortBy === "updatedAt") {
            const diff = new Date(aDate).getTime() - new Date(bDate).getTime()
            return ascending ? diff : -diff
          }
          if (sortBy === "title") {
            const diff = String(a.title || "").localeCompare(String(b.title || ""))
            return ascending ? diff : -diff
          }
          return 0
        })

        return allProjects
      }
      // Backend endpoint: GET /api/projects (use `showcase=true` query param for showcase filtering)
      const endpoint = "/projects"
      type BackendPaginatedResponse = {
        data: BackendProjectDto[]
        pagination: {
          page: number
          pageSize: number
          total: number
          totalPages: number
          hasNext: boolean
          hasPrevious: boolean
        }
      }
      const { excludeDrafts, myProjects, ownerId, search, query, tech, ...queryParams } = params || {}
      const queryText = (query || search || undefined) as string | undefined
      const techCsv = normalizeCsv(tech)
      const response = await apiClient.get<BackendPaginatedResponse>(endpoint, {
        params: {
          ...queryParams,
          ...(queryText ? { query: queryText } : {}),
          ...(techCsv ? { tech: techCsv } : {}),
          ...(excludeDrafts ? { excludeDrafts: true } : {}),
          ...(myProjects ? { myProjects: true } : {}),
          ...(ownerId ? { ownerId } : {}),
        },
      })
      // Return only the data array, not the pagination wrapper
      return response.data.data.map(mapProject)
    },
  })
}

export function useProject(id: string) {
  return useQuery({
    queryKey: ["projects", id],
    queryFn: async () => {
      if (USE_MOCKS) {
        return await mockProjectsApi.getById(id)
      }
      // Backend endpoint: GET /api/projects/{id}
      const response = await apiClient.get<BackendProjectDto>(`/projects/${id}`)
      return mapProject(response.data)
    },
    enabled: Boolean(id),
  })
}

export function useCreateProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: Partial<Project>): Promise<Project> => {
      if (USE_MOCKS) {
        return await mockProjectsApi.create(data)
      }
      // Backend endpoint: POST /api/projects (apiClient recurses toPascalCase on body)
      type BackendRequest = {
        Title: string
        Description: string
        ShortDescription?: string
        TechStack: string[]
        Status: string
        Visibility: string
        DifficultyLevel?: string
        ExpectedDurationDays?: number
        MaxTeamSize?: number
        Slug?: string | null
        OpenRoles?: Array<{
          role: string
          totalNeeded: number
          hoursPerWeek: number | null
          equityOptional: boolean
        }>
      }
      const openRoles =
        data.openRoles && data.openRoles.length > 0
          ? data.openRoles.map((r) => ({
              role: r.role,
              totalNeeded: r.totalNeeded,
              hoursPerWeek: r.hoursPerWeek ?? null,
              equityOptional: r.equityOptional,
            }))
          : undefined

      const response = await apiClient.post<BackendProjectDto>("/projects", {
        Title: data.title,
        Description: data.description,
        ShortDescription: data.detailedDescription,
        TechStack: data.technologies || [],
        Status: data.status || "draft",
        Visibility: data.visibility || "public",
        DifficultyLevel: data.complexityLevel,
        ExpectedDurationDays: data.duration ? parseInt(data.duration) : undefined,
        MaxTeamSize: undefined,
        Slug: data.slug ?? undefined,
        openRoles,
      } as BackendRequest)
      return mapProject(response.data)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects"] })
    },
  })
}

/**
 * Update project
 */
export function useUpdateProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ id, data }: { id: string; data: Partial<Project> }): Promise<Project> => {
      if (USE_MOCKS) {
        return await mockProjectsApi.update(id, data)
      }
      // Backend endpoint: PUT /api/projects/{id}
      type OpenRolePayload = { Role: string; TotalNeeded: number; HoursPerWeek: number | null; EquityOptional: boolean }
      type BackendRequest = {
        Title?: string
        Description?: string
        ShortDescription?: string
        TechStack?: string[]
        RequiredRoles?: string[]
        OpenRoles?: OpenRolePayload[]
        Status?: string
        Visibility?: string
        DifficultyLevel?: string
        ExpectedDurationDays?: number
        MaxTeamSize?: number
        ShowcasePublished?: boolean
        Featured?: boolean
        Slug?: string | null
      }
      // Build request with only defined fields
      const requestBody: BackendRequest = {}
      if (data.title !== undefined) requestBody.Title = data.title
      if (data.description !== undefined) requestBody.Description = data.description
      if (data.detailedDescription !== undefined) requestBody.ShortDescription = data.detailedDescription
      if (data.technologies !== undefined) requestBody.TechStack = data.technologies
      if (data.requiredRoles !== undefined) requestBody.RequiredRoles = data.requiredRoles
      if (data.openRoles !== undefined) {
        requestBody.OpenRoles = data.openRoles.map((r) => ({
          Role: r.role,
          TotalNeeded: r.totalNeeded,
          HoursPerWeek: r.hoursPerWeek ?? null,
          EquityOptional: r.equityOptional,
        }))
      }
      if (data.status !== undefined) requestBody.Status = data.status
      if (data.visibility !== undefined) requestBody.Visibility = data.visibility
      if (data.complexityLevel !== undefined) requestBody.DifficultyLevel = data.complexityLevel
      if (data.duration !== undefined) requestBody.ExpectedDurationDays = parseInt(data.duration)
      if (data.maxTeamSize !== undefined && data.maxTeamSize !== null) requestBody.MaxTeamSize = data.maxTeamSize
      // PE-08: Only send boolean fields when explicitly provided (null = don't change)
      if (data.showcasePublished !== undefined) requestBody.ShowcasePublished = data.showcasePublished
      if (data.featured !== undefined) requestBody.Featured = data.featured
      if (data.slug !== undefined) requestBody.Slug = data.slug

      const response = await apiClient.put<BackendProjectDto>(`/projects/${id}`, requestBody)

      // Handle 204 No Content - backend was updated successfully but returned no body
      if (response.status === 204 || !response.data) {
        const updated = await apiClient.get<BackendProjectDto>(`/projects/${id}`)
        return mapProject(updated.data)
      }

      return mapProject(response.data)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.id] })
      queryClient.invalidateQueries({ queryKey: ["projects", "list"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "roles", variables.id] })
    },
  })
}

/**
 * Change project status
 */
export function useChangeProjectStatus() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ id, status }: { id: string; status: ProjectStatus }): Promise<Project> => {
      if (USE_MOCKS) {
        return await mockProjectsApi.update(id, { status })
      }
      // Backend endpoint: PATCH /api/projects/{id}/status
      const response = await apiClient.patch<BackendProjectDto>(`/projects/${id}/status`, {
        Status: status,
      })
      return mapProject(response.data)
    },
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ["projects", variables.id] })
      queryClient.invalidateQueries({ queryKey: ["projects", "list"] })
      queryClient.invalidateQueries({ queryKey: ["teams", "roles", variables.id] })
    },
  })
}

/**
 * Delete project (permanent deletion)
 * Only project owner can delete
 */
export function useDeleteProject() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (id: string): Promise<void> => {
      if (USE_MOCKS) {
        // Mock deletion - just remove from cache
        return Promise.resolve()
      }
      // Backend endpoint: DELETE /api/projects/{id}
      await apiClient.delete(`/projects/${id}`)
      return undefined
    },
    onSuccess: (_, projectId) => {
      queryClient.removeQueries({ queryKey: ["projects", projectId] })
      queryClient.invalidateQueries({ queryKey: ["projects", "list"] })
    },
  })
}

/**
 * Get project permissions for current user
 */
export function useProjectPermissions(projectId: string) {
  return useQuery({
    queryKey: ["projects", projectId, "permissions"],
    queryFn: async () => {
      if (USE_MOCKS || !projectId) {
        return {
          canView: true,
          canEdit: true,
          canDelete: true,
          canManageTeam: true,
          canViewTasks: true,
          canPublishNews: true,
          canManageTasks: true,
          canManageFiles: true,
          canManageGallery: true,
          role: "owner" as "owner" | "leader" | "member" | null,
        }
      }
      // Backend endpoint: GET /api/projects/{id}/permissions
      const response = await apiClient.get<{
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
      }>(`/projects/${projectId}/permissions`)
      return response.data
    },
    enabled: Boolean(projectId),
    staleTime: 5 * 60 * 1000, // Cache for 5 minutes
  })
}

