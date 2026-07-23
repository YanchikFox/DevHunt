import { describe, it, expect } from "vitest"
import {
  DEFAULT_SAFE_REDIRECT,
  isSafeRedirectUrl,
  resolveSafeRedirectUrl,
} from "@/lib/security/safe-redirect"

describe("isSafeRedirectUrl", () => {
  it("allows internal paths", () => {
    expect(isSafeRedirectUrl("/dashboard")).toBe(true)
    expect(isSafeRedirectUrl("/profile")).toBe(true)
    expect(isSafeRedirectUrl("/projects/abc")).toBe(true)
  })

  it("blocks absolute URLs", () => {
    expect(isSafeRedirectUrl("https://evil.com")).toBe(false)
    expect(isSafeRedirectUrl("http://evil.com")).toBe(false)
  })

  it("blocks scheme-relative URLs", () => {
    expect(isSafeRedirectUrl("//evil.com")).toBe(false)
    expect(isSafeRedirectUrl("//evil.com/path")).toBe(false)
  })

  it("blocks javascript and other schemes", () => {
    expect(isSafeRedirectUrl("javascript:alert(1)")).toBe(false)
    expect(isSafeRedirectUrl("/javascript:alert(1)")).toBe(false)
  })

  it("blocks backslash open redirects", () => {
    expect(isSafeRedirectUrl("/\\evil.com")).toBe(false)
  })
})

describe("resolveSafeRedirectUrl", () => {
  it("returns safe path when valid", () => {
    expect(resolveSafeRedirectUrl("/profile")).toBe("/profile")
  })

  it("falls back for unsafe values", () => {
    expect(resolveSafeRedirectUrl("https://evil.com")).toBe(DEFAULT_SAFE_REDIRECT)
    expect(resolveSafeRedirectUrl("//evil.com")).toBe(DEFAULT_SAFE_REDIRECT)
    expect(resolveSafeRedirectUrl("javascript:alert(1)")).toBe(DEFAULT_SAFE_REDIRECT)
    expect(resolveSafeRedirectUrl(null)).toBe(DEFAULT_SAFE_REDIRECT)
  })

  it("supports custom fallback", () => {
    expect(resolveSafeRedirectUrl("https://evil.com", "/")).toBe("/")
  })
})
