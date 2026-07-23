/**
 * Test user fixtures for E2E tests
 */

export const E2E_USER = {
  email: process.env.DEVHUNT_E2E_USER_EMAIL ?? "e2e@devhunt.local",
  password: process.env.DEVHUNT_E2E_USER_PASSWORD ?? "TestPassword123!",
  fullName: "E2E Test User",
}

/**
 * Generate a unique test user with timestamp-based email
 */
export function generateTestUser() {
  const timestamp = Date.now()
  return {
    email: `e2e-test-${timestamp}@devhunt.local`,
    password: "TestPassword123!",
    fullName: `E2E User ${timestamp}`,
    confirmPassword: "TestPassword123!",
  }
}

/**
 * Invalid credential combinations for negative tests
 */
export const INVALID_CREDENTIALS = {
  wrongPassword: {
    email: "e2e@devhunt.local",
    password: "WrongPassword123!",
  },
  nonExistentEmail: {
    email: "nonexistent@devhunt.local",
    password: "TestPassword123!",
  },
  invalidEmailFormat: {
    email: "not-an-email",
    password: "TestPassword123!",
  },
}

/**
 * Weak passwords for password complexity validation tests
 * Password requirements: 8+ chars, uppercase, lowercase, digit, special char
 */
export const WEAK_PASSWORDS = {
  tooShort: "Pass1!", // 6 chars, missing length
  noUppercase: "password123!", // no uppercase
  noLowercase: "PASSWORD123!", // no lowercase
  noDigit: "PasswordTest!", // no digit
  noSpecial: "Password123", // no special char
}
