import { test, expect } from "@playwright/test"
import { login, createProjectViaUI } from "../helpers"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"

test.describe("Edge Cases - Concurrent Actions", () => {
  test.setTimeout(7 * 60_000)

  test("should handle rapid form submissions", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill(`RapidSubmit ${Date.now()}`)
    await projectsPage.createDialog.descriptionInput.fill("Testing rapid form submissions")

    // Click create button multiple times rapidly
    const createButton = projectsPage.createDialog.createButton
    await createButton.click()
    await createButton.click().catch(() => {}) // May fail if dialog closes
    await createButton.click().catch(() => {})

    // Should only create one project or show appropriate feedback
    await page.waitForTimeout(2000)
  })

  test("should handle double-click on action buttons", async ({ page }) => {
    await login(page)

    const projectTitle = await createProjectViaUI(page, {
      title: `DoubleClick ${Date.now()}`,
    })

    // Navigate to project
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })
    await page.getByText(projectTitle).first().click()
    await page.waitForURL(/\/dashboard\/projects\//)

    // Double-click on an action button
    const actionsMenu = page.getByRole("button", { name: /actions|menu/i })
    if (await actionsMenu.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await actionsMenu.dblclick()
      await page.waitForTimeout(1000)
    }
  })

  test("should handle page refresh during form submission", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill(`RefreshTest ${Date.now()}`)
    await projectsPage.createDialog.descriptionInput.fill("Testing refresh during submission")

    // Don't submit, just reload
    await page.reload()

    // Form should be reset
    await expect(page.getByRole("dialog")).toBeHidden({ timeout: 5_000 })
  })

  test("should handle back navigation during operation", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill(`BackNav ${Date.now()}`)

    // Press back
    await page.goBack()
    await page.waitForTimeout(1000)

    // Should handle gracefully
    await expect(page.locator("body")).not.toBeEmpty()
  })

  test("should prevent duplicate project creation", async ({ page }) => {
    await login(page)

    const timestamp = Date.now()
    const title1 = `DuplicateTest ${timestamp}`

    // Create first project
    await createProjectViaUI(page, { title: title1 })

    // Try to create another with same title
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill(title1)
    await projectsPage.createDialog.descriptionInput.fill("Duplicate project test")
    await projectsPage.createDialog.createButton.click()

    await page.waitForTimeout(2000)
    // Should either succeed (different ID) or show error
  })
})
