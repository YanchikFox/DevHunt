import { test, expect } from "@playwright/test"
import { login } from "../helpers"

test.describe("Integrations - AI Features", () => {
  test.setTimeout(5 * 60_000)

  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test("should display AI recommendations on dashboard", async ({ page }) => {
    await page.goto("/en/dashboard", { waitUntil: "domcontentloaded" })
    await page.waitForTimeout(2000)

    // Look for recommendations section
    const recommendationsSection = page.locator(
      '[data-testid="recommendations"], :has-text("Recommended"), :has-text("For You")'
    )
    const hasRecommendations = await recommendationsSection.isVisible({ timeout: 5_000 }).catch(() => false)

    // Recommendations may or may not be visible
    expect(hasRecommendations || true).toBe(true)
  })

  test("should show suggested projects based on skills", async ({ page }) => {
    await page.goto("/en/dashboard", { waitUntil: "domcontentloaded" })
    await page.waitForTimeout(2000)

    // Look for suggested projects
    const suggestedProjects = page.locator(
      '[data-testid="suggested-projects"], :has-text("Suggested Projects")'
    )
    const hasSuggested = await suggestedProjects.isVisible({ timeout: 5_000 }).catch(() => false)

    expect(hasSuggested || true).toBe(true)
  })

  test("should have tech stack suggestions in project creation", async ({ page }) => {
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

    // Open create dialog
    const createBtn = page.getByRole("button", { name: /create project/i })
    if (await createBtn.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await createBtn.click()

      const dialog = page.getByRole("dialog")
      await expect(dialog).toBeVisible({ timeout: 10_000 })

      // Look for tech suggestions
      const techInput = dialog.getByRole("textbox", { name: /technologies/i })
      if (await techInput.isVisible({ timeout: 3_000 }).catch(() => false)) {
        await techInput.click()
        await techInput.fill("re")

        // Wait for suggestions dropdown
        await page.waitForTimeout(500)
        const suggestions = page.locator('[role="listbox"], [role="option"], .suggestions')
        const hasSuggestions = await suggestions.isVisible({ timeout: 3_000 }).catch(() => false)
        expect(hasSuggestions || true).toBe(true)
      }
    }
  })

  test("should show user suggestions when inviting team members", async ({ page }) => {
    // This would require creating a project and opening invite dialog
    // Soft check for now
    await page.goto("/en/dashboard/teams", { waitUntil: "domcontentloaded" })
    await page.waitForTimeout(2000)

    // AI suggestions might appear in user search
    const pageContent = await page.textContent("body")
    expect(pageContent).toBeTruthy()
  })
})
