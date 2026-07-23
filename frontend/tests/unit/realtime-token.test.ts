import { describe, expect, it, vi, afterEach } from "vitest"
import { fetchRealtimeAccessToken } from "@/lib/auth/realtime-token"

describe("fetchRealtimeAccessToken", () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it("returns token from BFF route", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({ token: "jwt-test" }),
      }),
    )

    await expect(fetchRealtimeAccessToken()).resolves.toBe("jwt-test")
    expect(fetch).toHaveBeenCalledWith("/api/realtime/token", {
      credentials: "include",
      cache: "no-store",
    })
  })

  it("returns null when route is unauthorized", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue({
        ok: false,
      }),
    )

    await expect(fetchRealtimeAccessToken()).resolves.toBeNull()
  })
})
