import type { FullConfig } from "@playwright/test"
import { spawnSync } from "node:child_process"
import path from "node:path"
import { fileURLToPath } from "node:url"

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

function runOrThrow(command: string, args: string[], cwd: string) {
  const result = spawnSync(command, args, {
    cwd,
    stdio: "inherit",
    shell: false,
    env: process.env,
  })

  if (result.error) throw result.error
  if (result.status !== 0) {
    throw new Error(`Command failed: ${command} ${args.join(" ")}`)
  }
}

function runOrThrowQuiet(command: string, args: string[], cwd: string) {
  const result = spawnSync(command, args, {
    cwd,
    stdio: "pipe",
    shell: false,
    env: process.env,
    encoding: "utf-8",
  })

  if (result.error) throw result.error
  if (result.status !== 0) {
    throw new Error(`Command failed: ${command} ${args.join(" ")}`)
  }
}

async function waitForHttpOk(url: string, timeoutMs: number) {
  const startedAt = Date.now()
  let lastError: unknown = undefined

  // Node 18+ has global fetch; Playwright runs on Node 20 in most setups.
  while (Date.now() - startedAt < timeoutMs) {
    try {
      const res = await fetch(url, { method: "GET" })
      if (res.ok) return
    } catch (err) {
      lastError = err
    }
    await new Promise((r) => setTimeout(r, 1000))
  }

  throw new Error(`Timed out waiting for ${url}. Last error: ${String(lastError)}`)
}

export default async function globalSetup(_config: FullConfig) {
  const repoRoot = path.resolve(__dirname, "..", "..", "..")
  const composeFile = path.join(repoRoot, "docker-compose.yml")
  const composeE2EFile = path.join(repoRoot, "docker-compose.e2e.yml")

  // Provide deterministic defaults to avoid docker-compose warnings about unset variables.
  // Must match docker-compose.e2e.yml values
  process.env.POSTGRES_PASSWORD ||= "devhunt_dev_password_123"
  process.env.RABBITMQ_DEFAULT_USER ||= "devhunt"
  process.env.RABBITMQ_DEFAULT_PASS ||= "devhunt_e2e_rabbit_password"
  process.env.JWT_KEY ||= "devhunt_e2e_jwt_key_min_32_characters_long!!"
  process.env.ENCRYPTION_KEY ||= "devhunt_e2e_encryption_key_32_chars!!"
  process.env.ENCRYPTION_IV ||= "devhunt_e2e_iv_16_chr"

  // Helpful, explicit failure if Docker isn't available.
  // Use a quiet probe to avoid printing Docker's noisy connection error.
  try {
    runOrThrowQuiet("docker", ["version"], repoRoot)
  } catch {
    throw new Error(
      "Docker engine is not available. Start Docker Desktop (WSL2/Hyper-V) and re-run `npm run test:e2e:full`."
    )
  }

  const clean = process.env.DEVHUNT_E2E_CLEAN !== "0"
  const composeArgsBase = ["compose", "-f", composeFile, "-f", composeE2EFile]

  if (clean) {
    // Hard reset for determinism (fresh DB + queues).
    runOrThrow("docker", [...composeArgsBase, "down", "-v"], repoRoot)
  }

  // Start minimal dependencies first.
  runOrThrow(
    "docker",
    [...composeArgsBase, "up", "-d", "--build", "db", "cache-service", "message-broker", "object-storage"],
    repoRoot
  )

  // Run migrations + seeding as one-off jobs (deterministic completion).
  runOrThrow("docker", [...composeArgsBase, "run", "--rm", "--build", "db-migrator"], repoRoot)

  // On Windows dev machines, Docker pulls/builds can be blocked by local firewall/proxy.
  // Run the seeder on the host to avoid relying on Docker image pulls.
  if (process.platform === "win32") {
    const postgresDb = process.env.POSTGRES_DB || "devhunt_db"
    const postgresUser = process.env.POSTGRES_USER || "postgres"
    const postgresPassword = process.env.POSTGRES_PASSWORD || "devhunt_dev_password_123"

    process.env.ConnectionStrings__DefaultConnection = `Host=localhost;Port=5432;Database=${postgresDb};Username=${postgresUser};Password=${postgresPassword}`
    process.env.ASPNETCORE_ENVIRONMENT ||= "Development"
    process.env.DEVHUNT_E2E_USER_EMAIL ||= "e2e@devhunt.local"
    process.env.DEVHUNT_E2E_USER_PASSWORD ||= "TestPassword123!"
    process.env.DEVHUNT_SEEDER_SKIP_PROJECTS ||= "true"

    runOrThrow(
      "dotnet",
      [
        "run",
        "-c",
        "Release",
        "--project",
        path.join(repoRoot, "DevHunt.DatabaseSeeder", "DevHunt.DatabaseSeeder.csproj"),
      ],
      repoRoot
    )
  } else {
    process.env.DEVHUNT_SEEDER_SKIP_PROJECTS ||= "true"
    runOrThrow("docker", [...composeArgsBase, "run", "--rm", "db-seeder"], repoRoot)
  }

  // Bring up the app stack.
  runOrThrow(
    "docker",
    [
      ...composeArgsBase,
      "up",
      "-d",
      "--build",
      "auth-service",
      "core-api",
      "integration-gateway",
      "notification-service",
      "frontend",
    ],
    repoRoot
  )

  // Wait for readiness.
  await waitForHttpOk("http://localhost:7001/health", 180_000)
  await waitForHttpOk("http://localhost:7002/health", 180_000)
  // Next.js healthcheck accepts any status, so use login page to validate routing.
  await waitForHttpOk("http://localhost:3000/en/login", 180_000)
}
