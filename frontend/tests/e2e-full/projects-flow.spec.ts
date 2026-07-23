import { test, expect, type Page } from "@playwright/test"

const E2E_EMAIL = process.env.DEVHUNT_E2E_USER_EMAIL ?? "e2e@devhunt.local"
const E2E_PASSWORD = process.env.DEVHUNT_E2E_USER_PASSWORD ?? "TestPassword123!"

async function login(page: Page) {
  await page.goto("/en/login")
  await page.fill("#email", E2E_EMAIL)
  await page.fill("#password", E2E_PASSWORD)
  await page.click('button[type="submit"]')
  await page.waitForURL(/\/en\/(dashboard|projects|community|$)/, { timeout: 60_000 })
}

test.describe("Projects flow (full-stack)", () => {
  test.setTimeout(5 * 60_000)

  test("can create a project via UI", async ({ page }) => {
    await login(page)

    // networkidle can hang on pages with long-lived connections.
    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

    // Open create dialog
    const primaryCreate = page.getByRole("button", { name: "Create Project" })
    if (await primaryCreate.count()) {
      await primaryCreate.click()
    } else {
      await page.getByRole("button", { name: "Create Your First Project" }).click()
    }
    const dialog = page.getByRole("dialog", { name: "Create Project" })
    await expect(dialog).toBeVisible()

    const title = `E2E Project ${Date.now()}`

    await page.fill("#title", title)
    await page.fill("#description", "E2E full-stack project created by Playwright.")

    // Some environments validate these as required.
    const techInput = dialog.getByRole("textbox", { name: "Technologies" })
    await techInput.fill("React")
    await techInput.press("Enter")
    if (!(await dialog.getByText(/^React\s*×$/).count())) {
      await dialog.getByRole("button", { name: "Add" }).first().click()
    }
    await expect(dialog.getByText(/^React\s*×$/)).toBeVisible({ timeout: 10_000 })

    const roleInput = dialog.getByRole("textbox", { name: "Required Roles" })
    await roleInput.fill("Backend Developer")
    await roleInput.press("Enter")
    if (!(await dialog.getByText(/^Backend Developer\s*×$/).count())) {
      await dialog.getByRole("button", { name: "Add" }).nth(1).click()
    }
    await expect(dialog.getByText(/^Backend Developer\s*×$/)).toBeVisible({ timeout: 10_000 })

    // Submit inside dialog
    const createResponsePromise = page.waitForResponse(
      (res) =>
        res.request().method() === "POST" &&
        /\/projects(\?|$)/.test(res.url()) &&
        // In-browser requests should go through the Next.js proxy route.
        res.url().includes("/api/proxy-core"),
      { timeout: 60_000 }
    )

    await dialog.getByRole("button", { name: /^Create$/ }).click()
    const createResponse = await createResponsePromise

    if (!createResponse.ok()) {
      let body = ""
      try {
        body = await createResponse.text()
      } catch {
        body = "<failed to read response body>"
      }

      throw new Error(
        `Create project failed: ${createResponse.status()} ${createResponse.statusText()}\n${body}`
      )
    }

    // Prefer stable success signals over brittle toast text.
    await expect(dialog).toBeHidden({ timeout: 60_000 })

    // Project should appear in the list.
    await expect(page.getByText(title).first()).toBeVisible({ timeout: 60_000 })
  })
})
