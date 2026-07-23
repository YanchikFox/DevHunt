import { test, expect } from "@playwright/test"
import { RegisterPage } from "../page-objects/auth/register.page"
import { generateTestUser, E2E_USER, WEAK_PASSWORDS } from "../fixtures/test-users"

test.describe("Auth - Registration Flow", () => {
  test.setTimeout(5 * 60_000)

  let registerPage: RegisterPage

  test.beforeEach(async ({ page }) => {
    registerPage = new RegisterPage(page)
    await registerPage.navigate()
  })

  test("should register new user and redirect to verify-email or complete-profile", async () => {
    const testUser = generateTestUser()
    await registerPage.register(testUser)
    // App may redirect to verify-email or complete-profile depending on config
    await registerPage.page.waitForURL(/\/(verify-email|register\/complete-profile|dashboard)/, {
      timeout: 60_000,
    })
  })

  test("should show error when passwords do not match", async () => {
    const testUser = generateTestUser()
    await registerPage.fillForm({
      ...testUser,
      confirmPassword: "DifferentPassword123!",
    })
    await registerPage.submit()
    await registerPage.expectPasswordMismatchError()
  })

  test("should reject password that is too short", async () => {
    const testUser = generateTestUser()
    await registerPage.fillForm({
      ...testUser,
      password: WEAK_PASSWORDS.tooShort,
      confirmPassword: WEAK_PASSWORDS.tooShort,
    })
    await registerPage.submit()
    await registerPage.expectPasswordComplexityError()
  })

  test("should reject password without uppercase letter", async () => {
    const testUser = generateTestUser()
    await registerPage.fillForm({
      ...testUser,
      password: WEAK_PASSWORDS.noUppercase,
      confirmPassword: WEAK_PASSWORDS.noUppercase,
    })
    await registerPage.submit()
    await registerPage.expectPasswordComplexityError()
  })

  test("should reject password without lowercase letter", async () => {
    const testUser = generateTestUser()
    await registerPage.fillForm({
      ...testUser,
      password: WEAK_PASSWORDS.noLowercase,
      confirmPassword: WEAK_PASSWORDS.noLowercase,
    })
    await registerPage.submit()
    await registerPage.expectPasswordComplexityError()
  })

  test("should reject password without digit", async () => {
    const testUser = generateTestUser()
    await registerPage.fillForm({
      ...testUser,
      password: WEAK_PASSWORDS.noDigit,
      confirmPassword: WEAK_PASSWORDS.noDigit,
    })
    await registerPage.submit()
    await registerPage.expectPasswordComplexityError()
  })

  test("should reject password without special character", async () => {
    const testUser = generateTestUser()
    await registerPage.fillForm({
      ...testUser,
      password: WEAK_PASSWORDS.noSpecial,
      confirmPassword: WEAK_PASSWORDS.noSpecial,
    })
    await registerPage.submit()
    await registerPage.expectPasswordComplexityError()
  })

  test("should show error for already registered email", async () => {
    // Use the existing E2E user email
    await registerPage.fillForm({
      fullName: "Duplicate User",
      email: E2E_USER.email,
      password: "TestPassword123!",
    })
    await registerPage.submit()
    await registerPage.expectDuplicateEmailError()
  })

  test("should navigate to login page", async () => {
    await registerPage.loginLink.click()
    await expect(registerPage.page).toHaveURL(/\/login/)
  })
})
