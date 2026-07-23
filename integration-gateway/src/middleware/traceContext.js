import { randomUUID } from "node:crypto";

export function traceContext(req, res, next) {
  // Basic W3C traceparent propagation
  const incoming = req.headers["traceparent"];
  const traceId = incoming?.split("-")[1] || randomUUID().replace(/-/g, "").slice(0, 32);
  const spanId = randomUUID().replace(/-/g, "").slice(0, 16);
  const traceparent = `00-${traceId}-${spanId}-01`;

  req.traceparent = traceparent;
  res.setHeader("traceparent", traceparent);

  // Attach to logger if exists
  if (req.log) {
    req.log = req.log.child({ traceparent });
  }
  next();
}
