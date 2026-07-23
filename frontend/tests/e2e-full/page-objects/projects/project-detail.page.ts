import { expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Project Detail Page Object
 * Covers: /[locale]/dashboard/projects/[id]
 */
export class ProjectDetailPage extends BasePage {
  // Navigation
  get backButton() {
    return this.page.getByRole("button", { name: /back/i })
  }

  get backLink() {
    return this.page.getByRole("link", { name: /back|projects/i })
  }

  // Header
  get projectTitle() {
    return this.page.getByRole("heading", { level: 1 })
  }

  get statusBadge() {
    return this.page.locator('[data-testid="project-status"], .status-badge')
  }

  // Tabs
  get overviewTab() {
    return this.page.getByRole("tab", { name: /overview|activity/i })
  }

  get tasksTab() {
    return this.page.getByRole("tab", { name: /tasks/i })
  }

  get teamTab() {
    return this.page.getByRole("tab", { name: /team/i })
  }

  get filesTab() {
    return this.page.getByRole("tab", { name: /files/i })
  }

  get newsTab() {
    return this.page.getByRole("tab", { name: /news/i })
  }

  get showcaseTab() {
    return this.page.getByRole("tab", { name: /showcase/i })
  }

  get settingsTab() {
    return this.page.getByRole("tab", { name: /settings/i })
  }

  // Actions
  get actionsMenu() {
    return this.page.getByRole("button", { name: /actions|menu|more/i })
  }

  get actionsDropdown() {
    return this.page.locator('[data-testid="actions-dropdown"], [role="menu"]')
  }

  // Team tab elements
  get inviteButton() {
    return this.page.getByRole("button", { name: /invite/i })
  }

  get teamMembersList() {
    return this.page.locator('[data-testid="team-members"], .team-members')
  }

  // News tab elements
  get addNewsButton() {
    return this.page.getByRole("button", { name: /add news|new post/i })
  }

  /**
   * Navigate to project detail page by ID
   */
  async navigate(projectId: string) {
    await this.goto(`/dashboard/projects/${projectId}`)
    await this.page.waitForLoadState("domcontentloaded")
  }

  /**
   * Switch to a specific tab
   */
  async switchToTab(tab: "overview" | "tasks" | "team" | "files" | "news" | "showcase" | "settings") {
    const tabMap = {
      overview: this.overviewTab,
      tasks: this.tasksTab,
      team: this.teamTab,
      files: this.filesTab,
      news: this.newsTab,
      showcase: this.showcaseTab,
      settings: this.settingsTab,
    }
    const tabElement = tabMap[tab]
    await tabElement.click()
    await this.page.waitForTimeout(500)
  }

  /**
   * Open actions dropdown menu
   */
  async openActionsMenu() {
    await this.actionsMenu.click()
    await expect(this.actionsDropdown).toBeVisible({ timeout: 5_000 })
  }

  /**
   * Change project status via actions menu
   */
  async changeStatus(action: "publish" | "activate" | "complete" | "archive" | "reactivate") {
    // Open actions menu
    await this.openActionsMenu()

    // Click the action
    const actionItem = this.page.getByRole("menuitem", { name: new RegExp(action, "i") })
    await actionItem.click()

    // Handle confirmation dialog if present
    const confirmDialog = this.page.getByRole("dialog")
    if (await confirmDialog.isVisible({ timeout: 2_000 }).catch(() => false)) {
      const confirmButton = confirmDialog.getByRole("button", {
        name: new RegExp(`(${action}|confirm|yes)`, "i"),
      })
      await confirmButton.click()
    }

    // Wait for API response
    await this.page.waitForResponse(
      (res) =>
        res.request().method() === "PATCH" &&
        /\/projects\/[^/]+\/status/.test(res.url()),
      { timeout: 60_000 }
    )
  }

  /**
   * Assert project status matches expected
   */
  async expectStatus(status: string) {
    await expect(
      this.page.getByText(new RegExp(status, "i"))
    ).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Go back to projects list
   */
  async goBack() {
    const backBtn = this.backButton
    const backLnk = this.backLink

    if ((await backBtn.count()) > 0) {
      await backBtn.click()
    } else {
      await backLnk.click()
    }

    await this.page.waitForURL(/\/dashboard\/projects\/?$/, { timeout: 30_000 })
  }

  /**
   * Open invite team member dialog
   */
  async openInviteDialog() {
    await this.switchToTab("team")
    await this.inviteButton.click()
    await expect(
      this.page.getByRole("dialog", { name: /invite/i })
    ).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert owner/leader is visible in team tab
   */
  async expectOwnerVisible() {
    await this.switchToTab("team")
    // Use .first() to avoid strict mode when multiple elements match
    await expect(
      this.page.getByText(/^owner$|^leader$/i).first()
    ).toBeVisible({ timeout: 10_000 })
  }
}
