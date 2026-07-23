import type { FullConfig } from "@playwright/test"
import { spawnSync } from "node:child_process"
import path from "node:path"
import { fileURLToPath } from "node:url"

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

function run(command: string, args: string[], cwd: string) {
  spawnSync(command, args, {
    cwd,
    stdio: "inherit",
    shell: false,
    env: process.env,
  })
}

export default async function globalTeardown(_config: FullConfig) {
  const repoRoot = path.resolve(__dirname, "..", "..", "..")
  const composeFile = path.join(repoRoot, "docker-compose.yml")
  const composeE2EFile = path.join(repoRoot, "docker-compose.e2e.yml")

  // If Docker isn't available, nothing to tear down.
  const dockerCheck = spawnSync("docker", ["version"], {
    cwd: repoRoot,
    stdio: "ignore",
    shell: false,
    env: process.env,
  })
  if (dockerCheck.status !== 0) return

  const clean = process.env.DEVHUNT_E2E_CLEAN !== "0"
  const composeArgsBase = ["compose", "-f", composeFile, "-f", composeE2EFile]

  // Best-effort cleanup.
  if (clean) {
    run("docker", [...composeArgsBase, "down", "-v"], repoRoot)
  } else {
    run("docker", [...composeArgsBase, "down"], repoRoot)
  }
}
