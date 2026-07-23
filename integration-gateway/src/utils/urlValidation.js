/**
 * URL validation utilities to prevent SSRF attacks (SEC-014).
 * Blocks requests to private/internal networks and localhost.
 */

import { logger } from "./logger.js";
import net from "net";

/**
 * Private/reserved IPv4 ranges (RFC 1918, RFC 5737, etc.)
 */
const PRIVATE_IPV4_RANGES = [
  { start: "10.0.0.0", end: "10.255.255.255" },
  { start: "172.16.0.0", end: "172.31.255.255" },
  { start: "192.168.0.0", end: "192.168.255.255" },
  { start: "127.0.0.0", end: "127.255.255.255" },
  { start: "169.254.0.0", end: "169.254.255.255" },
  { start: "0.0.0.0", end: "0.255.255.255" },
];

function ipToLong(ip) {
  const parts = ip.split(".").map(Number);
  return ((parts[0] << 24) | (parts[1] << 16) | (parts[2] << 8) | parts[3]) >>> 0;
}

function isPrivateIp(ip) {
  if (!net.isIPv4(ip)) return false;
  const ipLong = ipToLong(ip);
  return PRIVATE_IPV4_RANGES.some(
    (range) => ipLong >= ipToLong(range.start) && ipLong <= ipToLong(range.end)
  );
}

const BLOCKED_HOSTNAMES = new Set([
  "localhost",
  "core-api",
  "auth-service",
  "notification-service",
  "integration-gateway",
  "ml-service",
  "rabbitmq",
  "postgres",
  "redis",
  "metadata.google.internal",
  "metadata.aws.internal",
]);

/**
 * Validates a URL to prevent SSRF attacks.
 * Blocks private IPs, localhost, internal Docker hostnames, and cloud metadata endpoints.
 *
 * @param {string} url - The URL to validate
 * @param {object} [options] - Validation options
 * @param {boolean} [options.requireHttps=false] - Require HTTPS protocol
 * @returns {{ valid: boolean, reason?: string }}
 */
export function validateUrl(url, options = {}) {
  if (!url || typeof url !== "string") {
    return { valid: false, reason: "URL is required" };
  }

  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return { valid: false, reason: "Invalid URL format" };
  }

  // Protocol check
  const allowedProtocols = options.requireHttps ? ["https:"] : ["http:", "https:"];
  if (!allowedProtocols.includes(parsed.protocol)) {
    return { valid: false, reason: `Protocol ${parsed.protocol} is not allowed` };
  }

  const hostname = parsed.hostname.toLowerCase();

  // Block known internal hostnames
  if (BLOCKED_HOSTNAMES.has(hostname)) {
    logger.warn(`SSRF blocked: internal hostname ${hostname}`);
    return { valid: false, reason: "Internal hostnames are not allowed" };
  }

  // Block IP addresses that resolve to private ranges
  if (net.isIPv4(hostname)) {
    if (isPrivateIp(hostname)) {
      logger.warn(`SSRF blocked: private IP ${hostname}`);
      return { valid: false, reason: "Private IP addresses are not allowed" };
    }
  }

  // Block IPv6 loopback and private
  if (net.isIPv6(hostname) || hostname === "[::1]" || hostname.startsWith("[")) {
    logger.warn(`SSRF blocked: IPv6 address ${hostname}`);
    return { valid: false, reason: "IPv6 addresses are not allowed in webhook URLs" };
  }

  // Block cloud metadata endpoints (AWS, GCP, Azure)
  if (hostname === "169.254.169.254" || hostname.endsWith(".internal")) {
    logger.warn(`SSRF blocked: cloud metadata endpoint ${hostname}`);
    return { valid: false, reason: "Cloud metadata endpoints are not allowed" };
  }

  return { valid: true };
}

/**
 * Validates a redirect URI against an allowlist of domains.
 *
 * @param {string} uri - The redirect URI to validate
 * @param {string[]} allowedOrigins - List of allowed origin domains
 * @returns {{ valid: boolean, reason?: string }}
 */
export function validateRedirectUri(uri, allowedOrigins) {
  const baseResult = validateUrl(uri);
  if (!baseResult.valid) return baseResult;

  if (!allowedOrigins || allowedOrigins.length === 0) {
    return { valid: true }; // No allowlist configured — allow any public URL
  }

  let parsed;
  try {
    parsed = new URL(uri);
  } catch {
    return { valid: false, reason: "Invalid redirect URI" };
  }

  const origin = parsed.origin.toLowerCase();
  const isAllowed = allowedOrigins.some(
    (allowed) => origin === allowed.toLowerCase()
  );

  if (!isAllowed) {
    logger.warn(`Redirect URI blocked: ${origin} not in allowlist`);
    return { valid: false, reason: "Redirect URI origin is not in the allowlist" };
  }

  return { valid: true };
}
