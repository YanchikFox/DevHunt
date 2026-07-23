/**
 * DevHunt Notification Service
 * Node.js сервис для отправки уведомлений (Email, SMS, Push)
 *
 * Архитектура: Core API -> Notification Service -> SendGrid/SMTP
 */

import "./telemetry.js";
import express from "express";
import cors from "cors";
import dotenv from "dotenv";
import { sendEmail } from "./services/emailService.js";
import { sendSMS } from "./services/smsService.js";
import { sendPush } from "./services/pushService.js";
import { rabbitMQConsumer } from "./services/rabbitmqConsumer.js";
import { logger } from "./utils/logger.js";
import { authenticateToken } from "./middleware/auth.js";
import { rateLimit } from "./middleware/rateLimit.js";
import { webhookRateLimit } from "./middleware/webhookRateLimit.js";
import { notImplemented } from "./middleware/notImplemented.js";
import { throttle, getThrottleMetrics } from "./middleware/throttle.js";
import {
  metricsMiddleware,
  metricsHandler,
  recordNotificationAttempt,
  observeNotificationLatency,
} from "./metrics.js";
import { requireMetricsAuth } from "./middleware/metricsAuth.js";
import { requestContext } from "./middleware/requestContext.js";

dotenv.config();

const app = express();
const PORT = process.env.PORT || 5003;

// CORS configuration - ограничить origins в production
const defaultDevOrigins = ["http://localhost:3000"];
const corsFromEnv = process.env.CORS_ALLOWED_ORIGINS?.split(",")
  .map((origin) => origin.trim())
  .filter(Boolean);
const allowedOrigins =
  corsFromEnv && corsFromEnv.length > 0 ? corsFromEnv : defaultDevOrigins;

if (process.env.ENVIRONMENT === "production") {
  if (!corsFromEnv || corsFromEnv.length === 0) {
    throw new Error("CORS_ALLOWED_ORIGINS must be defined in production.");
  }
  if (corsFromEnv.includes("*")) {
    throw new Error(
      'CORS_ALLOWED_ORIGINS must not contain wildcard "*" in production.',
    );
  }
}

// Middleware
app.use(
  cors({
    origin: allowedOrigins,
    credentials: true,
    methods: ["GET", "POST", "PUT", "DELETE", "OPTIONS"],
    allowedHeaders: ["Content-Type", "Authorization", "X-Requested-With"],
  }),
);

// Body size limit (5MB для уведомлений)
app.use(express.json({ limit: "5mb" }));
app.use(express.urlencoded({ extended: true, limit: "5mb" }));

// Request context (requestId/userId + logger child)
app.use(requestContext);

// Metrics instrumentation
app.use(metricsMiddleware);

// Rate limiting
app.use(rateLimit);

// Throttling and backpressure (после rate limiting)
app.use(throttle);

// Request logging
app.use((req, res, next) => {
  const log = req.log || logger;
  log.info(`${req.method} ${req.path} from ${req.ip}`);
  next();
});

// Health check
app.get("/health", (req, res) => {
  return res.json({
    status: "healthy",
    service: "notification-service",
    version: "1.0.0",
    timestamp: new Date().toISOString(),
  });
});

// Metrics endpoints\r\n// Prometheus metrics
app.get("/metrics", requireMetricsAuth, metricsHandler);

// Throttle diagnostics
app.get("/metrics/throttle", requireMetricsAuth, (req, res) => {
  res.json(getThrottleMetrics());
});

// Send notification endpoint (совместимость с Core API) - требует авторизацию
app.post("/api/notifications", authenticateToken, async (req, res) => {
  try {
    const start = performance.now();
    // Поддержка формата от Core API
    const {
      UserId,
      userId,
      Type,
      type,
      Title,
      title,
      Content,
      content,
      recipient,
      subject,
      template,
      data,
    } = req.body;

    const finalUserId = UserId || userId;
    const finalType = Type || type || "info";
    const finalTitle = Title || title || "DevHunt Notification";
    const finalContent = Content || content || "";
    const config = req.body.config || {};

    if (!finalUserId || !finalType) {
      return res.status(400).json({
        error: "Missing required fields: UserId (or userId), Type (or type)",
      });
    }

    const normalizedType = finalType.toLowerCase();

    let result = null;

    switch (normalizedType) {
      case "email": {
        const emailTo = recipient || config?.email;
        if (!emailTo) {
          return res.status(400).json({
            error:
              "Email address required. Provide recipient or email in config",
          });
        }
        logger.info(
          `Sending ${normalizedType} notification to user ${finalUserId}`,
        );
        result = await sendEmail({
          to: emailTo,
          subject: subject || finalTitle,
          html: finalContent,
          template,
          data,
        });
        break;
      }

      case "sms":
        logger.info(
          `Sending ${normalizedType} notification to user ${finalUserId}`,
        );
        // Получить номер телефона из БД или из запроса
        const phoneNumber = recipient || config?.phoneNumber;
        if (!phoneNumber) {
          return res.status(400).json({
            error:
              "Phone number required for SMS. Provide recipient or phoneNumber in config",
          });
        }
        result = await sendSMS({
          to: phoneNumber,
          message: finalContent,
        });
        break;

      case "push":
        logger.info(
          `Sending ${normalizedType} notification to user ${finalUserId}`,
        );
        // Получить device token из БД или из запроса
        const deviceToken = recipient || config?.deviceToken;
        if (!deviceToken) {
          return res.status(400).json({
            error:
              "Device token required for push. Provide recipient or deviceToken in config",
          });
        }
        result = await sendPush({
          to: deviceToken,
          title: finalTitle,
          body: finalContent,
          data: data || {},
        });
        break;

      default:
        return res.status(400).json({
          error: `Unsupported notification type: ${type}`,
        });
    }

    const statusLabel = result.success ? "success" : "failure";
    recordNotificationAttempt(normalizedType, statusLabel);
    observeNotificationLatency(normalizedType, statusLabel, (performance.now() - start) / 1000);

    if (result.success) {
      return res.json({
        success: true,
        notificationId: result.id || Date.now().toString(),
        type,
        sentAt: new Date().toISOString(),
      });
    }

    return res.status(500).json({
      success: false,
      error: result.message || "Failed to send notification",
    });
  } catch (error) {
    const failedChannel = (req.body?.Type || req.body?.type || "unknown").toString().toLowerCase();
    recordNotificationAttempt(failedChannel, "failure");
    logger.error("Error sending notification:", error);
    return res.status(500).json({
      success: false,
      error: error.message || "Internal server error",
    });
  }
});

