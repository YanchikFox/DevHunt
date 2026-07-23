import { describe, it, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";
import crypto from "crypto";

const ORIGINAL_ENV = { ...process.env };

function restoreEnv() {
  for (const key of Object.keys(process.env)) {
    if (!(key in ORIGINAL_ENV)) {
      delete process.env[key];
    }
  }
  Object.assign(process.env, ORIGINAL_ENV);
}

describe("internalApiAuth", () => {
  beforeEach(() => {
    restoreEnv();
  });

  afterEach(() => {
    restoreEnv();
  });

  it("computes HMAC matching Core API format", async () => {
    process.env.INTERNAL_API_KEY = "a".repeat(32);
    process.env.ENVIRONMENT = "development";

    const { buildInternalApiHeaders } = await import(
      "../src/utils/internalApiAuth.js"
    );

    const resource = "/api/internal/code-analysis/results";
    const headers = buildInternalApiHeaders(resource);
    const timestamp = headers["X-Request-Timestamp"];
    const message = `integration-gateway|${timestamp}|${resource}`;
    const expected = crypto
      .createHmac("sha256", process.env.INTERNAL_API_KEY)
      .update(message)
      .digest("base64");

    assert.equal(headers["X-Request-Signature"], expected);
    assert.equal(headers["X-Service-Name"], "integration-gateway");
    assert.match(headers.Authorization, /^Bearer /);
  });

  it("uses integration id as resource for token endpoint", async () => {
    process.env.INTERNAL_API_KEY = "b".repeat(32);
    process.env.ENVIRONMENT = "development";

    const {
      buildInternalApiHeadersForIntegrationToken,
    } = await import("../src/utils/internalApiAuth.js");

    const integrationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    const headers = buildInternalApiHeadersForIntegrationToken(integrationId);
    const timestamp = headers["X-Request-Timestamp"];
    const message = `integration-gateway|${timestamp}|${integrationId}`;
    const expected = crypto
      .createHmac("sha256", process.env.INTERNAL_API_KEY)
      .update(message)
      .digest("base64");

    assert.equal(headers["X-Request-Signature"], expected);
  });

  it("extracts pathname from full URL", async () => {
    process.env.INTERNAL_API_KEY = "c".repeat(32);
    process.env.ENVIRONMENT = "development";

    const { buildInternalApiHeadersForUrl, signingResourceFromUrl } =
      await import("../src/utils/internalApiAuth.js");

    const url =
      "http://core-api:8080/api/integrations/project/abc/for-internal";
    assert.equal(
      signingResourceFromUrl(url),
      "/api/integrations/project/abc/for-internal",
    );

    const headers = buildInternalApiHeadersForUrl(url);
    assert.ok(headers["X-Request-Signature"]);
  });

  it("throws when production lacks INTERNAL_API_KEY", async () => {
    process.env.ENVIRONMENT = "production";
    delete process.env.INTERNAL_API_KEY;

    const { assertInternalApiKeyConfigured } = await import(
      "../src/utils/internalApiAuth.js"
    );

    assert.throws(
      () => assertInternalApiKeyConfigured(),
      /INTERNAL_API_KEY must be configured/,
    );
  });
});
