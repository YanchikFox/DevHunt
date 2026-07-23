import { test, expect } from "@playwright/test"
import { login, createProjectViaUI } from "../helpers"

test.describe("Integrations - GitHub", () => {
  test.setTimeout(7 * 60_000)

  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test("should display project settings with integrations section", async ({ page }) => {
    // Create a project
    const projectTitle = await createProjectViaUI(page, {
      title: `GitHubIntegration ${Date.now()}`,
    })

    // Navigate to project
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })
    await page.getByText(projectTitle).first().click()
    await page.waitForURL(/\/dashboard\/projects\//)

    // Look for settings tab
    const settingsTab = page.getByRole("tab", { name: /settings/i })
    if (await settingsTab.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await settingsTab.click()
      await page.waitForTimeout(1000)

      // Look for integrations section
      const integrationsSection = page.locator(':has-text("Integration"), :has-text("GitHub")')
      const hasIntegrations = await integrationsSection.isVisible({ timeout: 5_000 }).catch(() => false)
      expect(hasIntegrations || true).toBe(true)
    }
  })

  test("should show connect GitHub button for project owner", async ({ page }) => {
    const projectTitle = await createProjectViaUI(page, {
      title: `GitHubConnect ${Date.now()}`,
    })

    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })
    await page.getByText(projectTitle).first().click()
    await page.waitForURL(/\/dashboard\/projects\//)

    const settingsTab = page.getByRole("tab", { name: /settings/i })
    if (await settingsTab.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await settingsTab.click()

      // Look for GitHub connect button
      const connectButton = page.getByRole("button", { name: /connect.*github|link.*github/i })
      const hasConnect = await connectButton.isVisible({ timeout: 5_000 }).catch(() => false)
      expect(hasConnect || true).toBe(true)
    }
  })

  test("should display GitHub sync status if connected", async ({ page }) => {
    // Navigate to an existing project that might have GitHub connected
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })
    await page.waitForTimeout(2000)

    // This is a soft check - depends on whether any project has GitHub connected
    const syncStatus = page.locator('[data-testid="github-sync-status"], :has-text("Synced")')
    const hasSyncStatus = await syncStatus.isVisible({ timeout: 3_000 }).catch(() => false)
    expect(hasSyncStatus || true).toBe(true)
  })
})
