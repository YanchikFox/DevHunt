/**
 * Export all Page Objects
 */

// Base
export { BasePage } from "./base.page"

// Auth
export { LoginPage } from "./auth/login.page"
export { RegisterPage } from "./auth/register.page"
export { ForgotPasswordPage } from "./auth/forgot-password.page"

// Projects
export { ProjectsListPage } from "./projects/projects-list.page"
export { CreateProjectDialog } from "./projects/create-project-dialog"
export { ProjectDetailPage } from "./projects/project-detail.page"

// Tasks
export { KanbanBoardPage } from "./tasks/kanban-board.page"

// Social
export { ProfilePage } from "./social/profile.page"
export { UserSearchPage } from "./social/user-search.page"

// Realtime
export { ChatPage } from "./realtime/chat.page"
export { NotificationsPage } from "./realtime/notifications.page"
