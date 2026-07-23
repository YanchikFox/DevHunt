import { defineConfig, devices } from "@playwright/test"

// Quick config for running e2e-full tests when Docker is already running
export default defineConfig({
  testDir: "./tests/e2e-full",
  fullyParallel: false,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  workers: 1,
  reporter: "list",
  // No globalSetup/globalTeardown - assume Docker is already running
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
