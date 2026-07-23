import { createThrottle } from "../../../packages/throttle/index.js";
import { logger } from "../utils/logger.js";

const throttleInstance = createThrottle({
  logger,
  maxConcurrent: parseInt(process.env.MAX_CONCURRENT_REQUESTS || "50", 10),
  maxQueueSize: parseInt(process.env.MAX_QUEUE_SIZE || "100", 10),
  cpuThreshold: parseFloat(process.env.CPU_THRESHOLD || "0.8"),
  memoryThreshold: parseFloat(process.env.MEMORY_THRESHOLD || "0.9"),
  metricsRefreshMs: parseInt(
    process.env.SYSTEM_METRICS_REFRESH_MS || "2000",
    10,
  ),
});

export const throttle = throttleInstance.throttle;
export const getThrottleMetrics = throttleInstance.getMetrics;
export const shutdownThrottle = throttleInstance.shutdown;
