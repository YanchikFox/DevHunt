import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { notImplemented } from "../src/middleware/notImplemented.js";

describe("notImplemented middleware", () => {
  it("returns 501 with explicit message", () => {
    const middleware = notImplemented("Bulk notifications");
    const body = {};
    const res = {
      statusCode: 0,
      payload: null,
      status(code) {
        this.statusCode = code;
        return this;
      },
      json(payload) {
        this.payload = payload;
        return this;
      },
    };

    middleware({}, res);

    assert.equal(res.statusCode, 501);
    assert.equal(res.payload.code, "NOT_IMPLEMENTED");
    assert.match(res.payload.message, /Core API/);
  });
});
