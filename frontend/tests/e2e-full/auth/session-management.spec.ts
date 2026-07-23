import { test, expect } from "@playwright/test"
import { login } from "../helpers"
import { E2E_USER } from "../fixtures/test-users"

test.describe("Auth - Session Management", () => {
  test.setTimeout(5 * 60_000)

  test("should maintain session after page refresh", async ({ page }) => {
    await login(page)
    await page.reload()
    // Should still be on dashboard, not redirected to login
    await expect(page).toHaveURL(/\/(dashboard|projects|community)/)
  })

  test("should logout and redirect to login", async ({ page }) => {
    await login(page)
    // Navigate to profile where logout is typically available
    await page.goto("/en/dashboard/profile", { waitUntil: "domcontentloaded" })

    // Look for user menu button (avatar/profile dropdown)
    const userMenuTrigger = page.locator(
      '[data-testid="user-menu"], button:has-text("Profile"), [aria-label="User menu"]'
    )

    // If there's a user menu, click it first
    if ((await userMenuTrigger.count()) > 0) {
      await userMenuTrigger.first().click()
    }

    // Click logout button/link
    const logoutButton = page.getByRole("menuitem", { name: /logout|sign out/i })
    const logoutLink = page.getByRole("link", { name: /logout|sign out/i })
    const logoutBtn = page.getByRole("button", { name: /logout|sign out/i })

    if ((await logoutButton.count()) > 0) {
      await logoutButton.click()
    } else if ((await logoutLink.count()) > 0) {
      await logoutLink.click()
    } else if ((await logoutBtn.count()) > 0) {
      await logoutBtn.click()
    }

    // Logout redirects to home page (with or without locale) or login page
    await page.waitForURL(/\/(login|en\/login|en\/?)$/, { timeout: 30_000 })
  })

  test("should redirect to login when accessing protected route without auth", async ({
    page,
    context,
  }) => {
    // Clear any existing auth state
    await context.clearCookies()

    // Try to access protected route
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

    // Should redirect to login
    await page.waitForURL(/\/login/, { timeout: 30_000 })
  })
})
