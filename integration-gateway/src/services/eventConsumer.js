/**
 * Event Bus Consumer для Integration Gateway
 * Subscribes to events from RabbitMQ для синхронизации с внешними системами
 */

import amqp from "amqplib";
import axios from "axios";
import { logger } from "../utils/logger.js";
import {
  buildInternalApiHeadersForIntegrationToken,
  buildInternalApiHeadersForUrl,
} from "../utils/internalApiAuth.js";
import { syncIntegrationData } from "./syncService.js";
import {
  createGitHubIssue,
  closeGitHubIssue,
} from "./githubIssueService.js";
import { recordEventResult, setRabbitConnectionState } from "../metrics.js";

const RABBITMQ_URL =
  process.env.RABBITMQ_URL ||
  process.env.RABBITMQ__CONNECTIONSTRING ||
  "amqp://localhost:5672";
const EXCHANGE_NAME = process.env.RABBITMQ_EXCHANGE_NAME || "devhunt.events";
const INTEGRATION_QUEUE =
  process.env.INTEGRATION_QUEUE || "devhunt.integrations";
const MAX_RETRY_COUNT = parseInt(process.env.RABBITMQ_MAX_RETRIES || "3", 10);
const WORKER_CONCURRENCY = parseInt(
  process.env.INTEGRATION_WORKER_CONCURRENCY || "5",
  10,
);
const CORE_API_URL = process.env.CORE_API_URL || "http://core-api:5000";

/**
 * Start RabbitMQ consumer for integration synchronization
 */
export async function startEventConsumer() {
  try {
    logger.info(`Connecting to RabbitMQ at ${RABBITMQ_URL}`);
    const connection = await amqp.connect(RABBITMQ_URL);
    setRabbitConnectionState(true);
    const channel = await connection.createChannel();

    // Declare exchange
    await channel.assertExchange(EXCHANGE_NAME, "topic", {
      durable: true,
      autoDelete: false,
    });

    // Declare queue for integrations
    await channel.assertQueue(INTEGRATION_QUEUE, {
      durable: true,
    });

    // Bind queue to exchange with routing keys for integration-worthy events
    const routingKeys = [
      "project.created", // Создание проекта - синхронизировать с GitHub/GitLab
      "project.updated", // Обновление проекта - обновить внешние системы
      "project.completed", // Завершение проекта - обновить статус
      "task.created", // Создание задачи - создать Issue в GitHub
      "task.updated", // Обновление задачи - обновить Issue в GitHub
      "task.completed", // Завершение задачи - закрыть Issue в GitHub
      "showcase.published", // Публикация showcase - уведомить внешние системы
    ];

    for (const routingKey of routingKeys) {
      await channel.bindQueue(INTEGRATION_QUEUE, EXCHANGE_NAME, routingKey);
      logger.info(`Bound queue to exchange with routing key: ${routingKey}`);
    }

    logger.info(
      `Listening for events on exchange: ${EXCHANGE_NAME}, queue: ${INTEGRATION_QUEUE}`,
    );

    await channel.prefetch(WORKER_CONCURRENCY);

    const pendingMessages = [];
    let processingCount = 0;

    const handleMessage = async (msg) => {
      const routingKey = msg.fields.routingKey || "unknown";
      try {
        const event = JSON.parse(msg.content.toString());

        logger.info(
          `Received event: ${event.EventType} for ${event.EntityType}:${event.EntityId} (routing: ${routingKey})`,
        );

        await processEvent(event, routingKey);

        channel.ack(msg);
        recordEventResult(routingKey, "processed");
        logger.info("Event processed successfully");
      } catch (error) {
        logger.error("Error processing event:", error);
        const headers = msg.properties.headers
          ? { ...msg.properties.headers }
          : {};
        const retryCount = headers["x-retry-count"] ?? 0;

        if (retryCount < MAX_RETRY_COUNT) {
          const newHeaders = {
            ...headers,
            "x-retry-count": retryCount + 1,
            "x-last-error": error?.message || "unknown error",
          };

          channel.publish(EXCHANGE_NAME, routingKey, msg.content, {
            headers: newHeaders,
            persistent: true,
            contentType: msg.properties.contentType,
            contentEncoding: msg.properties.contentEncoding,
          });
          channel.ack(msg);
          recordEventResult(routingKey, "retried");
          logger.warn(
            `Requeued message for ${routingKey} with retry ${retryCount + 1}/${MAX_RETRY_COUNT}`,
          );
        } else {
          recordEventResult(routingKey, "dropped");
          logger.error(
            `Event failed after ${retryCount} retries, dropping message`,
          );
          channel.ack(msg);
        }
      }
    };

    const scheduleNext = () => {
      if (processingCount >= WORKER_CONCURRENCY) {
        return;
      }

      const next = pendingMessages.shift();
      if (!next) {
        return;
      }

      processingCount += 1;
      handleMessage(next)
        .catch((err) => {
          logger.error(
            "Unhandled error while processing integration event:",
            err,
          );
        })
        .finally(() => {
          processingCount -= 1;
          if (pendingMessages.length > 0) {
            scheduleNext();
          }
        });
    };

    const enqueueMessage = (msg) => {
      pendingMessages.push(msg);
      scheduleNext();
    };

    // Consume messages
    channel.consume(
      INTEGRATION_QUEUE,
      (msg) => {
        if (msg) {
          enqueueMessage(msg);
        }
      },
      {
        noAck: false, // Manual acknowledgment
      },
    );

    // Handle connection errors
    connection.on("error", (err) => {
      logger.error("RabbitMQ connection error:", err);
      setRabbitConnectionState(false);
    });

    connection.on("close", () => {
      logger.warn("RabbitMQ connection closed, attempting to reconnect...");
      setRabbitConnectionState(false);
      // Попытка переподключения через 5 секунд
      setTimeout(() => {
        startEventConsumer().catch((err) => {
          logger.error("Reconnection failed:", err);
        });
      }, 5000);
    });

    logger.info("Integration event consumer started successfully");

    // Return connection for graceful shutdown
    return connection;
  } catch (error) {
    setRabbitConnectionState(false);
    logger.error("Failed to start event consumer:", error);
    // Попытка переподключения через 10 секунд
    setTimeout(() => {
      startEventConsumer().catch((err) => {
        logger.error("Reconnection failed:", err);
      });
    }, 10000);

    return null;
  }
}

