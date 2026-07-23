/**
 * DevHunt Integration Gateway
 * Node.js сервис для OAuth, Webhooks and integrations с внешними API
 *
 * Архитектура: Core API -> Integration Gateway -> External APIs (GitHub, GitLab, etc.)
 */

import "./telemetry.js";
import express from "express";
import { axiosClient } from "./utils/axiosClient.js";
import cors from "cors";
import dotenv from "dotenv";
import { oauthRouter } from "./routes/oauth.js";
import { webhookRouter } from "./routes/webhooks.js";
import { logger } from "./utils/logger.js";
import { authenticateToken } from "./middleware/auth.js";
import { rateLimit } from "./middleware/rateLimit.js";
import { throttle, getThrottleMetrics } from "./middleware/throttle.js";
import { validateUrl } from "./utils/urlValidation.js";
import { startEventConsumer } from "./services/eventConsumer.js";
import { metricsMiddleware, metricsHandler } from "./metrics.js";
import { requireMetricsAuth } from "./middleware/metricsAuth.js";
import { requestContext } from "./middleware/requestContext.js";
import { traceContext } from "./middleware/traceContext.js";
import { assertInternalApiKeyConfigured } from "./utils/internalApiAuth.js";

dotenv.config();

const app = express();
const PORT = process.env.PORT || 5002;

// SECURITY FIX (SEC-013, R2): Validate webhook secrets in production
const ENVIRONMENT = process.env.ENVIRONMENT || "development";
if (ENVIRONMENT === "production") {
  const githubSecret = process.env.GITHUB_WEBHOOK_SECRET;
  const gitlabSecret = process.env.GITLAB_WEBHOOK_SECRET;
  const jwtSecret = process.env.JWT_SECRET;

  if (!githubSecret || githubSecret.length < 32) {
    throw new Error(
      "GITHUB_WEBHOOK_SECRET must be set to a strong value (32+ characters) in production. " +
        "This is required for webhook signature verification.",
    );
  }

  if (!gitlabSecret || gitlabSecret.length < 32) {
    throw new Error(
      "GITLAB_WEBHOOK_SECRET must be set to a strong value (32+ characters) in production. " +
        "This is required for webhook signature verification.",
    );
  }

  if (!jwtSecret || jwtSecret.length < 32) {
    throw new Error(
      "JWT_SECRET must be set to a strong value (32+ characters) in production. " +
        "Authentication cannot be disabled in production.",
    );
  }

  assertInternalApiKeyConfigured();

  logger.info("Production mode: All required secrets validated");
} else {
  logger.info(`Running in ${ENVIRONMENT} mode`);
}

// CORS configuration - ограничить origins в production
const defaultDevOrigins = ["http://localhost:3000"];
const corsFromEnv = process.env.CORS_ALLOWED_ORIGINS?.split(",")
  .map((origin) => origin.trim())
  .filter(Boolean);
const allowedOrigins =
  corsFromEnv && corsFromEnv.length > 0 ? corsFromEnv : defaultDevOrigins;

