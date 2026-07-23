/**
 * Restricts /metrics to loopback clients or X-Metrics-Token (required in production).
 */

const MIN_TOKEN_LENGTH = 16;

function isProduction() {
  return (process.env.ENVIRONMENT || "development").toLowerCase() === "production";
}

function isLoopbackAddress(ip) {
  if (!ip) return false;
  const normalized = ip.replace("::ffff:", "");
  return (
    normalized === "127.0.0.1" ||
    normalized === "::1" ||
    normalized.startsWith("127.")
  );
}

function getClientIp(req) {
  return req.ip || req.socket?.remoteAddress || "";
}

/**
 * @param {import("express").Request} req
 * @param {import("express").Response} res
 * @param {import("express").NextFunction} next
 */
export function requireMetricsAuth(req, res, next) {
  const expectedToken = process.env.METRICS_TOKEN;

  if (isProduction() && (!expectedToken || expectedToken.length < MIN_TOKEN_LENGTH)) {
    res.status(503).send("METRICS_TOKEN must be configured in production");
    return;
  }

  if (expectedToken) {
    const provided = req.get("X-Metrics-Token");
    if (provided === expectedToken) {
      next();
      return;
    }
  }

  if (isLoopbackAddress(getClientIp(req))) {
    next();
    return;
  }

  res.status(403).send("Forbidden: metrics require X-Metrics-Token or local access");
}