/**
 * Event handlers map - each event type maps to its handler function
 */
const EVENT_HANDLERS = {
  "project.created": handleProjectCreated,
  "project.updated": handleProjectUpdated,
  "project.completed": handleProjectCompleted,
  "task.created": handleTaskCreated,
  "task.updated": handleTaskUpdated,
  "task.completed": handleTaskCompleted,
  "showcase.published": handleShowcasePublished,
};

/**
 * Обработать событие и синхронизировать с внешними системами
 */
async function processEvent(event, _routingKey) {
  const { EventType, EntityId, Data } = event;

  try {
    const handler = EVENT_HANDLERS[EventType];

    if (handler) {
      await handler(EntityId, Data);
    } else {
      logger.debug(`Event ${EventType} does not require integration sync`);
    }
  } catch (error) {
    logger.error(`Error processing event ${EventType}:`, error);
    throw error; // Пробрасываем для retry логики
  }
}

/**
 * Синхронизировать одну интеграцию проекта
 */
async function syncSingleIntegration(projectId, integration) {
  if (!integration.IsActive) return;

  logger.info(`Syncing project ${projectId} with ${integration.ServiceType}`);

  const accessToken = await getIntegrationAccessToken(integration.Id);
  if (!accessToken) {
    logger.warn(`No access token for integration ${integration.Id}`);
    return;
  }

  try {
    await syncIntegrationData(
      integration.Id,
      integration.ServiceType,
      integration.Config || {},
      accessToken,
    );
    logger.info(`Synced project ${projectId} with ${integration.ServiceType}`);
  } catch (error) {
    logger.error(
      `Failed to sync project ${projectId} with ${integration.ServiceType}:`,
      error,
    );
  }
}

/**
 * Обработать создание проекта - найти интеграции и синхронизировать
 */
