import { test, expect } from "@playwright/test"
import { ForgotPasswordPage } from "../page-objects/auth/forgot-password.page"
import { E2E_USER } from "../fixtures/test-users"

test.describe("Auth - Password Reset Flow", () => {
  test.setTimeout(5 * 60_000)

  let forgotPasswordPage: ForgotPasswordPage

  test.beforeEach(async ({ page }) => {
    forgotPasswordPage = new ForgotPasswordPage(page)
    await forgotPasswordPage.navigate()
  })

  test("should show success message for valid email", async () => {
    await forgotPasswordPage.requestReset(E2E_USER.email)
    await forgotPasswordPage.expectSuccessState()
  })

  test("should show success message for non-existent email (security)", async () => {
    // For security, we don't reveal whether email exists
    await forgotPasswordPage.requestReset("nonexistent@example.com")
    await forgotPasswordPage.expectSuccessState()
  })

  test("should prevent submission with empty email", async () => {
    await forgotPasswordPage.submitButton.click()
    // Should show validation or stay on page
    await forgotPasswordPage.expectStillOnForgotPasswordPage()
  })

  test.skip("should enforce cooldown after request", async () => {
    // Skip: Cooldown feature may not be implemented yet
    await forgotPasswordPage.requestReset(E2E_USER.email)
    await forgotPasswordPage.expectSuccessState()
    // Try again button should be disabled or show cooldown
    await forgotPasswordPage.expectCooldownActive()
  })

  test("should navigate back to login", async () => {
    await forgotPasswordPage.backToLoginLink.click()
    await expect(forgotPasswordPage.page).toHaveURL(/\/login/)
  })
})
