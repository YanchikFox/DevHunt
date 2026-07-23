import { expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Login Page Object
 * Covers: /[locale]/login
 */
export class LoginPage extends BasePage {
  // Form elements
  get emailInput() {
    return this.page.locator("#email")
  }

  get passwordInput() {
    return this.page.locator("#password")
  }

  get submitButton() {
    return this.page.locator('button[type="submit"]')
  }

  // OAuth buttons
  get githubButton() {
    return this.page.getByRole("button", { name: /GitHub/i })
  }

  get googleButton() {
    return this.page.getByRole("button", { name: /Google/i })
  }

  // Navigation links
  get forgotPasswordLink() {
    return this.page.getByRole("link", { name: /forgot/i })
  }

  get registerLink() {
    return this.page.getByRole("link", { name: /register|sign up|create account/i })
  }

  // Error elements
  get formError() {
    return this.page.locator('[role="alert"]')
  }

  /**
   * Navigate to login page
   */
  async navigate() {
    await this.goto("/login")
    await expect(this.emailInput).toBeVisible({ timeout: 30_000 })
  }

  /**
   * Fill email and password fields
   */
  async fillCredentials(email: string, password: string) {
    await this.emailInput.fill(email)
    await this.passwordInput.fill(password)
  }

  /**
   * Click submit button
   */
  async submit() {
    await this.submitButton.click()
  }

  /**
   * Fill credentials and submit
   */
  async login(email: string, password: string) {
    await this.fillCredentials(email, password)
    await this.submit()
  }

  /**
   * Login and wait for redirect to dashboard
   */
  async loginAndWaitForDashboard(email: string, password: string) {
    await this.login(email, password)
    await this.page.waitForURL(/\/en\/(dashboard|projects|community|$)/, {
      timeout: 60_000,
    })
  }

  /**
   * Assert invalid credentials error is shown
   */
  async expectInvalidCredentialsError() {
    // The app shows "CredentialsSignin" error - use first() to avoid strict mode violation
    await expect(
      this.page.getByText(/invalid|incorrect|wrong|credentials|CredentialsSignin/i).first()
    ).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert email validation error (HTML5)
   */
  async expectEmailValidationError() {
    // Check HTML5 validity
    const isInvalid = await this.emailInput.evaluate(
      (el) => !(el as HTMLInputElement).validity.valid
    )
    expect(isInvalid).toBe(true)
  }

  /**
   * Assert password validation error
   */
  async expectPasswordValidationError() {
    const isInvalid = await this.passwordInput.evaluate(
      (el) => !(el as HTMLInputElement).validity.valid
    )
    expect(isInvalid).toBe(true)
  }

  /**
   * Check if still on login page (no redirect happened)
   */
  async expectStillOnLoginPage() {
    await expect(this.page).toHaveURL(/\/login/)
  }
}
