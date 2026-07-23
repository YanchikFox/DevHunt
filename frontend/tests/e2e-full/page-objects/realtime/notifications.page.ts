import { expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Notifications Page Object
 * Covers: /[locale]/dashboard/notifications
 */
export class NotificationsPage extends BasePage {
  // Notification list
  get notificationList() {
    return this.page.locator('[data-testid="notification-list"], .notifications')
  }

  get notificationItems() {
    return this.page.locator('[data-testid="notification-item"], .notification-item, article')
  }

  get unreadNotifications() {
    return this.page.locator('[data-testid="unread"], .unread')
  }

  // Actions
  get markAllReadButton() {
    return this.page.getByRole("button", { name: /mark all|read all/i })
  }

  get clearAllButton() {
    return this.page.getByRole("button", { name: /clear|delete all/i })
  }

  // Filters
  get filterAll() {
    return this.page.getByRole("button", { name: /^all$/i })
  }

  get filterUnread() {
    return this.page.getByRole("button", { name: /unread/i })
  }

  // Empty state
  get emptyState() {
    return this.page.getByText(/no notifications|all caught up|nothing here/i)
  }

  // Notification bell (header)
  get notificationBell() {
    return this.page.locator('[data-testid="notification-bell"], [aria-label*="notification" i]')
  }

  get notificationBadge() {
    return this.page.locator('[data-testid="notification-badge"], .badge')
  }

  /**
   * Navigate to notifications page
   */
  async navigate() {
    await this.goto("/dashboard/notifications")
    await this.page.waitForLoadState("domcontentloaded")
    await this.page.waitForTimeout(1000)
  }

  /**
   * Click notification bell in header (if exists)
   */
  async clickNotificationBell() {
    await this.notificationBell.click()
  }

  /**
   * Get count of notifications
   */
  async getNotificationCount(): Promise<number> {
    return await this.notificationItems.count()
  }

  /**
   * Get count of unread notifications
   */
  async getUnreadCount(): Promise<number> {
    return await this.unreadNotifications.count()
  }

  /**
   * Mark all notifications as read
   */
  async markAllAsRead() {
    const btn = this.markAllReadButton
    if (await btn.isVisible({ timeout: 2_000 }).catch(() => false)) {
      await btn.click()
      await this.page.waitForResponse(
        (res) =>
          (res.request().method() === "PUT" || res.request().method() === "PATCH") &&
          /\/notifications/.test(res.url()),
        { timeout: 30_000 }
      ).catch(() => {})
    }
  }

  /**
   * Click on a notification
   */
  async clickNotification(index: number = 0) {
    const notifications = this.notificationItems
    const count = await notifications.count()

    if (count > index) {
      await notifications.nth(index).click()
    }
  }

  /**
   * Filter by unread only
   */
  async showUnreadOnly() {
    await this.filterUnread.click()
    await this.page.waitForTimeout(500)
  }

  /**
   * Show all notifications
   */
  async showAll() {
    await this.filterAll.click()
    await this.page.waitForTimeout(500)
  }

  /**
   * Assert empty state is shown
   */
  async expectEmptyState() {
    await expect(this.emptyState).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert notifications are present
   */
  async expectNotificationsPresent() {
    const count = await this.getNotificationCount()
    expect(count).toBeGreaterThan(0)
  }
}
