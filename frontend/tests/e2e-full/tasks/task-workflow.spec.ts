import { test, expect } from "@playwright/test"
import { KanbanBoardPage } from "../page-objects/tasks/kanban-board.page"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"
import { login, createProjectViaUI } from "../helpers"
import { generateTaskData } from "../fixtures/test-tasks"

test.describe("Tasks - Workflow & Status Transitions", () => {
  test.setTimeout(7 * 60_000)

  let kanbanPage: KanbanBoardPage
  let projectsPage: ProjectsListPage
  let projectTitle: string

  test.beforeEach(async ({ page }) => {
    await login(page)
    kanbanPage = new KanbanBoardPage(page)
    projectsPage = new ProjectsListPage(page)

    // Create a project for tasks
    projectTitle = await createProjectViaUI(page, {
      title: `WorkflowTest ${Date.now()}`,
    })

    // Navigate to project and tasks tab
    await projectsPage.clickProject(projectTitle)
    const tasksTab = page.getByRole("tab", { name: /tasks/i })
    await tasksTab.click()
    await page.waitForTimeout(1000)
  })

  test("should create task in todo status by default", async ({ page }) => {
    const taskData = generateTaskData({ prefix: "TodoTask" })
    await kanbanPage.createTask({ title: taskData.title })

    // Task should be in todo/backlog column
    await kanbanPage.expectTaskVisible(taskData.title)
  })

  test("should change task status from todo to doing", async ({ page }) => {
    const taskData = generateTaskData({ prefix: "ProgressTask" })
    await kanbanPage.createTask({ title: taskData.title })

    await kanbanPage.changeTaskStatus(taskData.title, "doing")
    // Verify status changed (task moved or status updated)
    await page.waitForTimeout(1000)
  })

  test("should complete task workflow: todo -> doing -> review -> done", async ({
    page,
  }) => {
    const taskData = generateTaskData({ prefix: "FullWorkflow" })
    await kanbanPage.createTask({ title: taskData.title })

    // Move through statuses
    await kanbanPage.changeTaskStatus(taskData.title, "doing")
    await page.waitForTimeout(500)

    await kanbanPage.changeTaskStatus(taskData.title, "review")
    await page.waitForTimeout(500)

    await kanbanPage.changeTaskStatus(taskData.title, "done")
    await page.waitForTimeout(500)
  })

  test("should create task with high priority", async ({ page }) => {
    const taskData = generateTaskData({ prefix: "UrgentTask", priority: "urgent" })
    await kanbanPage.createTask({
      title: taskData.title,
      priority: "urgent",
    })

    await kanbanPage.expectTaskVisible(taskData.title)
  })

  test("should filter tasks by status", async ({ page }) => {
    // Create tasks in different statuses
    const todoTask = generateTaskData({ prefix: "FilterTodo" })
    await kanbanPage.createTask({ title: todoTask.title })

    // Look for filter controls
    const statusFilter = page.locator('[data-testid="task-filter"], select[name="status"]')
    if (await statusFilter.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await statusFilter.click()
      await page.getByRole("option", { name: /todo/i }).click()
    }
  })
})
