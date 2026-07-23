import { Page, Locator, expect } from "@playwright/test"

/**
 * Base Page Object with common functionality for all pages
 */
export abstract class BasePage {
  constructor(public readonly page: Page) { }

  /**
   * Default locale for navigation
   */
  protected get locale(): string {
    return "en"
  }

  /**
   * Navigate to a path with locale prefix
   */
  async goto(
    path: string,
    options?: { waitUntil?: "domcontentloaded" | "load" | "networkidle" }
  ) {
    const fullPath = path.startsWith("/") ? `/${this.locale}${path}` : `/${this.locale}/${path}`
    await this.page.goto(fullPath, {
      waitUntil: options?.waitUntil ?? "domcontentloaded",
    })
  }

  /**
   * Wait for API response with proxy-core detection
   */
  async waitForApiResponse(options: {
    method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE"
    urlPattern: RegExp
    timeout?: number
    requireProxyCore?: boolean
    expectOk?: boolean
  }) {
    const {
      method,
      urlPattern,
      timeout = 60_000,
      requireProxyCore = true,
      expectOk = true,
    } = options

    const response = await this.page.waitForResponse(
      (res) => {
        if (res.request().method() !== method) return false
        const url = res.url()
        if (requireProxyCore && !url.includes("/api/proxy-core")) return false
        return urlPattern.test(url)
      },
      { timeout }
    )

    if (expectOk && !response.ok()) {
      let body = ""
      try {
        body = await response.text()
      } catch {
        body = "<failed to read>"
      }
      throw new Error(
        `API call failed: ${method} ${response.url()}\nStatus: ${response.status()}\nBody: ${body}`
      )
    }

    return response
  }

  /**
   * Get element by test ID
   */
  getByTestId(testId: string): Locator {
    return this.page.getByTestId(testId)
  }

  /**
   * Get dialog by accessible name
   */
  getDialog(name: string | RegExp): Locator {
    return this.page.getByRole("dialog", { name })
  }

  /**
   * Assert success toast is visible
   */
  async expectToastSuccess(message?: string | RegExp) {
    const toast = this.page.locator('[data-sonner-toast][data-type="success"]')
    await expect(toast).toBeVisible({ timeout: 10_000 })
    if (message) {
      await expect(toast).toContainText(message)
    }
  }

  /**
   * Assert error toast is visible
   */
  async expectToastError(message?: string | RegExp) {
    const toast = this.page.locator('[data-sonner-toast][data-type="error"]')
    await expect(toast).toBeVisible({ timeout: 10_000 })
    if (message) {
      await expect(toast).toContainText(message)
    }
  }

  /**
   * Wait for page URL to match pattern
   */
  async waitForUrl(pattern: RegExp, timeout = 60_000) {
    await this.page.waitForURL(pattern, { timeout })
  }

  /**
   * Check if element is visible without throwing
   */
  async isVisible(locator: Locator, timeout = 5_000): Promise<boolean> {
    try {
      await expect(locator).toBeVisible({ timeout })
      return true
    } catch {
      return false
    }
  }
}