async function handleProjectCreated(projectId, _data) {
  logger.info(
    `Project ${projectId} created, checking for integrations to sync`,
  );

  try {
    const integrations = await getProjectIntegrations(projectId);

    if (!integrations || integrations.length === 0) {
      logger.debug(`No integrations found for project ${projectId}`);
      return;
    }

    for (const integration of integrations) {
      await syncSingleIntegration(projectId, integration);
    }
  } catch (error) {
    logger.error(`Error handling project created: ${error.message}`);
  }
}

/**
 * Обработать обновление проекта - обновить внешние системы
 */
async function handleProjectUpdated(projectId, _data) {
  logger.info(`Project ${projectId} updated, syncing with external systems`);

  // Похожая логика на handleProjectCreated
  await handleProjectCreated(projectId, _data);
}

/**
 * Обработать завершение проекта - обновить статус во внешних системах
 */
async function handleProjectCompleted(projectId, _data) {
  logger.info(`Project ${projectId} completed, updating external systems`);

  // Получить интеграции и обновить статус проекта
  // Например, закрыть milestone в GitHub, обновить статус в Jira
  const integrations = await getProjectIntegrations(projectId);

  for (const integration of integrations) {
    if (!integration.IsActive) {
      continue;
    }

    // Обновить статус проекта во внешней системе
    // (требует реализации API для каждого провайдера)
    logger.info(`Updating project status in ${integration.ServiceType}`);
  }
}

/**
 * Create a task in GitHub
 */
async function createTaskInGitHub(taskId, data, integration) {
  const accessToken = await getIntegrationAccessToken(integration.Id);
  if (!accessToken) {
    logger.warn(`No access token for GitHub integration ${integration.Id}`);
    return;
  }

  try {
    const issue = await createGitHubIssue(data, integration, accessToken);
    if (issue) {
      logger.info(`Created GitHub Issue #${issue.number} for task ${taskId}`);
    }
  } catch (error) {
    logger.error(`Failed to create GitHub Issue for task ${taskId}:`, error.message);
  }
}

/**
 * Log placeholder for unsupported services
 */
function logUnsupportedService(integration) {
  logger.info(`Creating task in ${integration.ServiceType} (not implemented)`);
}

/**
 * Service handlers for task creation
 */
const TASK_CREATE_HANDLERS = {
  github: createTaskInGitHub,
  jira: (taskId, data, integration) => logUnsupportedService(integration),
  trello: (taskId, data, integration) => logUnsupportedService(integration),
};

/**
 * Check if task creation should be processed
 */
function shouldProcessTaskCreation(taskId, data) {
  if (!data?.ProjectId) {
    logger.debug("No ProjectId in task data, skipping");
    return false;
  }

  if (data?.GitHubIssueId) {
    logger.debug(`Task ${taskId} already linked to GitHub Issue, skipping creation`);
    return false;
  }

  return true;
}

/**
 * Process active integrations with a handler map
 * @param {string} projectId - Project ID
 * @param {Object} handlers - Map of service types to handler functions
 * @param {Function} handlerArgs - Function that returns args for each handler
 */
async function processActiveIntegrations(projectId, handlers, handlerArgs) {
  const integrations = await getProjectIntegrations(projectId);

  for (const integration of integrations) {
    if (!integration.IsActive) continue;

    const serviceType = integration.ServiceType?.toLowerCase();
    const handler = handlers[serviceType];

    if (handler) {
      await handler(...handlerArgs(integration));
    }
  }
}

/**
 * Обработать создание задачи - создать Issue в GitHub
 */
async function handleTaskCreated(taskId, data) {
  logger.info(`Task ${taskId} created, checking for task integrations`);

  if (!shouldProcessTaskCreation(taskId, data)) return;

  await processActiveIntegrations(
    data.ProjectId,
    TASK_CREATE_HANDLERS,
    (integration) => [taskId, data, integration]
  );
}

/**
 * Check if task update/completion should be processed
 */
function shouldProcessTaskUpdate(taskId, data, action) {
  if (!data?.ProjectId) return false;

  if (!data?.GitHubIssueNumber) {
    logger.debug(`Task ${taskId} has no linked GitHub Issue, skipping ${action}`);
    return false;
  }

  return true;
}

