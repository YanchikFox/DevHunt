import { describe, it, beforeEach, afterEach } from "node:test";
import assert from "node:assert/strict";

const ORIGINAL_ENV = { ...process.env };

function restoreEnv() {
  for (const key of Object.keys(process.env)) {
    if (!(key in ORIGINAL_ENV)) {
      delete process.env[key];
    }
  }
  Object.assign(process.env, ORIGINAL_ENV);
}

function createMockReq({ ip = "203.0.113.1", token } = {}) {
  return {
    ip,
    socket: { remoteAddress: ip },
    get(name) {
      if (name === "X-Metrics-Token") return token;
      return undefined;
    },
  };
}

function createMockRes() {
  const res = {
    statusCode: 200,
    body: "",
    status(code) {
      this.statusCode = code;
      return this;
    },
    send(body) {
      this.body = body;
      return this;
    },
  };
  return res;
}

describe("requireMetricsAuth", () => {
  beforeEach(() => restoreEnv());
  afterEach(() => restoreEnv());

  it("allows loopback without token in development", async () => {
    process.env.ENVIRONMENT = "development";
    delete process.env.METRICS_TOKEN;

    const { requireMetricsAuth } = await import(
      "../src/middleware/metricsAuth.js"
    );
    const req = createMockReq({ ip: "127.0.0.1" });
    const res = createMockRes();
    let called = false;
    requireMetricsAuth(req, res, () => {
      called = true;
    });
    assert.equal(called, true);
  });

  it("allows remote client with valid token", async () => {
    process.env.METRICS_TOKEN = "metrics-secret-token-32chars";
    const { requireMetricsAuth } = await import(
      "../src/middleware/metricsAuth.js"
    );
    const req = createMockReq({
      ip: "203.0.113.5",
      token: process.env.METRICS_TOKEN,
    });
    const res = createMockRes();
    let called = false;
    requireMetricsAuth(req, res, () => {
      called = true;
    });
    assert.equal(called, true);
  });

  it("denies remote client without token", async () => {
    process.env.METRICS_TOKEN = "metrics-secret-token-32chars";
    const { requireMetricsAuth } = await import(
      "../src/middleware/metricsAuth.js"
    );
    const req = createMockReq({ ip: "203.0.113.5" });
    const res = createMockRes();
    requireMetricsAuth(req, res, () => {});
    assert.equal(res.statusCode, 403);
  });
});
