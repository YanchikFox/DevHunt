import { expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Profile Page Object
 * Covers: /[locale]/dashboard/profile and /[locale]/dashboard/profile/edit
 */
export class ProfilePage extends BasePage {
  // View profile elements
  get profileName() {
    return this.page.locator('[data-testid="profile-name"], h1, .profile-name')
  }

  get profileBio() {
    return this.page.locator('[data-testid="profile-bio"], .bio')
  }

  get profileAvatar() {
    return this.page.locator('[data-testid="profile-avatar"], .avatar, img[alt*="avatar" i]')
  }

  get profileSkills() {
    return this.page.locator('[data-testid="profile-skills"], .skills')
  }

  get editProfileButton() {
    return this.page.getByRole("button", { name: /edit/i })
  }

  get editProfileLink() {
    return this.page.getByRole("link", { name: /edit/i })
  }

  // Edit profile elements
  get fullNameInput() {
    return this.page.locator('#fullName, [name="fullName"]')
  }

  get bioInput() {
    return this.page.locator('#bio, [name="bio"], textarea')
  }

  get experienceInput() {
    return this.page.locator('#experience, [name="experience"]')
  }

  get githubInput() {
    return this.page.locator('#github, [name="github"]')
  }

  get linkedinInput() {
    return this.page.locator('#linkedin, [name="linkedin"]')
  }

  get websiteInput() {
    return this.page.locator('#website, [name="website"]')
  }

  get saveButton() {
    return this.page.getByRole("button", { name: /save|update/i })
  }

  get cancelButton() {
    return this.page.getByRole("button", { name: /cancel/i })
  }

  // Stats
  get followersCount() {
    return this.page.locator('[data-testid="followers-count"], :has-text("followers")')
  }

  get followingCount() {
    return this.page.locator('[data-testid="following-count"], :has-text("following")')
  }

  get projectsCount() {
    return this.page.locator('[data-testid="projects-count"], :has-text("projects")')
  }

  /**
   * Navigate to own profile view
   */
  async navigateToProfile() {
    await this.goto("/dashboard/profile")
    await this.page.waitForLoadState("domcontentloaded")
  }

  /**
   * Navigate to profile edit page
   */
  async navigateToEditProfile() {
    await this.goto("/dashboard/profile/edit")
    await this.page.waitForLoadState("domcontentloaded")
  }

  /**
   * Navigate to other user's profile
   */
  async navigateToUserProfile(userId: string) {
    await this.goto(`/dashboard/profile/${userId}`)
    await this.page.waitForLoadState("domcontentloaded")
  }

  /**
   * Click edit button to go to edit page
   */
  async clickEditProfile() {
    const btn = this.editProfileButton
    const link = this.editProfileLink

    if ((await btn.count()) > 0) {
      await btn.click()
    } else {
      await link.click()
    }

    await this.page.waitForURL(/\/profile\/edit/, { timeout: 30_000 })
  }

  /**
   * Fill edit profile form
   */
  async fillEditForm(data: {
    fullName?: string
    bio?: string
    experience?: number
    github?: string
    linkedin?: string
    website?: string
  }) {
    if (data.fullName) {
      await this.fullNameInput.fill(data.fullName)
    }
    if (data.bio) {
      await this.bioInput.fill(data.bio)
    }
    if (data.experience !== undefined) {
      await this.experienceInput.fill(data.experience.toString())
    }
    if (data.github) {
      await this.githubInput.fill(data.github)
    }
    if (data.linkedin) {
      await this.linkedinInput.fill(data.linkedin)
    }
    if (data.website) {
      await this.websiteInput.fill(data.website)
    }
  }

  /**
   * Save profile changes
   */
  async saveProfile() {
    const responsePromise = this.page.waitForResponse(
      (res) =>
        (res.request().method() === "PUT" || res.request().method() === "PATCH") &&
        /\/profile|\/users\/me/.test(res.url()),
      { timeout: 60_000 }
    )

    await this.saveButton.click()
    await responsePromise
  }

  /**
   * Assert profile name is displayed
   */
  async expectProfileName(name: string) {
    await expect(this.page.getByText(name)).toBeVisible({ timeout: 10_000 })
  }
}
