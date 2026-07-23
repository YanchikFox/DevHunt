import { randomUUID } from "node:crypto";
import { logger } from "../utils/logger.js";

export function requestContext(req, res, next) {
  const incomingRequestId = req.headers["x-request-id"];
  const requestId = incomingRequestId || randomUUID();
  // Never trust caller-supplied user identifiers; rely on authenticated context only
  delete req.headers["x-user-id"];
  const userId = req.user?.user_id ?? req.user?.id ?? req.user?.sub ?? null;

  req.requestId = requestId;
  if (userId) {
    req.userId = userId;
  }
  req.log = logger.child({ requestId, userId });
  res.setHeader("x-request-id", requestId);
  next();
}
