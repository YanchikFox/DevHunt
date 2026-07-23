import { test, expect } from "@playwright/test"
import { login, waitForOkResponse } from "./helpers"

test.describe("Notifications flow (full-stack)", () => {
  test.setTimeout(5 * 60_000)

  test("notifications page loads settings + notifications", async ({ page }) => {
    await login(page)

    const settingsPromise = waitForOkResponse(page, {
      method: "GET",
      urlMatch: /\/users\/me\/settings(\?|$)/,
      label: "Load notification settings",
    })

    const notificationsPromise = waitForOkResponse(page, {
      method: "GET",
      urlMatch: /\/notifications(\?|$)/,
      label: "Load notifications",
    })

    await page.goto("/en/dashboard/notifications", { waitUntil: "domcontentloaded" })

    await Promise.all([settingsPromise, notificationsPromise])

    await expect(page.getByRole("heading", { name: "Notifications" })).toBeVisible({
      timeout: 60_000,
    })

    await expect(page.locator("#messages")).toBeVisible({ timeout: 60_000 })
    await expect(page.locator("#invitations")).toBeVisible({ timeout: 60_000 })
  })
})
