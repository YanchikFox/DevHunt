import { Page, Locator, expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * User Search / Teams Page Object
 * Covers: /[locale]/dashboard/teams (user discovery)
 */
export class UserSearchPage extends BasePage {
  // Search elements
  get searchInput() {
    return this.page.locator('#search, [name="search"], input[placeholder*="search" i]')
  }

  get skillsFilter() {
    return this.page.locator('[data-testid="skills-filter"], #skills')
  }

  get experienceFilter() {
    return this.page.locator('[data-testid="experience-filter"], #experience')
  }

  // Results
  get userCards() {
    return this.page.locator('[data-testid="user-card"], .user-card, article')
  }

  get emptyState() {
    return this.page.getByText(/no users|no results/i)
  }

  get loadingState() {
    return this.page.locator('[data-testid="loading"], .loading')
  }

  // User card elements
  get followButtons() {
    return this.page.getByRole("button", { name: /follow/i })
  }

  get unfollowButtons() {
    return this.page.getByRole("button", { name: /unfollow|following/i })
  }

  /**
   * Navigate to teams/user search page
   */
  async navigate() {
    await this.goto("/dashboard/teams")
    await this.page.waitForLoadState("domcontentloaded")
    await this.page.waitForTimeout(1000)
  }

  /**
   * Search for users by query
   */
  async searchUsers(query: string) {
    await this.searchInput.fill(query)
    await this.page.waitForTimeout(500) // Debounce
  }

  /**
   * Clear search
   */
  async clearSearch() {
    await this.searchInput.clear()
    await this.page.waitForTimeout(500)
  }

  /**
   * Get user card by name
   */
  getUserCard(name: string): Locator {
    return this.page.locator(`[data-testid="user-card"]:has-text("${name}"), .user-card:has-text("${name}")`)
  }

  /**
   * Click on user to view profile
   */
  async clickUser(name: string) {
    const userCard = this.getUserCard(name)
    await userCard.click()
    await this.page.waitForURL(/\/profile\//, { timeout: 30_000 })
  }

  /**
   * Follow a user from the list
   */
  async followUser(name: string) {
    const userCard = this.getUserCard(name)
    const followBtn = userCard.getByRole("button", { name: /follow/i })

    const responsePromise = this.page.waitForResponse(
      (res) =>
        res.request().method() === "POST" &&
        /\/follow/.test(res.url()),
      { timeout: 60_000 }
    )

    await followBtn.click()
    await responsePromise
  }

  /**
   * Unfollow a user from the list
   */
  async unfollowUser(name: string) {
    const userCard = this.getUserCard(name)
    const unfollowBtn = userCard.getByRole("button", { name: /unfollow|following/i })

    const responsePromise = this.page.waitForResponse(
      (res) =>
        res.request().method() === "DELETE" &&
        /\/follow/.test(res.url()),
      { timeout: 60_000 }
    )

    await unfollowBtn.click()
    await responsePromise
  }

  /**
   * Get count of visible users
   */
  async getUserCount(): Promise<number> {
    return await this.userCards.count()
  }

  /**
   * Assert user is visible in list
   */
  async expectUserVisible(name: string) {
    await expect(this.page.getByText(name)).toBeVisible({ timeout: 30_000 })
  }

  /**
   * Assert empty state is shown
   */
  async expectEmptyState() {
    await expect(this.emptyState).toBeVisible({ timeout: 10_000 })
  }
}
