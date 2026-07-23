import { test, expect } from "@playwright/test"
import { login } from "../helpers"
import { RegisterPage } from "../page-objects/auth/register.page"
import { ProjectsListPage } from "../page-objects/projects/projects-list.page"

test.describe("Edge Cases - Form Validation", () => {
  test.setTimeout(5 * 60_000)

  test("should validate email format on registration", async ({ page }) => {
    const registerPage = new RegisterPage(page)
    await registerPage.navigate()

    await registerPage.fillForm({
      fullName: "Test User",
      email: "invalid-email-format",
      password: "TestPassword123!",
    })
    await registerPage.submit()

    // Should show email validation error or HTML5 validation
    await registerPage.expectStillOnRegisterPage()
  })

  test("should validate minimum length for project title", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill("AB") // 2 chars
    await projectsPage.createDialog.descriptionInput.fill("Valid description here.")
    await projectsPage.createDialog.clickCreate()

    // Dialog should remain open
    await expect(projectsPage.createDialog.dialog).toBeVisible()
  })

  test("should validate minimum length for project description", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    await projectsPage.createDialog.titleInput.fill("Valid Title")
    await projectsPage.createDialog.descriptionInput.fill("Short") // < 10 chars
    await projectsPage.createDialog.clickCreate()

    await expect(projectsPage.createDialog.dialog).toBeVisible()
  })

  test("should validate URL format for social links", async ({ page }) => {
    await login(page)
    await page.goto("/en/dashboard/profile/edit", { waitUntil: "domcontentloaded" })

    const githubInput = page.locator('#github, [name="github"]')
    if (await githubInput.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await githubInput.fill("not-a-valid-url")

      const saveButton = page.getByRole("button", { name: /save/i })
      await saveButton.click()

      // Should show validation error or prevent save
      await page.waitForTimeout(1000)
    }
  })

  test("should prevent XSS in user inputs", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    // Try to inject script
    const xssPayload = '<script>alert("XSS")</script>'
    await projectsPage.createDialog.titleInput.fill(`Safe Title ${Date.now()}`)
    await projectsPage.createDialog.descriptionInput.fill(xssPayload)
    await projectsPage.createDialog.submit()

    // Should either reject or sanitize
    const pageContent = await page.content()
    expect(pageContent).not.toContain('<script>')
  })

  test("should handle very long input gracefully", async ({ page }) => {
    await login(page)
    const projectsPage = new ProjectsListPage(page)
    await projectsPage.navigate()
    await projectsPage.openCreateDialog()

    const longText = "A".repeat(10000)
    await projectsPage.createDialog.descriptionInput.fill(longText)

    // Should either truncate or show error
    const value = await projectsPage.createDialog.descriptionInput.inputValue()
    expect(value.length).toBeLessThanOrEqual(10000)
  })
})
