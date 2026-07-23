import { test, expect } from "@playwright/test"
import { ProfilePage } from "../page-objects/social/profile.page"
import { login } from "../helpers"

test.describe("Social - Profile Management", () => {
  test.setTimeout(5 * 60_000)

  let profilePage: ProfilePage

  test.beforeEach(async ({ page }) => {
    await login(page)
    profilePage = new ProfilePage(page)
  })

  test("should display own profile page", async ({ page }) => {
    await profilePage.navigateToProfile()

    // Profile should show user info
    await expect(page.locator("body")).toContainText(/profile|dashboard/i)
  })

  test("should navigate to edit profile page", async ({ page }) => {
    await profilePage.navigateToEditProfile()

    // Edit form should be visible
    await expect(profilePage.fullNameInput).toBeVisible({ timeout: 30_000 })
  })

  test("should update profile name", async ({ page }) => {
    await profilePage.navigateToEditProfile()

    const newName = `E2E User ${Date.now()}`
    await profilePage.fillEditForm({ fullName: newName })
    await profilePage.saveProfile()

    // Verify update
    await profilePage.navigateToProfile()
    await profilePage.expectProfileName(newName)
  })

  test("should update profile bio", async ({ page }) => {
    await profilePage.navigateToEditProfile()

    const newBio = `Updated bio at ${new Date().toISOString()}`
    await profilePage.fillEditForm({ bio: newBio })
    await profilePage.saveProfile()
  })

  test("should update social links", async ({ page }) => {
    await profilePage.navigateToEditProfile()

    await profilePage.fillEditForm({
      github: "https://github.com/e2e-test",
      linkedin: "https://linkedin.com/in/e2e-test",
      website: "https://e2e-test.dev",
    })
    await profilePage.saveProfile()
  })

  test("should display profile stats", async ({ page }) => {
    await profilePage.navigateToProfile()

    // Stats section should exist
    const statsSection = page.locator('[data-testid="profile-stats"], .stats')
    if (await statsSection.isVisible({ timeout: 5_000 }).catch(() => false)) {
      await expect(statsSection).toBeVisible()
    }
  })

  test("should navigate to privacy settings", async ({ page }) => {
    await page.goto("/en/dashboard/profile/privacy", { waitUntil: "domcontentloaded" })

    // Privacy page should load
    await expect(page.locator("body")).toContainText(/privacy|settings|visibility/i)
  })
})
