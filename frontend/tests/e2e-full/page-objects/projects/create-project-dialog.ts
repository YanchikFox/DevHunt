import { Page, expect } from "@playwright/test"

/**
 * Create Project Dialog Component
 * Covers the dialog for creating new projects
 */
export class CreateProjectDialog {
  constructor(private readonly page: Page) {}

  get dialog() {
    return this.page.getByRole("dialog", { name: /Create Project/i })
  }

  get titleInput() {
    return this.page.locator("#title")
  }

  get descriptionInput() {
    return this.page.locator("#description")
  }

  get technologiesInput() {
    return this.dialog.getByRole("textbox", { name: /Technologies/i })
  }

  get rolesInput() {
    return this.dialog.getByRole("textbox", { name: /Required Roles/i })
  }

  get visibilitySelect() {
    return this.page.locator("#visibility")
  }

  get createButton() {
    // Match "Create" but not "Create Project" (dialog title)
    return this.dialog.getByRole("button", { name: /^Create$/ })
  }

  get cancelButton() {
    return this.dialog.getByRole("button", { name: /cancel/i })
  }

  // Validation errors
  get titleError() {
    return this.page.getByText(/title.*at least|at least.*3.*characters/i)
  }

  get descriptionError() {
    return this.page.getByText(/description.*at least|at least.*10.*characters/i)
  }

  /**
   * Check if dialog is visible
   */
  async isVisible(): Promise<boolean> {
    try {
      await expect(this.dialog).toBeVisible({ timeout: 5_000 })
      return true
    } catch {
      return false
    }
  }

  /**
   * Wait for dialog to be visible
   */
  async waitForVisible(timeout = 10_000) {
    await expect(this.dialog).toBeVisible({ timeout })
  }

  /**
   * Fill project form
   */
  async fillForm(data: {
    title: string
    description: string
    technologies?: string[]
    roles?: string[]
    visibility?: "public" | "private"
  }) {
    await this.titleInput.fill(data.title)
    await this.descriptionInput.fill(data.description)

    // Add technologies
    if (data.technologies && data.technologies.length > 0) {
      for (const tech of data.technologies) {
        await this.technologiesInput.fill(tech)
        await this.technologiesInput.press("Enter")
        // Wait for chip to appear
        await this.page.waitForTimeout(300)
      }
      // Clear input after adding all
      await this.technologiesInput.fill("")
    }

    // Add roles
    if (data.roles && data.roles.length > 0) {
      for (const role of data.roles) {
        await this.rolesInput.fill(role)
        await this.rolesInput.press("Enter")
        await this.page.waitForTimeout(300)
      }
      await this.rolesInput.fill("")
    }

    // Set visibility
    if (data.visibility) {
      await this.visibilitySelect.click()
      await this.page
        .getByRole("option", { name: new RegExp(data.visibility, "i") })
        .click()
    }
  }

  /**
   * Submit form and wait for API response
   */
  async submit() {
    const responsePromise = this.page.waitForResponse(
      (res) =>
        res.request().method() === "POST" &&
        /\/projects(\?|$)/.test(res.url()) &&
        res.url().includes("/api/proxy-core"),
      { timeout: 60_000 }
    )

    await this.createButton.click()
    const response = await responsePromise

    if (!response.ok()) {
      let body = ""
      try {
        body = await response.text()
      } catch {
        body = "<failed to read>"
      }
      throw new Error(`Create project failed: ${response.status()}\n${body}`)
    }

    // Wait for dialog to close
    await expect(this.dialog).toBeHidden({ timeout: 60_000 })
  }

  /**
   * Click submit without waiting for response (for validation tests)
   */
  async clickCreate() {
    await this.createButton.click()
  }

  /**
   * Cancel and close dialog
   */
  async cancel() {
    await this.cancelButton.click()
    await expect(this.dialog).toBeHidden({ timeout: 10_000 })
  }

  /**
   * Assert title validation error
   */
  async expectTitleError() {
    await expect(this.titleError).toBeVisible({ timeout: 5_000 })
  }

  /**
   * Assert description validation error
   */
  async expectDescriptionError() {
    await expect(this.descriptionError).toBeVisible({ timeout: 5_000 })
  }
}
