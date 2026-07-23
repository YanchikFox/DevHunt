import { test, expect } from "@playwright/test"
import { login, waitForOkResponse, tinyPngFilePayload } from "./helpers"

test.describe("Projects deep flow (full-stack)", () => {
  test.setTimeout(7 * 60_000)

  test("can create project, add news, upload media", async ({ page }) => {
    await login(page)

    await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

    // Open create dialog
    const primaryCreate = page.getByRole("button", { name: "Create Project" })
    if (await primaryCreate.count()) {
      await primaryCreate.click()
    } else {
      await page.getByRole("button", { name: "Create Your First Project" }).click()
    }

    const createDialog = page.getByRole("dialog", { name: "Create Project" })
    await expect(createDialog).toBeVisible()

    const title = `E2E Deep Project ${Date.now()}`

    await page.fill("#title", title)
    await page.fill("#description", "E2E full-stack deep project flow.")

    // Some environments validate these as required.
    const techInput = createDialog.getByRole("textbox", { name: "Technologies" })
    await techInput.fill("React")
    await techInput.press("Enter")
    if (!(await createDialog.getByText(/^React\s*×$/).count())) {
      await createDialog.getByRole("button", { name: "Add" }).first().click()
    }
    await expect(createDialog.getByText(/^React\s*×$/)).toBeVisible({ timeout: 10_000 })

    const roleInput = createDialog.getByRole("textbox", { name: "Required Roles" })
    await roleInput.fill("Backend Developer")
    await roleInput.press("Enter")
    if (!(await createDialog.getByText(/^Backend Developer\s*×$/).count())) {
      await createDialog.getByRole("button", { name: "Add" }).nth(1).click()
    }
    await expect(createDialog.getByText(/^Backend Developer\s*×$/)).toBeVisible({ timeout: 10_000 })

    const createProjectPromise = waitForOkResponse(page, {
      method: "POST",
      urlMatch: /\/projects(\?|$)/,
      label: "Create project",
    })

    await createDialog.getByRole("button", { name: /^Create$/ }).click()
    await createProjectPromise

    await expect(createDialog).toBeHidden({ timeout: 60_000 })

    // Open the newly created project.
    const projectRow = page.getByText(title).first()
    await expect(projectRow).toBeVisible({ timeout: 60_000 })
    await projectRow.click()

    await page.waitForURL(/\/en\/dashboard\/projects\//, { timeout: 60_000 })

    // Go to Activity tab (news + gallery live there).
    const activityTab = page.getByRole("tab", { name: "Activity" })
    if (await activityTab.count()) {
      await activityTab.click()
    } else {
      await page.getByText("Activity").first().click()
    }

    // Add News
    await page.getByRole("button", { name: "Add News" }).click()
    const newsDialog = page.getByRole("dialog", { name: "Publish News" })
    await expect(newsDialog).toBeVisible({ timeout: 30_000 })

    const newsTitle = `E2E News ${Date.now()}`
    const newsContent = "This news item was created by Playwright full-stack E2E." +
      "\n\nIt should be visible on the project Activity tab.";

    await newsDialog.getByPlaceholder("News title").fill(newsTitle)
    await newsDialog.getByPlaceholder("Write your update here...").fill(newsContent)

    const createNewsPromise = waitForOkResponse(page, {
      method: "POST",
      urlMatch: /\/projects\/[^/]+\/news(\?|$)/,
      label: "Create news",
    })

    await newsDialog.getByRole("button", { name: "Publish" }).click()
    await createNewsPromise

    await expect(newsDialog).toBeHidden({ timeout: 60_000 })
    await expect(page.getByText(newsTitle).first()).toBeVisible({ timeout: 60_000 })

    // Upload Media (tiny PNG)
    await page.getByRole("button", { name: "Upload Media" }).click()
    const uploadDialog = page.getByRole("dialog", { name: "Upload Media" })
    await expect(uploadDialog).toBeVisible({ timeout: 30_000 })

    const filePayload = tinyPngFilePayload()
    await uploadDialog.locator('input[type="file"]').setInputFiles(filePayload)

    const uploadPromise = waitForOkResponse(page, {
      method: "POST",
      urlMatch: /\/projects\/[^/]+\/files(\?|$)/,
      label: "Upload file",
    })

    await uploadDialog.getByRole("button", { name: "Upload" }).click()
    await uploadPromise

    await expect(uploadDialog).toBeHidden({ timeout: 60_000 })

    // The gallery should no longer say "No media uploaded yet." (may take a moment to refresh).
    await expect(page.getByText("No media uploaded yet.")).toBeHidden({ timeout: 60_000 })
  })
})
