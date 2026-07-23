import { describe, expect, it } from "vitest"
import { isProxyPathAllowed, normalizeProxyPath } from "@/lib/security/proxy-allowlist"

describe("proxy-allowlist", () => {
  it("allows known core API paths", () => {
    expect(isProxyPathAllowed("core", ["projects", "abc", "tasks"])).toBe(true)
    expect(isProxyPathAllowed("core", ["Profile", "me"])).toBe(true)
    expect(isProxyPathAllowed("core", ["notifications"])).toBe(true)
  })

  it("blocks internal and metrics paths on all services", () => {
    expect(isProxyPathAllowed("core", ["internal", "code-analysis"])).toBe(false)
    expect(isProxyPathAllowed("core", ["metrics"])).toBe(false)
    expect(isProxyPathAllowed("auth", ["swagger"])).toBe(false)
    expect(isProxyPathAllowed("ml", ["health"])).toBe(false)
  })

  it("returns false for unknown core paths", () => {
    expect(isProxyPathAllowed("core", ["for-internal", "integrations"])).toBe(false)
    expect(isProxyPathAllowed("core", ["unknown-endpoint"])).toBe(false)
  })

  it("allows auth and ml prefixes", () => {
    expect(isProxyPathAllowed("auth", ["auth", "oauth", "exchange"])).toBe(true)
    expect(isProxyPathAllowed("auth", ["auth", "login"])).toBe(true)
    expect(isProxyPathAllowed("ml", ["ai", "chat"])).toBe(true)
    expect(isProxyPathAllowed("ml", ["internal", "models"])).toBe(false)
  })

  it("normalizes encoded segments", () => {
    expect(normalizeProxyPath(["Profile", "me"])).toBe("profile/me")
  })
})
