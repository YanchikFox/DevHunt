import Redis from "ioredis";
import { RateLimiterRedis, RateLimiterMemory } from "rate-limiter-flexible";
import { logger } from "../utils/logger.js";

const RATE_LIMIT_WINDOW_MS = Number(process.env.RATE_LIMIT_WINDOW_MS || 60_000); // default 1 minute
const RATE_LIMIT_DURATION_SECONDS = Math.max(
  1,
  Math.ceil(RATE_LIMIT_WINDOW_MS / 1000),
);
const RATE_LIMIT_MAX_REQUESTS = Number(
  process.env.RATE_LIMIT_MAX_REQUESTS || 100,
);
const RATE_LIMIT_REDIS_URL =
  process.env.RATE_LIMIT_REDIS_URL ||
  process.env.REDIS_URL ||
  "redis://cache-service:6379";

let rateLimiter = createRateLimiter();

function createRateLimiter() {
  const memoryLimiter = new RateLimiterMemory({
    keyPrefix: "integration_gateway_rl_mem",
    points: RATE_LIMIT_MAX_REQUESTS,
    duration: RATE_LIMIT_DURATION_SECONDS,
  });

  if (!RATE_LIMIT_REDIS_URL) {
    logger.warn(
      "RATE_LIMIT_REDIS_URL not set; using in-memory rate limiter (not distributed).",
    );
    return memoryLimiter;
  }

  try {
    const redisClient = new Redis(RATE_LIMIT_REDIS_URL, {
      enableOfflineQueue: false,
      maxRetriesPerRequest: 1,
    });

    redisClient.on("error", (err) => {
      logger.warn(`Redis rate limiter error: ${err.message}`);
    });

    return new RateLimiterRedis({
      storeClient: redisClient,
      keyPrefix: "integration_gateway_rl",
      points: RATE_LIMIT_MAX_REQUESTS,
      duration: RATE_LIMIT_DURATION_SECONDS,
      insuranceLimiter: memoryLimiter, // fallback if Redis is unavailable
      execEvenly: true,
      blockDuration: 0,
    });
  } catch (error) {
    logger.warn(
      `Failed to initialize Redis rate limiter, falling back to memory: ${error.message}`,
    );
    return memoryLimiter;
  }
}

export async function rateLimit(req, res, next) {
  const ip =
    req.ip ||
    req.headers["x-forwarded-for"]?.toString().split(",")[0]?.trim() ||
    req.connection.remoteAddress ||
    "unknown";

  try {
    await rateLimiter.consume(ip);
    res.setHeader("X-RateLimit-Limit", RATE_LIMIT_MAX_REQUESTS.toString());
    return next();
  } catch (error) {
    if (error instanceof Error) {
      logger.error("Rate limiter failed", error);
      return res
        .status(503)
        .json({ error: "Rate limiting unavailable, request rejected" });
    }

    const retryAfter = Math.ceil(error.msBeforeNext / 1000);
    res.setHeader("Retry-After", retryAfter.toString());
    res.setHeader("X-RateLimit-Remaining", "0");

    return res.status(429).json({
      error: "Too many requests",
      retryAfter,
    });
  }
}
