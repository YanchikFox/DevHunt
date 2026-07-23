import { describe, it, expect } from "vitest"
import { readFileSync } from "fs"
import { join } from "path"
import { fileURLToPath } from "url"
import { dirname } from "path"

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)

describe("Next.js Configuration", () => {
  it("should have valid next.config.mjs with i18n plugin", async () => {
    const configPath = join(__dirname, "../../next.config.mjs")
    const configContent = readFileSync(configPath, "utf-8")

    // Check that next-intl plugin is configured
    expect(configContent).toContain("next-intl/plugin")
    expect(configContent).toContain("createNextIntlPlugin")

    // Check that i18n request file path is specified
    expect(configContent).toMatch(/\.\/src\/i18n\/request\.ts/)
  })

  it("should have i18n request.ts file", () => {
    const requestPath = join(__dirname, "../../src/i18n/request.ts")
    const requestContent = readFileSync(requestPath, "utf-8")

    // Check that getRequestConfig is imported and used
    expect(requestContent).toContain("getRequestConfig")
    expect(requestContent).toContain("next-intl/server")
  })

  it("should have i18n routing.ts file", () => {
    const routingPath = join(__dirname, "../../src/i18n/routing.ts")
    const routingContent = readFileSync(routingPath, "utf-8")

    // Check that routing is configured
    expect(routingContent).toContain("defineRouting")
    expect(routingContent).toContain("locales")
  })
})
