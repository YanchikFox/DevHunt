/**
 * Winston logger configuration
 */

import winston from "winston";
import axios from "axios";
import { AxiosHeaders } from "axios";

class OpenObserveTransport extends winston.Transport {
  constructor(opts = {}) {
    super(opts);
    this.name = "openobserve";
    this.url =
      opts.url ||
      process.env.OPENOBSERVE_LOG_URL ||
      "http://openobserve:5080/api/default/logs/_json";
    const user =
      opts.user || process.env.OPENOBSERVE_ROOT_USER || "admin@devhunt.local";
    const pass =
      opts.password || process.env.OPENOBSERVE_ROOT_PASSWORD || "ChangeMe123!";
    const token = Buffer.from(`${user}:${pass}`).toString("base64");
    this.authHeader = `Basic ${token}`;
    this.enabled = opts.enabled ?? process.env.OPENOBSERVE_LOG_ENABLED === "true";
  }

  log(info, callback) {
    setImmediate(callback);
    if (!this.enabled) return;

    axios
      .post(
        this.url,
        [
          {
            level: info.level,
            message: info.message,
            timestamp: info.timestamp || new Date().toISOString(),
            service: "integration-gateway",
            ...info,
          },
        ],
        {
          headers: {
            Authorization: this.authHeader,
            "Content-Type": "application/json",
            "traceparent": info.traceparent,
          },
          timeout: 2000,
        }
      )
      .catch(() => {});
  }
}

const LOG_LEVEL = process.env.LOG_LEVEL || "info";

export const logger = winston.createLogger({
  level: LOG_LEVEL,
  format: winston.format.combine(
    winston.format.timestamp(),
    winston.format.errors({ stack: true }),
    winston.format.splat(),
    winston.format.json(),
  ),
  defaultMeta: { service: "integration-gateway" },
  transports: [
    new winston.transports.Console({
      format: winston.format.combine(
        winston.format.timestamp(),
        winston.format.errors({ stack: true }),
        winston.format.splat(),
        winston.format.json()
      ),
    }),
    new OpenObserveTransport(),
  ],
});
