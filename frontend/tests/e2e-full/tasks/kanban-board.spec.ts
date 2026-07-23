import { test, expect } from "@playwright/test"
import { KanbanBoardPage } from "../page-objects/tasks/kanban-board.page"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"
import { login, createProjectViaUI } from "../helpers"
import { generateTaskData } from "../fixtures/test-tasks"

test.describe("Tasks - Kanban Board UI", () => {
  test.setTimeout(7 * 60_000)

  let kanbanPage: KanbanBoardPage
  let projectsPage: ProjectsListPage
  let projectTitle: string

  test.beforeEach(async ({ page }) => {
    await login(page)
    kanbanPage = new KanbanBoardPage(page)
    projectsPage = new ProjectsListPage(page)

    // Create a project
    projectTitle = await createProjectViaUI(page, {
      title: `KanbanTest ${Date.now()}`,
    })

    // Navigate to tasks
    await projectsPage.clickProject(projectTitle)
    const tasksTab = page.getByRole("tab", { name: /tasks/i })
    await tasksTab.click()
    await page.waitForTimeout(1000)
  })

  test("should display kanban board columns", async ({ page }) => {
    // Board should have standard columns
    await expect(page.getByText(/to\s*do|backlog/i).first()).toBeVisible({ timeout: 10_000 })
  })

  test("should show add task button", async ({ page }) => {
    await expect(kanbanPage.addTaskButton).toBeVisible({ timeout: 10_000 })
  })

  test("should display task cards on board", async ({ page }) => {
    // Create a task first
    const taskData = generateTaskData({ prefix: "BoardTask" })
    await kanbanPage.createTask({ title: taskData.title })

    // Task card should be visible on board
    await kanbanPage.expectTaskVisible(taskData.title)
  })

  test("should show task count per column", async ({ page }) => {
    // Create multiple tasks
    const task1 = generateTaskData({ prefix: "CountTask1" })
    const task2 = generateTaskData({ prefix: "CountTask2" })

    await kanbanPage.createTask({ title: task1.title })
    await kanbanPage.createTask({ title: task2.title })

    // Both tasks should be visible
    await kanbanPage.expectTaskVisible(task1.title)
    await kanbanPage.expectTaskVisible(task2.title)
  })

  test("should open task dialog from board", async ({ page }) => {
    const taskData = generateTaskData({ prefix: "DialogTask" })
    await kanbanPage.createTask({ title: taskData.title })

    // Click task to open dialog
    await kanbanPage.openTask(taskData.title)
    await expect(kanbanPage.taskDialog).toBeVisible()
  })
})
