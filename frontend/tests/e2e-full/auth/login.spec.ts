import { test, expect } from "@playwright/test"
import { LoginPage } from "../page-objects/auth/login.page"
import { E2E_USER, INVALID_CREDENTIALS } from "../fixtures/test-users"

test.describe("Auth - Login Flow", () => {
  test.setTimeout(5 * 60_000)

  let loginPage: LoginPage

  test.beforeEach(async ({ page }) => {
    loginPage = new LoginPage(page)
    await loginPage.navigate()
  })

  test("should login with valid credentials and redirect to dashboard", async () => {
    await loginPage.loginAndWaitForDashboard(E2E_USER.email, E2E_USER.password)
    await expect(loginPage.page).toHaveURL(/\/(dashboard|projects|community)/)
  })

  test("should show error for invalid password", async () => {
    await loginPage.login(
      INVALID_CREDENTIALS.wrongPassword.email,
      INVALID_CREDENTIALS.wrongPassword.password
    )
    await loginPage.expectInvalidCredentialsError()
  })

  test("should show error for non-existent email", async () => {
    await loginPage.login(
      INVALID_CREDENTIALS.nonExistentEmail.email,
      INVALID_CREDENTIALS.nonExistentEmail.password
    )
    await loginPage.expectInvalidCredentialsError()
  })

  test("should validate email format client-side", async () => {
    await loginPage.fillCredentials(
      INVALID_CREDENTIALS.invalidEmailFormat.email,
      INVALID_CREDENTIALS.invalidEmailFormat.password
    )
    await loginPage.submit()
    await loginPage.expectEmailValidationError()
  })

  test("should prevent submission with empty fields", async () => {
    await loginPage.submit()
    // Form should not navigate away - either HTML5 validation or page stays
    await loginPage.expectStillOnLoginPage()
  })

  test("should display OAuth login options", async () => {
    await expect(loginPage.githubButton).toBeVisible()
    await expect(loginPage.googleButton).toBeVisible()
  })

  test("should navigate to register page", async () => {
    await loginPage.registerLink.click()
    await expect(loginPage.page).toHaveURL(/\/register/)
  })

  test("should navigate to forgot password page", async () => {
    await loginPage.forgotPasswordLink.click()
    await expect(loginPage.page).toHaveURL(/\/forgot-password/)
  })
})
