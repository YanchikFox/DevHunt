import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { apiClient } from "../client"

export interface ProjectDocument {
  id: string
  title: string
  content?: string
  contentPreview?: string
  documentType: string | null
  contentFormat: string
  path: string | null
  sortOrder: number
  isPublic: boolean
  viewsCount: number
  createdAt: string
  updatedAt: string
  author: {
    id: string
    fullName: string
    avatarUrl: string | null
  }
}

export interface ProjectDocumentListResponse {
  data: ProjectDocument[]
  pagination: {
    page: number
    pageSize: number
    total: number
    totalPages: number
    hasNext: boolean
    hasPrevious: boolean
  }
}

export interface CreateDocumentRequest {
  title: string
  content: string
  documentType?: string
  contentFormat?: string
  path?: string
  sortOrder?: number
  isPublic?: boolean
}

export interface UpdateDocumentRequest {
  title?: string
  content?: string
  documentType?: string
  contentFormat?: string
  path?: string
  sortOrder?: number
  isPublic?: boolean
}

export function useProjectDocuments(projectId: string) {
  return useQuery({
    queryKey: ["projects", projectId, "documents"],
    queryFn: async () => {
      const response = await apiClient.get<ProjectDocumentListResponse>(`/projects/${projectId}/docs`)
      return response.data.data
    },
    enabled: !!projectId,
  })
}

export function useProjectDocument(projectId: string, documentId: string | null) {
  return useQuery({
    queryKey: ["projects", projectId, "documents", documentId],
    queryFn: async () => {
      if (!documentId) return null
      const response = await apiClient.get<ProjectDocument>(
        `/projects/${projectId}/docs/${documentId}`
      )
      return response.data
    },
    enabled: !!projectId && !!documentId,
  })
}

export function useCreateDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      data,
    }: {
      projectId: string
      data: CreateDocumentRequest
    }) => {
      const response = await apiClient.post<ProjectDocument>(
        `/projects/${projectId}/docs`,
        data
      )
      return response.data
    },
    onSuccess: (_, { projectId }) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "documents"] })
    },
  })
}

export function useUpdateDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      documentId,
      data,
    }: {
      projectId: string
      documentId: string
      data: UpdateDocumentRequest
    }) => {
      const response = await apiClient.put<ProjectDocument>(
        `/projects/${projectId}/docs/${documentId}`,
        data
      )
      return response.data
    },
    onSuccess: (_, { projectId, documentId }) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "documents"] })
      queryClient.invalidateQueries({
        queryKey: ["projects", projectId, "documents", documentId],
      })
    },
  })
}

export function useDeleteDocument() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({
      projectId,
      documentId,
    }: {
      projectId: string
      documentId: string
    }) => {
      await apiClient.delete(`/projects/${projectId}/docs/${documentId}`)
    },
    onSuccess: (_, { projectId }) => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "documents"] })
    },
  })
}

// ── AI Generation hooks (replacing passport artifacts) ──

export const AI_DOCUMENT_TYPES = [
  "ai-overview", "ai-tech-stack", "ai-architecture",
  "ai-roadmap", "ai-decisions", "ai-diagram",
] as const

export type AiDocumentType = typeof AI_DOCUMENT_TYPES[number]

export function isAiDocument(type: string | null): boolean {
  return type != null && (AI_DOCUMENT_TYPES as readonly string[]).includes(type)
}

interface GeneratePassportDocsResponse {
  documents: ProjectDocument[]
  provider: string | null
  model: string | null
}

export function useGeneratePassportDocs(projectId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      const response = await apiClient.post<GeneratePassportDocsResponse>(
        `/projects/${projectId}/docs/generate-passport`
      )
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "documents"] })
    },
  })
}

interface GenerateDiagramDocRequest {
  techStack: string
  idea?: string
  diagramType?: string
  projectContext?: string
}

interface GenerateDiagramDocResponse {
  document: ProjectDocument
}

export function useGenerateDiagramDoc(projectId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (data: GenerateDiagramDocRequest) => {
      const response = await apiClient.post<GenerateDiagramDocResponse>(
        `/projects/${projectId}/docs/generate-diagram`,
        data
      )
      return response.data
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["projects", projectId, "documents"] })
    },
  })
}