/**
 * Handle GitHub task update (currently skipped)
 */
async function handleGitHubTaskUpdate(taskId, integration) {
  const accessToken = await getIntegrationAccessToken(integration.Id);
  if (!accessToken) return;

  logger.debug(`Task ${taskId} update sync to GitHub skipped (not critical)`);
}

/**
 * Service handlers for task updates
 */
const TASK_UPDATE_HANDLERS = {
  github: handleGitHubTaskUpdate,
};

/**
 * Обработать обновление задачи - обновить Issue в GitHub
 */
async function handleTaskUpdated(taskId, data) {
  logger.info(`Task ${taskId} updated, syncing with external systems`);

  if (!shouldProcessTaskUpdate(taskId, data, "update")) return;

  await processActiveIntegrations(
    data.ProjectId,
    TASK_UPDATE_HANDLERS,
    (integration) => [taskId, integration]
  );
}

/**
 * Close GitHub issue for completed task
 */
async function closeGitHubTaskIssue(taskId, data, integration) {
  const accessToken = await getIntegrationAccessToken(integration.Id);
  if (!accessToken) {
    logger.warn(`No access token for GitHub integration ${integration.Id}`);
    return;
  }

  try {
    await closeGitHubIssue(data, integration, accessToken);
    logger.info(`Closed GitHub Issue #${data.GitHubIssueNumber} for task ${taskId}`);
  } catch (error) {
    logger.error(`Failed to close GitHub Issue for task ${taskId}:`, error.message);
  }
}

/**
 * Service handlers for task completion
 */
const TASK_COMPLETE_HANDLERS = {
  github: closeGitHubTaskIssue,
  jira: (taskId, data, integration) => logUnsupportedService(integration),
  trello: (taskId, data, integration) => logUnsupportedService(integration),
};

/**
 * Обработать завершение задачи - закрыть Issue в GitHub
 */
async function handleTaskCompleted(taskId, data) {
  logger.info(`Task ${taskId} completed, updating external systems`);

  if (!shouldProcessTaskUpdate(taskId, data, "close")) return;

  await processActiveIntegrations(
    data.ProjectId,
    TASK_COMPLETE_HANDLERS,
    (integration) => [taskId, data, integration]
  );
}

/**
 * Обработать публикацию showcase - уведомить внешние системы
 */
function handleShowcasePublished(projectId, _data) {
  logger.info(`Showcase published for project ${projectId}`);

  // Можно отправить уведомление в Slack, Discord, или создать release в GitHub
  // (требует реализации API для каждого провайдера)
}

/**
 * Получить интеграции проекта из Core API
 * I-07: Use dedicated internal endpoint with HMAC service auth (not JWT-protected endpoint)
 */
async function getProjectIntegrations(projectId) {
  try {
    const url = `${CORE_API_URL}/api/integrations/project/${projectId}/for-internal`;
    const response = await axios.get(url, {
      headers: buildInternalApiHeadersForUrl(url),
    });
    return response.data || [];
  } catch (error) {
    logger.error(`Failed to get integrations for project ${projectId}:`, error);
    return [];
  }
}

/**
 * Log token retrieval errors appropriately
 */
function logTokenError(integrationId, error) {
  if (error?.response?.status === 404) {
    logger.debug(`Integration ${integrationId} has no access token configured`);
  } else {
    logger.error(
      `Failed to get access token for integration ${integrationId}:`,
      error?.message || error,
    );
  }
}

/**
 * Получить access token интеграции из Core API
 */
async function getIntegrationAccessToken(integrationId) {
  try {
    logger.debug(`Getting access token for integration ${integrationId}`);

    const response = await axios.get(
      `${CORE_API_URL}/api/integrations/${integrationId}/token`,
      {
        headers: buildInternalApiHeadersForIntegrationToken(integrationId),
        timeout: 5000,
      },
    );

    if (response.data?.accessToken) {
      return response.data.accessToken;
    }

    logger.warn(`No access token returned for integration ${integrationId}`);
    return null;
  } catch (error) {
    logTokenError(integrationId, error);
    return null;
  }
}

