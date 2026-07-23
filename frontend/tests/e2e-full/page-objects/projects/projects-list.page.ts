import { Page, Locator, expect } from "@playwright/test"
import { BasePage } from "../base.page"
import { CreateProjectDialog } from "./create-project-dialog"

/**
 * Projects List Page Object
 * Covers: /[locale]/dashboard/projects
 */
export class ProjectsListPage extends BasePage {
  readonly createDialog: CreateProjectDialog

  constructor(page: Page) {
    super(page)
    this.createDialog = new CreateProjectDialog(page)
  }

  // Buttons - flexible matching for create project buttons
  get createProjectButton() {
    return this.page.getByRole("button", { name: /create.*project/i })
  }

  get createFirstProjectButton() {
    return this.page.getByRole("button", { name: /create your first project/i })
  }

  // Fallback: any button containing "create" in the page
  get anyCreateButton() {
    return this.page.locator('button:has-text("Create")')
  }

  // Filters
  get searchInput() {
    return this.page.locator("#project-search")
  }

  get statusFilter() {
    return this.page.getByRole("combobox").first()
  }

  // View toggles
  get myProjectsTab() {
    return this.page.getByRole("button", { name: /my projects/i })
  }

  get allProjectsTab() {
    return this.page.getByRole("button", { name: /all projects/i })
  }

  // Project list
  get projectCards() {
    return this.page.locator('[data-testid="project-card"], .project-card, article')
  }

  get emptyState() {
    return this.page.getByText(/no projects|create your first/i)
  }

  /**
   * Navigate to projects list page
   */
  async navigate() {
    await this.goto("/dashboard/projects")
    await this.page.waitForLoadState("domcontentloaded")
    // Wait for page to finish loading - either projects list or empty state with create button
    await this.page
      .locator('button:has-text("Create"), [data-testid="project-card"], .project-card')
      .first()
      .waitFor({ state: "visible", timeout: 30_000 })
  }

  /**
   * Open the create project dialog
   */
  async openCreateDialog() {
    const primaryBtn = this.createProjectButton
    const anyCreate = this.anyCreateButton

    // Try primary button first (regex match for any "create project" button)
    if ((await primaryBtn.count()) > 0) {
      await primaryBtn.first().click()
    } else if ((await anyCreate.count()) > 0) {
      // Fallback to any button with "Create" text
      await anyCreate.first().click()
    } else {
      throw new Error("No create project button found")
    }

    await this.createDialog.waitForVisible()
  }

  /**
   * Search for projects by query
   */
  async searchProjects(query: string) {
    await this.searchInput.fill(query)
    // Wait for debounced search
    await this.page.waitForTimeout(500)
  }

  /**
   * Clear search input
   */
  async clearSearch() {
    await this.searchInput.clear()
    await this.page.waitForTimeout(500)
  }

  /**
   * Filter by project status
   */
  async filterByStatus(
    status: "all" | "draft" | "active" | "recruiting" | "in_progress" | "completed"
  ) {
    await this.statusFilter.click()
    await this.page.getByRole("option", { name: new RegExp(status, "i") }).click()
    await this.page.waitForTimeout(500)
  }

  /**
   * Switch to My Projects view
   */
  async showMyProjects() {
    await this.myProjectsTab.click()
    await this.page.waitForTimeout(500)
  }

  /**
   * Switch to All Projects view
   */
  async showAllProjects() {
    await this.allProjectsTab.click()
    await this.page.waitForTimeout(500)
  }

  /**
   * Get a project card by title
   */
  getProjectCard(title: string): Locator {
    return this.page.locator(`text="${title}"`).first()
  }

  /**
   * Click on a project to view details
   */
  async clickProject(title: string) {
    const projectElement = this.page.getByText(title).first()
    await projectElement.click()
    await this.page.waitForURL(/\/dashboard\/projects\//, { timeout: 60_000 })
  }

  /**
   * Assert project is visible in list
   */
  async expectProjectVisible(title: string) {
    await expect(this.page.getByText(title).first()).toBeVisible({ timeout: 60_000 })
  }

  /**
   * Assert project is not visible in list
   */
  async expectProjectNotVisible(title: string) {
    await expect(this.page.getByText(title)).toBeHidden({ timeout: 10_000 })
  }

  /**
   * Get count of visible projects
   */
  async getProjectCount(): Promise<number> {
    return await this.projectCards.count()
  }
}