// Bulk + read-state endpoints are owned by Core API (PostgreSQL). Do not fake delivery here.
app.post(
  "/api/notifications/bulk",
  authenticateToken,
  notImplemented("Bulk notifications"),
);
app.put(
  "/api/notifications/:id/read",
  authenticateToken,
  notImplemented("Mark notification as read"),
);
app.put(
  "/api/notifications/user/:userId/read-all",
  authenticateToken,
  notImplemented("Mark all notifications as read"),
);

// OpenObserve alerts webhook — per-IP rate limit + Bearer secret
app.post("/api/alerts/webhook", webhookRateLimit, (req, res, next) => {
  const secret = process.env.ALERT_WEBHOOK_SECRET;
  if (!secret) {
    logger.warn("ALERT_WEBHOOK_SECRET not configured, rejecting alert webhook");
    return res.status(503).json({ error: "Alert webhook not configured" });
  }
  const provided = req.headers["authorization"];
  if (!provided || provided !== `Bearer ${secret}`) {
    logger.warn("Alert webhook: unauthorized request rejected");
    return res.status(401).json({ error: "Unauthorized" });
  }
  next();
}, async (req, res) => {
  try {
    const alert = req.body;

    logger.warn("OpenObserve Alert received", {
      alertName: alert.alert_name || alert.name || "unknown",
      severity: alert.severity || "warning",
      stream: alert.stream_name || alert.stream || "unknown",
      message: alert.alert_message || alert.message || "",
      timestamp: alert.alert_start_time || new Date().toISOString(),
      payload: alert,
    });

    // Send email notification for critical alerts
    const severity = (alert.severity || "warning").toLowerCase();
    if (severity === "critical" || severity === "high") {
      const adminEmail = process.env.ALERT_ADMIN_EMAIL || process.env.SMTP_FROM;
      if (adminEmail) {
        // P1-3: Escape webhook data to prevent XSS in email HTML
        const escapeHtml = (str) =>
          String(str ?? "")
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#x27;");

        await sendEmail({
          to: adminEmail,
          subject: `[${severity.toUpperCase()}] DevHunt Alert: ${(alert.alert_name || alert.name || "System Alert").replace(/[<>]/g, "")}`,
          html: `
            <h2>DevHunt Monitoring Alert</h2>
            <p><strong>Alert:</strong> ${escapeHtml(alert.alert_name || alert.name || "Unknown")}</p>
            <p><strong>Severity:</strong> ${escapeHtml(severity)}</p>
            <p><strong>Stream:</strong> ${escapeHtml(alert.stream_name || alert.stream || "N/A")}</p>
            <p><strong>Message:</strong> ${escapeHtml(alert.alert_message || alert.message || "No message")}</p>
            <p><strong>Time:</strong> ${escapeHtml(alert.alert_start_time || new Date().toISOString())}</p>
            <hr>
            <pre>${escapeHtml(JSON.stringify(alert, null, 2))}</pre>
          `,
        });
        logger.info(`Alert email sent to ${adminEmail}`);
      }
    }

    return res.json({
      success: true,
      received: true,
      timestamp: new Date().toISOString(),
    });
  } catch (error) {
    logger.error("Error processing alert webhook:", error);
    return res.status(500).json({
      success: false,
      error: error.message || "Failed to process alert",
    });
  }
});

// Root endpoint
app.get("/", (req, res) => {
  return res.json({
    service: "DevHunt Notification Service",
    version: "1.0.0",
    status: "running",
    endpoints: {
      health: "/health",
      send: "/api/notifications/send",
      batch: "/api/notifications/batch",
      docs: "https://github.com/devhunt/notification-service",
    },
  });
});

// Start server
const server = app.listen(PORT, () => {
  logger.info(`🚀 Notification Service running on port ${PORT}`);
  logger.info(`📧 Email provider: ${process.env.EMAIL_PROVIDER || "SMTP"}`);

  // Start RabbitMQ consumer for async notifications
  if (process.env.RABBITMQ_ENABLED !== "false") {
    rabbitMQConsumer().catch((err) => {
      logger.error("Failed to start RabbitMQ consumer:", err);
    });
  }
});

// Graceful shutdown
process.on("SIGTERM", () => {
  logger.info("SIGTERM received, shutting down gracefully...");
  server.close(() => {
    logger.info("Server closed");
    process.exitCode = 0;
  });
});
