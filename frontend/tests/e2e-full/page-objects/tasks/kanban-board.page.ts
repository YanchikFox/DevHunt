import { Page, Locator, expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Kanban Board Page Object
 * Covers task board within project detail page
 */
export class KanbanBoardPage extends BasePage {
  private projectId: string = ""

  // Board elements
  get board() {
    return this.page.locator('[data-testid="kanban-board"], .kanban-board, [role="region"]')
  }

  get columns() {
    return this.page.locator('[data-testid="kanban-column"], .kanban-column')
  }

  get todoColumn() {
    return this.page.locator('[data-testid="column-todo"], :has-text("To Do"):has([data-testid="task-card"])')
  }

  get doingColumn() {
    return this.page.locator('[data-testid="column-doing"], :has-text("Doing"):has([data-testid="task-card"])')
  }

  get reviewColumn() {
    return this.page.locator('[data-testid="column-review"], :has-text("Review")')
  }

  get doneColumn() {
    return this.page.locator('[data-testid="column-done"], :has-text("Done")')
  }

  // Task elements
  get taskCards() {
    return this.page.locator('[data-testid="task-card"], .task-card')
  }

  get addTaskButton() {
    return this.page.getByRole("button", { name: /add task|create task|new task/i })
  }

  // Task dialog
  get taskDialog() {
    return this.page.getByRole("dialog")
  }

  get taskTitleInput() {
    return this.page.locator('#title, [name="title"], input[placeholder*="title" i]')
  }

  get taskDescriptionInput() {
    return this.page.locator('#description, [name="description"], textarea')
  }

  get taskPrioritySelect() {
    return this.page.locator('#priority, [name="priority"]')
  }

  get taskAssigneeSelect() {
    return this.page.locator('#assignee, [name="assignedToUserId"]')
  }

  get taskDeadlineInput() {
    return this.page.locator('#deadline, [name="deadline"], input[type="date"]')
  }

  get saveTaskButton() {
    return this.taskDialog.getByRole("button", { name: /save|create|add/i })
  }

  get cancelTaskButton() {
    return this.taskDialog.getByRole("button", { name: /cancel/i })
  }

  /**
   * Navigate to project tasks tab
   */
  async navigateToProjectTasks(projectId: string) {
    this.projectId = projectId
    await this.goto(`/dashboard/projects/${projectId}`)
    await this.page.waitForLoadState("domcontentloaded")

    // Click on Tasks tab
    const tasksTab = this.page.getByRole("tab", { name: /tasks/i })
    await tasksTab.click()
    await this.page.waitForTimeout(1000)
  }

  /**
   * Open create task dialog
   */
  async openCreateTaskDialog() {
    await this.addTaskButton.click()
    await expect(this.taskDialog).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Fill task form
   */
  async fillTaskForm(data: {
    title: string
    description?: string
    priority?: "low" | "medium" | "high" | "urgent"
    deadline?: string
  }) {
    await this.taskTitleInput.fill(data.title)

    if (data.description) {
      await this.taskDescriptionInput.fill(data.description)
    }

    if (data.priority) {
      await this.taskPrioritySelect.click()
      await this.page.getByRole("option", { name: new RegExp(data.priority, "i") }).click()
    }

    if (data.deadline) {
      await this.taskDeadlineInput.fill(data.deadline)
    }
  }

  /**
   * Create a new task
   */
  async createTask(data: {
    title: string
    description?: string
    priority?: "low" | "medium" | "high" | "urgent"
  }) {
    await this.openCreateTaskDialog()
    await this.fillTaskForm(data)

    // Submit and wait for API
    const responsePromise = this.page.waitForResponse(
      (res) =>
        res.request().method() === "POST" &&
        /\/tasks(\?|$)/.test(res.url()),
      { timeout: 60_000 }
    )

    await this.saveTaskButton.click()
    await responsePromise

    await expect(this.taskDialog).toBeHidden({ timeout: 30_000 })
  }

  /**
   * Get task card by title
   */
  getTaskCard(title: string): Locator {
    return this.page.locator(`[data-testid="task-card"]:has-text("${title}"), .task-card:has-text("${title}")`)
  }

  /**
   * Click on a task to open details
   */
  async openTask(title: string) {
    const taskCard = this.getTaskCard(title)
    await taskCard.click()
    await expect(this.taskDialog).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert task is visible on board
   */
  async expectTaskVisible(title: string) {
    await expect(this.page.getByText(title)).toBeVisible({ timeout: 30_000 })
  }

  /**
   * Assert task is in specific column
   */
  async expectTaskInColumn(title: string, column: "todo" | "doing" | "review" | "done") {
    const columnLocator = {
      todo: this.todoColumn,
      doing: this.doingColumn,
      review: this.reviewColumn,
      done: this.doneColumn,
    }[column]

    await expect(columnLocator.getByText(title)).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Change task status via drag or menu
   */
  async changeTaskStatus(
    taskTitle: string,
    newStatus: "todo" | "doing" | "review" | "done"
  ) {
    // Open task dialog
    await this.openTask(taskTitle)

    // Find status select/dropdown
    const statusSelect = this.taskDialog.locator('#status, [name="status"]')
    await statusSelect.click()

    const statusMap = {
      todo: /to\s*do|backlog/i,
      doing: /doing|in\s*progress/i,
      review: /review/i,
      done: /done|complete/i,
    }

    await this.page.getByRole("option", { name: statusMap[newStatus] }).click()

    // Save
    const responsePromise = this.page.waitForResponse(
      (res) =>
        res.request().method() === "PUT" &&
        /\/tasks\//.test(res.url()),
      { timeout: 60_000 }
    )

    await this.saveTaskButton.click()
    await responsePromise
  }

  /**
   * Delete a task
   */
  async deleteTask(title: string) {
    await this.openTask(title)

    const deleteButton = this.taskDialog.getByRole("button", { name: /delete/i })
    await deleteButton.click()

    // Confirm deletion
    const confirmButton = this.page.getByRole("button", { name: /confirm|yes|delete/i })
    if (await confirmButton.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await confirmButton.click()
    }

    await this.page.waitForResponse(
      (res) =>
        res.request().method() === "DELETE" &&
        /\/tasks\//.test(res.url()),
      { timeout: 60_000 }
    )
  }

  /**
   * Get count of tasks in a column
   */
  async getColumnTaskCount(column: "todo" | "doing" | "review" | "done"): Promise<number> {
    const columnLocator = {
      todo: this.todoColumn,
      doing: this.doingColumn,
      review: this.reviewColumn,
      done: this.doneColumn,
    }[column]

    return await columnLocator.locator('[data-testid="task-card"], .task-card').count()
  }
}
