/**
 * HMAC headers for Core API internal service authentication.
 * Must match DevHunt.CoreApi.Security.InternalServiceAuthenticator.
 */

import crypto from "crypto";

export const INTERNAL_SERVICE_NAME = "integration-gateway";
const MIN_KEY_LENGTH = 32;
const DEV_FALLBACK_KEY = "dev-internal-key-for-local-development-only";

/**
 * @returns {boolean}
 */
export function isProductionEnvironment() {
  const env =
    process.env.ENVIRONMENT ||
    process.env.ASPNETCORE_ENVIRONMENT ||
    process.env.NODE_ENV ||
    "development";
  return env.toLowerCase() === "production";
}

/**
 * @returns {string}
 */
export function getInternalApiKey() {
  const key = process.env.INTERNAL_API_KEY;
  if (isProductionEnvironment()) {
    if (!key || key.length < MIN_KEY_LENGTH) {
      throw new Error(
        `INTERNAL_API_KEY must be configured (min ${MIN_KEY_LENGTH} chars) in production`,
      );
    }
    return key;
  }
  return key || DEV_FALLBACK_KEY;
}

/**
 * Ensures INTERNAL_API_KEY is present in production. Call at process startup.
 */
export function assertInternalApiKeyConfigured() {
  getInternalApiKey();
}

/**
 * Normalizes a URL or path to the signing resource (request path only).
 * @param {string} urlOrPath
 * @returns {string}
 */
export function signingResourceFromUrl(urlOrPath) {
  if (!urlOrPath) {
    return "/";
  }
  if (urlOrPath.includes("://")) {
    return new URL(urlOrPath).pathname;
  }
  return urlOrPath.startsWith("/") ? urlOrPath : `/${urlOrPath}`;
}

/**
 * Builds Authorization + service headers with HMAC for the given signing resource.
 * @param {string} resource - Path (e.g. /api/internal/...) or integration GUID for /token endpoint
 * @returns {Record<string, string>}
 */
export function buildInternalApiHeaders(resource) {
  const apiKey = getInternalApiKey();
  const timestamp = Math.floor(Date.now() / 1000).toString();
  const message = `${INTERNAL_SERVICE_NAME}|${timestamp}|${resource}`;
  const signature = crypto
    .createHmac("sha256", apiKey)
    .update(message)
    .digest("base64");

  return {
    Authorization: `Bearer ${apiKey}`,
    "X-Service-Name": INTERNAL_SERVICE_NAME,
    "X-Request-Timestamp": timestamp,
    "X-Request-Signature": signature,
  };
}

/**
 * @param {string} urlOrPath
 * @returns {Record<string, string>}
 */
export function buildInternalApiHeadersForUrl(urlOrPath) {
  return buildInternalApiHeaders(signingResourceFromUrl(urlOrPath));
}

/**
 * Token endpoint signs with integration id, not request path.
 * @param {string} integrationId
 * @returns {Record<string, string>}
 */
export function buildInternalApiHeadersForIntegrationToken(integrationId) {
  return buildInternalApiHeaders(String(integrationId));
}

/**
 * @param {string} urlOrPath
 * @param {Record<string, string>} [extra]
 * @returns {Record<string, string>}
 */
export function mergeInternalApiHeaders(urlOrPath, extra = {}) {
  return { ...buildInternalApiHeadersForUrl(urlOrPath), ...extra };
}
