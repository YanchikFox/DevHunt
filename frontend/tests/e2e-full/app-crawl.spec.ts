import { test, expect, type Page } from "@playwright/test"

// Node.js process global is available in Playwright tests
const E2E_EMAIL = process.env.DEVHUNT_E2E_USER_EMAIL ?? "e2e@devhunt.local"
const E2E_PASSWORD = process.env.DEVHUNT_E2E_USER_PASSWORD ?? "TestPassword123!"

async function login(page: Page) {
  await page.goto("/en/login")
  await page.fill("#email", E2E_EMAIL)
  await page.fill("#password", E2E_PASSWORD)
  await page.click('button[type="submit"]')

  // Most apps redirect to dashboard; keep it flexible.
  await page.waitForURL(/\/en\/(dashboard|projects|community|$)/, { timeout: 60_000 })
}

test.describe("Full-stack E2E (docker-compose + seeding)", () => {
  test.setTimeout(5 * 60_000)

  test("login + crawl key routes without 500/console errors", async ({ page }) => {
    const consoleErrors: string[] = []
    page.on("console", (msg) => {
      if (msg.type() === "error") {
        consoleErrors.push(msg.text())
      }
    })

    await login(page)

    const routes = [
      "/en/dashboard",
      "/en/dashboard/projects",
      "/en/dashboard/teams",
      "/en/dashboard/notifications",
      "/en/dashboard/profile",
      "/en/projects",
      "/en/community",
    ]

    for (const route of routes) {
      // networkidle can hang on pages that keep long-lived connections (SSE/ws/polling).
      const response = await page.goto(route, { waitUntil: "domcontentloaded" })
      if (response) {
        expect(response.status(), `HTTP status for ${route}`).toBeLessThan(500)
      }
      await expect(page.locator("body")).toBeVisible()
    }

    // Fail on obvious JS/runtime errors.
    const filtered = consoleErrors.filter(
      (e) =>
        !/Failed to load resource/i.test(e) &&
        !/ResizeObserver loop limit exceeded/i.test(e)
    )
    expect(filtered, "Unexpected console errors").toEqual([])
  })
})
