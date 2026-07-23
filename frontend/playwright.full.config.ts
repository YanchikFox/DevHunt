import { defineConfig, devices } from "@playwright/test"

export default defineConfig({
  testDir: "./tests/e2e-full",
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: "html",
  globalSetup: "./tests/e2e-full/global-setup",
  globalTeardown: "./tests/e2e-full/global-teardown",
  use: {
    baseURL: "http://localhost:3000",
    trace: "on-first-retry",
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
})
