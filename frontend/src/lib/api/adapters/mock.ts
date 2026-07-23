import { delay } from "../client"
import {
  mockUsers,
  mockProjects,
  mockTeamMembers,
  mockTasks,
  mockInternships,
  mockNotifications,
} from "@/mocks/data"
import type {
  User,
  Project,
  TeamMember,
  Invitation,
  Task,
  Internship,
  Notification,
  LoginRequest,
  RegisterRequest,
  AuthResponse,
} from "../schema"

/**
 * Mock API adapters - simulate API calls with delay
 */
export const mockAuth = {
  login: async (data: LoginRequest): Promise<AuthResponse> => {
    await delay(800)
    const user = mockUsers.find((u) => u.email === data.email)
    if (!user || data.password !== "password123") {
      throw new Error("Invalid credentials")
    }
    return {
      accessToken: `mock-jwt-token-${user.id}`,
      refreshToken: `mock-refresh-token-${user.id}`,
      email: user.email,
      userId: user.id,
    }
  },

  register: async (data: RegisterRequest): Promise<AuthResponse> => {
    await delay(1000)
    const newUser: User = {
      id: String(mockUsers.length + 1),
      email: data.email,
      name: data.fullName,
      role: "participant",
      skills: [],
      language: "pl",
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }
    return {
      accessToken: `mock-jwt-token-${newUser.id}`,
      refreshToken: `mock-refresh-token-${newUser.id}`,
      email: newUser.email,
      userId: newUser.id,
    }
  },
}

export const mockProjectsApi = {
  list: async (params?: { status?: string; search?: string }): Promise<Project[]> => {
    await delay(600)
    let projects = [...mockProjects]
    if (params?.status) {
      projects = projects.filter((p) => p.status === params.status)
    }
    if (params?.search) {
      const search = params.search.toLowerCase()
      projects = projects.filter(
        (p) =>
          p.title.toLowerCase().includes(search) || p.description.toLowerCase().includes(search)
      )
    }
    return projects
  },

  getById: async (id: string): Promise<Project> => {
    await delay(400)
    const project = mockProjects.find((p) => p.id === id)
    if (!project) {
      throw new Error("Project not found")
    }
    return project
  },

  create: async (data: Partial<Project>): Promise<Project> => {
    await delay(800)
    const newProject: Project = {
      id: String(mockProjects.length + 1),
      title: data.title || "Untitled Project",
      description: data.description || "",
      technologies: data.technologies || [],
      requiredRoles: data.requiredRoles || [],
      status: data.status || "draft",
      visibility: data.visibility || "public",
      ownerId: data.ownerId || "1",
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      ...data,
    }
    return newProject
  },

  update: async (id: string, data: Partial<Project>): Promise<Project> => {
    await delay(600)
    const project = mockProjects.find((p) => p.id === id)
    if (!project) {
      throw new Error("Project not found")
    }
    return { ...project, ...data, updatedAt: new Date().toISOString() }
  },
}

export const mockTeamsApi = {
  getMembers: async (projectId: string): Promise<TeamMember[]> => {
    await delay(400)
    return mockTeamMembers.filter((m) => m.projectId === projectId)
  },

  search: async (params: {
    skills?: string[]
    experienceLevel?: string
    timezone?: string
  }): Promise<User[]> => {
    await delay(600)
    let users = [...mockUsers]
    if (params.skills && params.skills.length > 0) {
      users = users.filter((u) => params.skills?.some((skill) => u.skills.includes(skill)) ?? false)
    }
    if (params.experienceLevel) {
      users = users.filter((u) => u.experienceLevel === params.experienceLevel)
    }
    return users
  },
}

export const mockInvitationsApi = {
  create: async (data: Partial<Invitation>): Promise<Invitation> => {
    await delay(700)
    if (!data.projectId || !data.inviteeId) {
      throw new Error("Missing required fields: projectId or inviteeId")
    }
    return {
      id: String(Date.now()),
      projectId: data.projectId,
      inviteeId: data.inviteeId,
      inviterId: data.inviterId || "1",
      type: data.type || "invite",
      role: data.role || "developer",
      message: data.message,
      status: "pending",
      createdAt: new Date().toISOString(),
    }
  },
}

export const mockTasksApi = {
  list: async (projectId: string): Promise<Task[]> => {
    await delay(400)
    return mockTasks.filter((t) => t.projectId === projectId)
  },

  create: async (data: Partial<Task>): Promise<Task> => {
    await delay(600)
    if (!data.projectId) {
      throw new Error("Missing required field: projectId")
    }
    return {
      id: String(Date.now()),
      projectId: data.projectId,
      title: data.title || "Untitled Task",
      description: data.description,
      status: data.status || "todo",
      priority: data.priority || "medium",
      assigneeId: data.assigneeId,
      positionInColumn: data.positionInColumn ?? 0,
      linkCount: data.linkCount ?? 0,
      attachmentCount: data.attachmentCount ?? 0,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }
  },

  update: async (id: string, data: Partial<Task>): Promise<Task> => {
    await delay(400)
    const task = mockTasks.find((t) => t.id === id)
    if (!task) {
      throw new Error("Task not found")
    }
    return { ...task, ...data, updatedAt: new Date().toISOString() }
  },

  delete: async (id: string): Promise<void> => {
    await delay(400)
    const index = mockTasks.findIndex((t) => t.id === id)
    if (index === -1) {
      throw new Error("Task not found")
    }
    mockTasks.splice(index, 1)
  },
}

export const mockInternshipsApi = {
  list: async (params?: { type?: string; isRemote?: boolean }): Promise<Internship[]> => {
    await delay(500)
    let internships = [...mockInternships]
    if (params?.type) {
      internships = internships.filter((i) => i.type === params.type)
    }
    if (params?.isRemote !== undefined) {
      internships = internships.filter((i) => i.isRemote === params.isRemote)
    }
    return internships
  },

  getById: async (id: string): Promise<Internship> => {
    await delay(300)
    const internship = mockInternships.find((i) => i.id === id)
    if (!internship) {
      throw new Error("Internship not found")
    }
    return internship
  },
}

export const mockNotificationsApi = {
  list: async (userId: string): Promise<Notification[]> => {
    await delay(300)
    return mockNotifications.filter((n) => n.userId === userId)
  },

  markAsRead: async (_id: string): Promise<void> => {
    await delay(200)
    // Mock: in real app, this would update the notification
  },
}
