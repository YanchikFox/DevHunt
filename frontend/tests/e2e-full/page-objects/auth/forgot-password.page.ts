import { expect } from "@playwright/test"
import { BasePage } from "../base.page"

/**
 * Forgot Password Page Object
 * Covers: /[locale]/forgot-password
 */
export class ForgotPasswordPage extends BasePage {
  // Form elements
  get emailInput() {
    return this.page.locator("#email")
  }

  get submitButton() {
    return this.page.getByRole("button", { name: /send|submit|reset/i })
  }

  // Navigation
  get backToLoginLink() {
    return this.page.getByRole("link", { name: /back|login|sign in/i })
  }

  // State elements
  get successMessage() {
    return this.page.getByText(/check your inbox|email sent|sent.*email|if.*exists/i)
  }

  get tryAgainButton() {
    return this.page.getByRole("button", { name: /try again|resend/i })
  }

  get cooldownTimer() {
    return this.page.getByText(/\d+.*seconds?|wait/i)
  }

  /**
   * Navigate to forgot password page
   */
  async navigate() {
    await this.goto("/forgot-password")
    await expect(this.emailInput).toBeVisible({ timeout: 30_000 })
  }

  /**
   * Fill email and submit reset request
   */
  async requestReset(email: string) {
    await this.emailInput.fill(email)
    await this.submitButton.click()
  }

  /**
   * Assert success state is shown
   * Note: For security, success is always shown regardless of email existence
   */
  async expectSuccessState() {
    await expect(this.successMessage).toBeVisible({ timeout: 10_000 })
  }

  /**
   * Assert cooldown is active (shows countdown)
   */
  async expectCooldownActive() {
    // After submitting, submit button should be disabled or cooldown timer shown
    const isDisabled = await this.submitButton.isDisabled()
    const hasCooldownText = await this.isVisible(this.cooldownTimer, 5_000)
    expect(isDisabled || hasCooldownText).toBe(true)
  }

  /**
   * Assert email validation error
   */
  async expectEmailValidationError() {
    const isInvalid = await this.emailInput.evaluate(
      (el) => !(el as HTMLInputElement).validity.valid
    )
    expect(isInvalid).toBe(true)
  }

  /**
   * Check if still on forgot password page
   */
  async expectStillOnForgotPasswordPage() {
    await expect(this.page).toHaveURL(/\/forgot-password/)
  }
}
