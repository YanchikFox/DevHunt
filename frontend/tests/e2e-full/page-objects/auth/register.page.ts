import { expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Register Page Object
 * Covers: /[locale]/register
 */
export class RegisterPage extends BasePage {
  // Form elements
  get fullNameInput() {
    return this.page.locator("#fullName")
  }

  get emailInput() {
    return this.page.locator("#email")
  }

  get passwordInput() {
    return this.page.locator("#password")
  }

  get confirmPasswordInput() {
    return this.page.locator("#confirmPassword")
  }

  get submitButton() {
    return this.page.locator('button[type="submit"]')
  }

  // Navigation links
  get loginLink() {
    return this.page.getByRole("link", { name: /login|sign in/i })
  }

  // Error elements
  get formError() {
    return this.page.locator('[role="alert"]')
  }

  /**
   * Navigate to register page
   */
  async navigate() {
    await this.goto("/register")
    await expect(this.fullNameInput).toBeVisible({ timeout: 30_000 })
  }

  /**
   * Fill all registration form fields
   */
  async fillForm(data: {
    fullName: string
    email: string
    password: string
    confirmPassword?: string
  }) {
    await this.fullNameInput.fill(data.fullName)
    await this.emailInput.fill(data.email)
    await this.passwordInput.fill(data.password)
    await this.confirmPasswordInput.fill(data.confirmPassword ?? data.password)
  }

  /**
   * Click submit button
   */
  async submit() {
    await this.submitButton.click()
    // Wait for client-side validation to complete
    await this.page.waitForTimeout(500)
  }

  /**
   * Fill form and submit
   */
  async register(data: { fullName: string; email: string; password: string }) {
    await this.fillForm(data)
    await this.submit()
  }

  /**
   * Assert password mismatch error
   */
  async expectPasswordMismatchError() {
    await expect(
      this.page.getByText(/passwords don't match|passwords do not match|must match/i)
    ).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert password complexity error
   */
  async expectPasswordComplexityError() {
    // Look for password-related error messages (either min length or complexity)
    const errorLocator = this.page.locator('[role="alert"]').filter({
      hasText: /must be at least|must include|upper.*lower|password.*character/i,
    })
    await expect(errorLocator.first()).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert duplicate email error
   */
  async expectDuplicateEmailError() {
    await expect(
      this.page
        .getByText(/already exists|already registered|email.*taken|in use/i)
        .first()
    ).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert redirect to email verification page
   */
  async expectRedirectToVerifyEmail() {
    await this.page.waitForURL(/\/verify-email/, { timeout: 60_000 })
  }

  /**
   * Assert redirect to complete profile page
   */
  async expectRedirectToCompleteProfile() {
    await this.page.waitForURL(/\/register\/complete-profile/, { timeout: 60_000 })
  }

  /**
   * Check if still on register page
   */
  async expectStillOnRegisterPage() {
    await expect(this.page).toHaveURL(/\/register/)
  }
}
