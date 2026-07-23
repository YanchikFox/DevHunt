import { test, expect } from "@playwright/test"

test.describe("Auth pages smoke (CI-stable)", () => {
  test("login page renders", async ({ page }) => {
    await page.goto("/ru/login")
    await expect(page.locator("form")).toBeVisible()
    await expect(page.locator("#email")).toBeVisible()
    await expect(page.locator("#password")).toBeVisible()
    await expect(page.locator('button[type="submit"]')).toBeVisible()
  })

  test("login validates invalid email client-side", async ({ page }) => {
    await page.goto("/ru/login")
    await page.fill("#email", "invalid-email")
    await page.fill("#password", "password123")
    await page.click('button[type="submit"]')

    // The email field is `type="email"`, so the browser's native validation
    // can block submit before any custom error text is rendered.
    await expect(page.locator("#email")).toBeFocused()
    const isEmailValid = await page
      .locator("#email")
      .evaluate((el) => (el as HTMLInputElement).validity.valid)
    expect(isEmailValid).toBe(false)
  })

  test("register page renders", async ({ page }) => {
    await page.goto("/ru/register")
    await expect(page.locator("form")).toBeVisible()
    await expect(page.locator("#fullName")).toBeVisible()
    await expect(page.locator("#email")).toBeVisible()
    await expect(page.locator("#password")).toBeVisible()
    await expect(page.locator("#confirmPassword")).toBeVisible()
    await expect(page.locator('button[type="submit"]')).toBeVisible()
  })

  test("register validates password mismatch client-side", async ({ page }) => {
    await page.goto("/ru/register")
    await page.fill("#fullName", "John Doe")
    await page.fill("#email", "john@example.com")
    await page.fill("#password", "Password1!")
    await page.fill("#confirmPassword", "Password2!")
    await page.click('button[type="submit"]')

    await expect(page.getByText("Hasła nie są identyczne")).toBeVisible()
  })
})
