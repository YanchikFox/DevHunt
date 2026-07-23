import { Page, Response } from "@playwright/test"

export const E2E_EMAIL = process.env.DEVHUNT_E2E_USER_EMAIL ?? "e2e@devhunt.local"
export const E2E_PASSWORD = process.env.DEVHUNT_E2E_USER_PASSWORD ?? "TestPassword123!"

export async function login(page: Page) {
  await page.goto("/en/login", { waitUntil: "domcontentloaded" })

  // Wait for form to be ready
  const emailInput = page.locator("#email")
  await emailInput.waitFor({ state: "visible", timeout: 30_000 })

  await emailInput.fill(E2E_EMAIL)
  await page.locator("#password").fill(E2E_PASSWORD)

  // Click submit and wait for navigation
  await Promise.all([
    page.waitForURL(/\/en\/(dashboard|projects|community|$)/, { timeout: 60_000 }),
    page.locator('button[type="submit"]').click(),
  ])
}

function isProxyCoreUrl(url: string) {
  return url.includes("/api/proxy-core")
}

export async function waitForOkResponse(
  page: Page,
  options: {
    method: string
    urlMatch: RegExp | ((url: string) => boolean)
    timeout?: number
    label?: string
    requireProxyCore?: boolean
  }
): Promise<Response> {
  const {
    method,
    urlMatch,
    timeout = 60_000,
    label = "request",
    requireProxyCore = true,
  } = options

  const response = await page.waitForResponse(
    (res) => {
      if (res.request().method() !== method) return false
      const url = res.url()
      if (requireProxyCore && !isProxyCoreUrl(url)) return false
      return typeof urlMatch === "function" ? urlMatch(url) : urlMatch.test(url)
    },
    { timeout }
  )

  if (!response.ok()) {
    let body = ""
    try {
      body = await response.text()
    } catch {
      body = "<failed to read response body>"
    }

    throw new Error(
      `${label} failed: ${response.status()} ${response.statusText()}\n${body}`
    )
  }

  return response
}

export function tinyPngFilePayload() {
  // 1x1 transparent PNG
  const base64 =
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMB/ax0fQAAAABJRU5ErkJggg=="
  return {
    name: "e2e-tiny.png",
    mimeType: "image/png",
    buffer: Buffer.from(base64, "base64"),
  }
}

/**
 * Wait for auth API response (not proxied through core)
 */
export async function waitForAuthResponse(
  page: Page,
  options: {
    method: string
    urlMatch: RegExp
    timeout?: number
    label?: string
  }
): Promise<Response> {
  const { method, urlMatch, timeout = 60_000, label = "auth request" } = options

  const response = await page.waitForResponse(
    (res) => {
      if (res.request().method() !== method) return false
      return urlMatch.test(res.url())
    },
    { timeout }
  )

  if (!response.ok()) {
    let body = ""
    try {
      body = await response.text()
    } catch {
      body = "<failed to read response body>"
    }
    throw new Error(
      `${label} failed: ${response.status()} ${response.statusText()}\n${body}`
    )
  }

  return response
}

/**
 * Create a project via UI and return its title
 * Useful for tests that need a pre-existing project
 */
export async function createProjectViaUI(
  page: Page,
  options?: {
    title?: string
    description?: string
    technologies?: string[]
    roles?: string[]
  }
): Promise<string> {
  const timestamp = Date.now()
  const title = options?.title ?? `E2E Project ${timestamp}`
  const description =
    options?.description ??
    `E2E test project created at ${new Date().toISOString()}. This description meets the minimum length requirement.`

  await page.goto("/en/dashboard/projects", { waitUntil: "domcontentloaded" })

  // Wait for page to finish loading - either projects list or empty state with create button
  await page
    .locator('button:has-text("Create"), [data-testid="project-card"], .project-card')
    .first()
    .waitFor({ state: "visible", timeout: 30_000 })

  // Open create dialog - handle both button variants
  const createBtn = page.getByRole("button", { name: /create.*project/i })
  const createFirstBtn = page.getByRole("button", { name: /create your first project/i })
  const anyCreateBtn = page.locator('button:has-text("Create")')

  if ((await createBtn.count()) > 0) {
    await createBtn.first().click()
  } else if ((await createFirstBtn.count()) > 0) {
    await createFirstBtn.first().click()
  } else if ((await anyCreateBtn.count()) > 0) {
    await anyCreateBtn.first().click()
  } else {
    throw new Error("No create project button found in createProjectViaUI")
  }

  const dialog = page.getByRole("dialog", { name: "Create Project" })
  await dialog.waitFor({ state: "visible", timeout: 10_000 })

  await page.fill("#title", title)
  await page.fill("#description", description)

  // Add technologies if provided
  if (options?.technologies) {
    const techInput = dialog.getByRole("textbox", { name: "Technologies" })
    for (const tech of options.technologies) {
      await techInput.fill(tech)
      await techInput.press("Enter")
      // Small delay to ensure chip is added
      await page.waitForTimeout(200)
    }
  }

  // Add roles if provided
  if (options?.roles) {
    const rolesInput = dialog.getByRole("textbox", { name: "Required Roles" })
    for (const role of options.roles) {
      await rolesInput.fill(role)
      await rolesInput.press("Enter")
      await page.waitForTimeout(200)
    }
  }

  // Submit and wait for API response
  const createPromise = waitForOkResponse(page, {
    method: "POST",
    urlMatch: /\/projects(\?|$)/,
    label: "Create project",
  })
  await dialog.getByRole("button", { name: /^Create$/ }).click()
  await createPromise

  // Wait for dialog to close
  await dialog.waitFor({ state: "hidden", timeout: 60_000 })

  return title
}
