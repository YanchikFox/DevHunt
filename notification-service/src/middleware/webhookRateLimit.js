import Redis from "ioredis";
import { RateLimiterRedis, RateLimiterMemory } from "rate-limiter-flexible";
import { logger } from "../utils/logger.js";

const WEBHOOK_RATE_LIMIT_WINDOW_MS = Number(
  process.env.ALERT_WEBHOOK_RATE_LIMIT_WINDOW_MS || 60_000,
);
const WEBHOOK_RATE_LIMIT_DURATION_SECONDS = Math.max(
  1,
  Math.ceil(WEBHOOK_RATE_LIMIT_WINDOW_MS / 1000),
);
const WEBHOOK_RATE_LIMIT_MAX_REQUESTS = Number(
  process.env.ALERT_WEBHOOK_RATE_LIMIT_MAX_REQUESTS || 30,
);
const RATE_LIMIT_REDIS_URL =
  process.env.RATE_LIMIT_REDIS_URL ||
  process.env.REDIS_URL ||
  "redis://cache-service:6379";

let webhookRateLimiter = createWebhookRateLimiter();

function createWebhookRateLimiter() {
  const memoryLimiter = new RateLimiterMemory({
    keyPrefix: "notification_webhook_rl_mem",
    points: WEBHOOK_RATE_LIMIT_MAX_REQUESTS,
    duration: WEBHOOK_RATE_LIMIT_DURATION_SECONDS,
  });

  if (!RATE_LIMIT_REDIS_URL) {
    return memoryLimiter;
  }

  try {
    const redisClient = new Redis(RATE_LIMIT_REDIS_URL, {
      enableOfflineQueue: false,
      maxRetriesPerRequest: 1,
    });

    redisClient.on("error", (err) => {
      logger.warn(`Webhook rate limiter Redis error: ${err.message}`);
    });

    return new RateLimiterRedis({
      storeClient: redisClient,
      keyPrefix: "notification_webhook_rl",
      points: WEBHOOK_RATE_LIMIT_MAX_REQUESTS,
      duration: WEBHOOK_RATE_LIMIT_DURATION_SECONDS,
      insuranceLimiter: memoryLimiter,
    });
  } catch (error) {
    logger.warn(
      `Webhook rate limiter Redis init failed, using memory: ${error.message}`,
    );
    return memoryLimiter;
  }
}

/**
 * Per-IP rate limit for unauthenticated alert webhooks (DEV-20).
 */
export async function webhookRateLimit(req, res, next) {
  const ip =
    req.ip ||
    req.headers["x-forwarded-for"]?.toString().split(",")[0]?.trim() ||
    req.connection.remoteAddress ||
    "unknown";

  try {
    await webhookRateLimiter.consume(`webhook:${ip}`);
    return next();
  } catch (error) {
    if (error instanceof Error) {
      logger.error("Webhook rate limiter failed", error);
      return res
        .status(503)
        .json({ error: "Rate limiting unavailable, request rejected" });
    }

    const retryAfter = Math.ceil(error.msBeforeNext / 1000);
    res.setHeader("Retry-After", retryAfter.toString());
    return res.status(429).json({
      error: "Too many webhook requests",
      retryAfter,
    });
  }
}
