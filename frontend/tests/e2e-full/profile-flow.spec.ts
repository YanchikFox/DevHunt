import { test, expect } from "@playwright/test"
import { login, waitForOkResponse } from "./helpers"

test.describe("Profile flow (full-stack)", () => {
  test.setTimeout(5 * 60_000)

  test("can edit own profile", async ({ page }) => {
    await login(page)

    await page.goto("/en/dashboard/profile/edit", { waitUntil: "domcontentloaded" })
    await expect(page.getByRole("heading", { name: "Edit Profile" })).toBeVisible()

    const updatedName = `E2E User ${Date.now()}`

    await page.fill("#fullName", updatedName)
    await page.fill("#bio", "Updated by Playwright full-stack E2E.")
    await page.fill("#github", "https://github.com/e2e-devhunt")

    const saveResponsePromise = waitForOkResponse(page, {
      method: "PUT",
      urlMatch: /\/Profile\/me(\?|$)/,
      label: "Update profile",
    })

    await page.getByRole("button", { name: /^Save|Saving…$/ }).click()
    await saveResponsePromise

    await page.waitForURL(/\/en\/dashboard\/profile(\?|$)/, { timeout: 60_000 })
    await expect(page.getByRole("heading", { level: 1, name: updatedName })).toBeVisible({
      timeout: 60_000,
    })
  })
})
