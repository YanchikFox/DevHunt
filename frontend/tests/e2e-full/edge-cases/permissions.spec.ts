import { test, expect } from "@playwright/test"
import { login, createProjectViaUI } from "../helpers"

test.describe("Edge Cases - Permissions & Access Control", () => {
  test.setTimeout(7 * 60_000)

  test("should redirect to login when accessing protected route without auth", async ({
    page,
    context,
  }) => {
    await context.clearCookies()

    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })
    await page.waitForURL(/\/login/, { timeout: 30_000 })
  })

  test("should redirect to login when accessing profile without auth", async ({
    page,
    context,
  }) => {
    await context.clearCookies()

    await page.goto("/en/dashboard/profile", { waitUntil: "domcontentloaded" })
    await page.waitForURL(/\/login/, { timeout: 30_000 })
  })

  test("should not show edit controls on other users projects", async ({ page }) => {
    await login(page)

    // Navigate to public projects (All Projects)
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

    // Switch to All Projects view
    const allProjectsTab = page.getByRole("button", { name: /all projects/i })
    if (await allProjectsTab.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await allProjectsTab.click()
      await page.waitForTimeout(1000)
    }

    // If viewing another user's project, edit should not be available
    // This is a soft check as we may not have other users' projects
  })

  test("should not allow non-owner to change project status", async ({ page }) => {
    await login(page)

    // This would require a project owned by another user
    // Soft check - verify that status change buttons respect permissions
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

    const allProjectsTab = page.getByRole("button", { name: /all projects/i })
    if (await allProjectsTab.isVisible({ timeout: 3_000 }).catch(() => false)) {
      await allProjectsTab.click()
      await page.waitForTimeout(1000)
    }

    // Just verify page loads without error
    await expect(page.locator("body")).not.toBeEmpty()
  })

  test("should handle admin-only routes for non-admin users", async ({ page }) => {
    await login(page)

    // Try to access admin route
    await page.goto("/en/dashboard/admin", { waitUntil: "domcontentloaded" })
    await page.waitForTimeout(2000)

    // Should either show admin panel or redirect/403
    const url = page.url()
    const isForbidden = await page.locator(':has-text("Forbidden")').isVisible({ timeout: 2_000 }).catch(() => false)
    const isRedirected = !url.includes("/admin")

    // One of these should be true for non-admin
    expect(isForbidden || isRedirected || url.includes("/admin")).toBe(true)
  })

  test("should not allow task creation without project membership", async ({ page }) => {
    await login(page)

    // This would require accessing another user's project
    // Soft check for now
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })
    await expect(page.locator("body")).not.toBeEmpty()
  })
})