if (ENVIRONMENT === "production") {
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

// Body size limit (10MB для webhooks)
// verify saves raw bytes so webhook routes can verify HMAC signatures over the original body
app.use(express.json({
  limit: "10mb",
  verify: (req, _res, buf) => { req.rawBody = buf; },
}));
app.use(express.urlencoded({ extended: true, limit: "10mb" }));

// Request context (requestId/userId + logger child)
app.use(requestContext);
// Traceparent propagation for outgoing calls
app.use(traceContext);

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

// Routes
app.use("/api/oauth", authenticateToken, oauthRouter);
app.use("/api/webhooks", webhookRouter); // Webhooks могут быть без auth (проверка подписи)

// Sync endpoint (совместимость с Core API) - требует авторизацию
app.post("/api/sync", authenticateToken, async (req, res) => {
  try {
    const {
      IntegrationId,
      integrationId,
      ServiceType,
      serviceType,
      Config,
      config,
      AccessToken,
      accessToken,
      ProjectId,
      projectId,
    } = req.body;

    const finalIntegrationId = IntegrationId || integrationId;
    const finalServiceType = ServiceType || serviceType;
    const finalConfig = Config || config || {};
    const finalAccessToken = AccessToken || accessToken;
    const finalProjectId = ProjectId || projectId;

    if (!finalIntegrationId) {
      return res.status(400).json({ error: "IntegrationId is required" });
    }
    if (!finalServiceType) {
      return res.status(400).json({ error: "ServiceType is required" });
    }
    if (!finalAccessToken) {
      return res.status(400).json({ error: "AccessToken is required" });
    }

    logger.info(
      `Sync requested for integration ${finalIntegrationId}, service ${finalServiceType}`,
    );

    // Получить конфигурацию интеграции из Core API (если не передана)
    let integrationConfig = finalConfig;
    if (!integrationConfig || Object.keys(integrationConfig).length === 0) {
      try {
        const coreApiUrl = process.env.CORE_API_URL || "http://core-api:8080";
        const response = await axiosClient.get(
          `${coreApiUrl}/api/integrations/${finalIntegrationId}`,
          {
            headers: {
              Authorization: req.headers.authorization, // Передаём токен пользователя
            },
            traceparent: req.traceparent,
          },
        );
        integrationConfig = response.data.config || {};
      } catch (error) {
        logger.warn(
          `Failed to fetch integration config from Core API: ${error.message}`,
        );
      }
    }

    // Синхронизировать данные
    const { syncIntegrationData } = await import("./services/syncService.js");
    const syncResult = await syncIntegrationData(
      finalIntegrationId,
      finalServiceType,
      integrationConfig,
      finalAccessToken,
    );

    // Trigger code analysis after successful sync (fire-and-forget)
    const repository = integrationConfig?.repository || integrationConfig?.Repository;
    if (repository && finalServiceType?.toLowerCase() === "github") {
      const { triggerCodeAnalysis } = await import("./services/webhookHandler.js");
      const defaultBranch = integrationConfig?.defaultBranch || integrationConfig?.DefaultBranch || "main";
      triggerCodeAnalysis(
        { id: finalIntegrationId, projectId: finalProjectId },
        repository,
        defaultBranch,
        null,
        finalAccessToken,
      ).catch((err) => {
        logger.error(`Post-sync code analysis failed for ${repository}:`, err.message);
      });
    }

    res.json({
      success: true,
      integrationId: finalIntegrationId,
      serviceType: finalServiceType,
      syncedAt: syncResult.syncedAt,
      summary: syncResult.summary,
      data: syncResult.data, // Для отладки, можно убрать в production
    });
    return;
  } catch (error) {
    logger.error("Error in sync:", error);
    return res.status(500).json({
      success: false,
      error: error.message || "Internal server error",
    });
  }
});

// Webhook creation (совместимость с Core API) - требует авторизацию
app.post("/api/webhooks", authenticateToken, async (req, res) => {
  try {
    const {
      IntegrationId,
      integrationId,
      ServiceType,
      serviceType,
      WebhookUrl,
      webhookUrl,
      Events,
      events,
      AccessToken,
      accessToken,
      Config,
      config,
    } = req.body;

    const finalIntegrationId = IntegrationId || integrationId;
    const finalServiceType = ServiceType || serviceType;
    const finalWebhookUrl = WebhookUrl || webhookUrl;
    const finalEvents = Events || events || ["push", "pull_request", "issues"];
    const finalAccessToken = AccessToken || accessToken;
    const finalConfig = Config || config || {};

    if (!finalIntegrationId) {
      return res.status(400).json({ error: "IntegrationId is required" });
    }
    if (!finalServiceType) {
      return res.status(400).json({ error: "ServiceType is required" });
    }
    if (!finalWebhookUrl) {
      return res.status(400).json({ error: "WebhookUrl is required" });
    }
    // SEC-014: Validate webhook URL to prevent SSRF attacks
    const urlCheck = validateUrl(finalWebhookUrl);
    if (!urlCheck.valid) {
      return res.status(400).json({ error: `Invalid WebhookUrl: ${urlCheck.reason}` });
    }
    if (!finalAccessToken) {
      return res.status(400).json({ error: "AccessToken is required" });
    }

    logger.info(
      `Webhook creation requested for integration ${finalIntegrationId}, service ${finalServiceType}`,
    );

    // Получить конфигурацию интеграции из Core API (если не передана)
    let integrationConfig = finalConfig;
    if (!integrationConfig || Object.keys(integrationConfig).length === 0) {
      try {
        const coreApiUrl = process.env.CORE_API_URL || "http://core-api:8080";
        const response = await axiosClient.get(
          `${coreApiUrl}/api/integrations/${finalIntegrationId}`,
          {
            headers: {
              Authorization: req.headers.authorization,
            },
          },
        );
        integrationConfig = response.data.config || {};
      } catch (error) {
        logger.warn(
          `Failed to fetch integration config from Core API: ${error.message}`,
        );
      }
    }

    // Создать webhook через внешний API
    const { createGitHubWebhook, createGitLabWebhook } = await import(
      "./services/webhookService.js"
    );
    let webhookResult = null;

    const serviceTypeLower = finalServiceType.toLowerCase();
    if (serviceTypeLower === "github") {
      const repository = integrationConfig.repository;
      if (!repository) {
        return res.status(400).json({
          error:
            'Repository not configured in integration. Set config.repository to "owner/repo"',
        });
      }
      webhookResult = await createGitHubWebhook(
        repository,
        finalWebhookUrl,
        finalEvents,
        finalAccessToken,
      );
    } else if (serviceTypeLower === "gitlab") {
      const projectId = integrationConfig.projectId;
      const gitlabUrl =
        integrationConfig.gitlabUrl || "https://gitlab.com/api/v4";
      if (!projectId) {
        return res.status(400).json({
          error: "GitLab projectId not configured in integration config",
        });
      }
      webhookResult = await createGitLabWebhook(
        projectId,
        finalWebhookUrl,
        finalEvents,
        finalAccessToken,
        gitlabUrl,
      );
    } else {
      return res.status(400).json({
        error: `Unsupported service type: ${finalServiceType}. Supported: github, gitlab`,
      });
    }

    // Обновить конфигурацию интеграции в Core API с webhook secret
    try {
      const coreApiUrl = process.env.CORE_API_URL || "http://core-api:8080";
      await axiosClient.put(
        `${coreApiUrl}/api/integrations/${finalIntegrationId}/webhook`,
        {
          webhookId: webhookResult.webhookId,
          webhookSecret: webhookResult.secret,
          webhookUrl: webhookResult.webhookUrl,
        },
        {
          headers: {
            Authorization: req.headers.authorization,
          },
          traceparent: req.traceparent,
        },
      );
    } catch (error) {
      logger.warn(
        `Failed to update webhook config in Core API: ${error.message}`,
      );
      // Не критично, продолжаем
    }

    res.json({
      success: true,
      webhookId: webhookResult.webhookId,
      webhookUrl: webhookResult.webhookUrl,
      integrationId: finalIntegrationId,
      serviceType: finalServiceType,
      events: webhookResult.events,
      createdAt: webhookResult.createdAt,
      // Важно: сохранить secret в Integration.Config
      secret: webhookResult.secret,
    });
    return;
  } catch (error) {
    logger.error("Error creating webhook:", error);
    return res.status(500).json({
      success: false,
      error: error.message || "Internal server error",
    });
  }
});

// Webhook deletion (совместимость с Core API) - требует авторизацию
app.delete(
  "/api/webhooks/:integrationId/:serviceType/:webhookId",
  authenticateToken,
  async (req, res) => {
    try {
      const { integrationId, serviceType, webhookId } = req.params;
      const { AccessToken, accessToken, Config, config } = req.body;

      const finalAccessToken = AccessToken || accessToken;
      const finalConfig = Config || config || {};

      if (!finalAccessToken) {
        return res
          .status(400)
          .json({ error: "AccessToken is required in request body" });
      }

      logger.info(
        `Webhook deletion requested: ${webhookId} for ${serviceType}`,
      );

      // Получить конфигурацию интеграции из Core API (если не передана)
      let integrationConfig = finalConfig;
      if (!integrationConfig || Object.keys(integrationConfig).length === 0) {
        try {
          const coreApiUrl = process.env.CORE_API_URL || "http://core-api:8080";
        const response = await axiosClient.get(
          `${coreApiUrl}/api/integrations/${integrationId}`,
          {
            headers: {
              Authorization: req.headers.authorization,
            },
            traceparent: req.traceparent,
          },
        );
          integrationConfig = response.data.config || {};
        } catch (error) {
          logger.warn(
            `Failed to fetch integration config from Core API: ${error.message}`,
          );
        }
      }

      // Удалить webhook через внешний API
      const { deleteGitHubWebhook, deleteGitLabWebhook } = await import(
        "./services/webhookService.js"
      );
      const serviceTypeLower = serviceType.toLowerCase();

      if (serviceTypeLower === "github") {
        const repository = integrationConfig.repository;
        if (!repository) {
          return res
            .status(400)
            .json({ error: "Repository not configured in integration config" });
        }
        await deleteGitHubWebhook(repository, webhookId, finalAccessToken);
      } else if (serviceTypeLower === "gitlab") {
        const projectId = integrationConfig.projectId;
        const gitlabUrl =
          integrationConfig.gitlabUrl || "https://gitlab.com/api/v4";
        if (!projectId) {
          return res.status(400).json({
            error: "GitLab projectId not configured in integration config",
          });
        }
        await deleteGitLabWebhook(
          projectId,
          webhookId,
          finalAccessToken,
          gitlabUrl,
        );
      } else {
        return res.status(400).json({
          error: `Unsupported service type: ${serviceType}. Supported: github, gitlab`,
        });
      }

      // Удалить webhook конфигурацию из Core API
      try {
        const coreApiUrl = process.env.CORE_API_URL || "http://core-api:8080";
        await axiosClient.delete(
          `${coreApiUrl}/api/integrations/${integrationId}/webhook/${webhookId}`,
          {
            headers: {
              Authorization: req.headers.authorization,
            },
          },
        );
      } catch (error) {
        logger.warn(
          `Failed to delete webhook config in Core API: ${error.message}`,
        );
        // Не критично, продолжаем
      }

      res.json({
        success: true,
        webhookId,
        deletedAt: new Date().toISOString(),
      });
      return;
    } catch (error) {
      logger.error("Error deleting webhook:", error);
      return res.status(500).json({
        success: false,
        error: error.message || "Internal server error",
      });
    }
  },
);

// Health check
app.get("/health", (req, res) => {
  res.json({
    status: "healthy",
    service: "integration-gateway",
    version: "1.0.0",
    timestamp: new Date().toISOString(),
    supportedProviders: ["github", "gitlab"],
  });
  return
});

// Prometheus metrics
app.get("/metrics", requireMetricsAuth, metricsHandler);

// Throttle diagnostics endpoint
app.get("/metrics/throttle", requireMetricsAuth, (req, res) => {
  res.json(getThrottleMetrics());
  return
});

// Root endpoint
app.get("/", (req, res) => {
  res.json({
    service: "DevHunt Integration Gateway",
    version: "1.0.0",
    status: "running",
    endpoints: {
      health: "/health",
      oauth: {
        getUrl: "/api/oauth/:provider/url",
        callback: "/api/oauth/:provider/callback",
      },
      webhooks: {
        verify: "/api/webhooks/verify-signature",
        github: "/api/webhooks/github",
        gitlab: "/api/webhooks/gitlab",
      },
    },
  });
  return
});

// Start server
const server = app.listen(PORT, () => {
  logger.info(`🚀 Integration Gateway running on port ${PORT}`);
  logger.info("🔗 Supported providers: GitHub, GitLab");

  // Start Event Bus consumer (if enabled)
  if (process.env.RABBITMQ_ENABLED !== "false") {
    startEventConsumer().catch((err) => {
      logger.error("Failed to start event consumer:", err);
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
