import { test, expect } from "@playwright/test"
import { KanbanBoardPage } from "../page-objects/tasks/kanban-board.page"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"
import { login, createProjectViaUI } from "../helpers"
import { generateTaskData } from "../fixtures/test-tasks"

test.describe("Tasks - CRUD Operations", () => {
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
      title: `TaskTest ${Date.now()}`,
    })

    // Navigate to project and tasks tab
    await projectsPage.clickProject(projectTitle)
    const tasksTab = page.getByRole("tab", { name: /tasks/i })
    await tasksTab.click()
    await page.waitForTimeout(1000)
  })

  test("should create a task with title only", async ({ page }) => {
    const taskData = generateTaskData({ prefix: "MinimalTask" })

    await kanbanPage.openCreateTaskDialog()
    await kanbanPage.taskTitleInput.fill(taskData.title)
    await kanbanPage.saveTaskButton.click()

    await kanbanPage.expectTaskVisible(taskData.title)
  })

  test("should create a task with all fields", async ({ page }) => {
    const taskData = generateTaskData({ prefix: "FullTask", priority: "high" })

    await kanbanPage.createTask({
      title: taskData.title,
      description: taskData.description,
      priority: taskData.priority,
    })

    await kanbanPage.expectTaskVisible(taskData.title)
  })

  test("should open task details when clicking on task card", async ({ page }) => {
    // First create a task
    const taskData = generateTaskData({ prefix: "ViewTask" })
    await kanbanPage.createTask({ title: taskData.title })

    // Click to open details
    await kanbanPage.openTask(taskData.title)

    await expect(kanbanPage.taskDialog).toBeVisible()
    await expect(kanbanPage.taskTitleInput).toHaveValue(taskData.title)
  })

  test("should edit task title", async ({ page }) => {
    // Create task
    const taskData = generateTaskData({ prefix: "EditTask" })
    await kanbanPage.createTask({ title: taskData.title })

    // Open and edit
    await kanbanPage.openTask(taskData.title)
    const newTitle = `Edited ${Date.now()}`
    await kanbanPage.taskTitleInput.fill(newTitle)

    // Save
    const responsePromise = page.waitForResponse(
      (res) => res.request().method() === "PUT" && /\/tasks\//.test(res.url()),
      { timeout: 60_000 }
    )
    await kanbanPage.saveTaskButton.click()
    await responsePromise

    await kanbanPage.expectTaskVisible(newTitle)
  })

  test("should cancel task creation", async ({ page }) => {
    await kanbanPage.openCreateTaskDialog()
    await kanbanPage.taskTitleInput.fill("Should Not Be Created")
    await kanbanPage.cancelTaskButton.click()

    await expect(kanbanPage.taskDialog).toBeHidden()
  })
})
