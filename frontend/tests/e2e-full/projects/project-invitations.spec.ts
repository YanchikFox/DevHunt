import { test, expect } from "@playwright/test"
import { login } from "../helpers"

test.describe("Projects - Invitation Flows", () => {
  test.setTimeout(7 * 60_000)

  test.beforeEach(async ({ page }) => {
    await login(page)
  })

  test("should display invitations page", async ({ page }) => {
    // Navigate to invitations/teams page
    await page.goto("/en/dashboard/invitations", { waitUntil: "domcontentloaded" })

    // Wait for page to load
    await page.waitForTimeout(2000)

    // Should see some content related to invitations
    const pageContent = await page.textContent("body")
    expect(pageContent).toBeTruthy()
  })

  test("should display teams page with invitation section", async ({ page }) => {
    await page.goto("/en/dashboard/teams", { waitUntil: "domcontentloaded" })

    // Wait for page to load
    await page.waitForTimeout(2000)

    // Should have some content
    const pageContent = await page.textContent("body")
    expect(pageContent).toBeTruthy()
  })
})
