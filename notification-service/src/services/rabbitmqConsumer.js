/**
 * RabbitMQ Consumer
 * Subscribes to events из Event Bus (devhunt.events exchange)
 * Обрабатывает события и отправляет уведомления
 */

import amqp from "amqplib";
import { logger } from "../utils/logger.js";
import { sendEmail } from "./emailService.js";
import { recordEventMessage, setRabbitConnectionState } from "../metrics.js";

const RABBITMQ_URL =
  process.env.RABBITMQ_URL ||
  process.env.RABBITMQ__CONNECTIONSTRING ||
  "amqp://devhunt:devhunt_password@message-broker:5672";
const EXCHANGE_NAME = process.env.RABBITMQ_EXCHANGE_NAME || "devhunt.events";
const NOTIFICATION_QUEUE =
  process.env.NOTIFICATION_QUEUE || "devhunt.notifications";
const MAX_RETRY_COUNT = parseInt(process.env.RABBITMQ_MAX_RETRIES || "3", 10);

/**
 * Start RabbitMQ consumer for async notifications from Event Bus
 */
export async function rabbitMQConsumer() {
  try {
    logger.info(`Connecting to RabbitMQ at ${RABBITMQ_URL}`);
    const connection = await amqp.connect(RABBITMQ_URL);
    setRabbitConnectionState(true);
    const channel = await connection.createChannel();

    // Declare exchange (should already exist, but ensure it)
    await channel.assertExchange(EXCHANGE_NAME, "topic", {
      durable: true,
      autoDelete: false,
    });

    // Declare queue for notifications
    await channel.assertQueue(NOTIFICATION_QUEUE, {
      durable: true,
    });

    // Bind queue to exchange with routing keys for notification-worthy events
    // Подписываемся на события, которые требуют уведомлений
    const routingKeys = [
      "project.*", // Все события проекта
      "showcase.*", // Все события showcase
      "team.*", // События команды
      "invitation.*", // События приглашений
      "message.sent", // Новые сообщения
      "conversation.created", // Новые чаты
    ];

    for (const routingKey of routingKeys) {
      await channel.bindQueue(NOTIFICATION_QUEUE, EXCHANGE_NAME, routingKey);
      logger.info(`Bound queue to exchange with routing key: ${routingKey}`);
    }

    logger.info(
      `Listening for events on exchange: ${EXCHANGE_NAME}, queue: ${NOTIFICATION_QUEUE}`,
    );

    // Consume messages
    channel.consume(
      NOTIFICATION_QUEUE,
      async (msg) => {
        if (msg) {
          const routingKey = msg.fields.routingKey || "unknown";
          try {
            const event = JSON.parse(msg.content.toString());

            logger.info(
              `Received event: ${event.EventType} for ${event.EntityType}:${event.EntityId} (routing: ${routingKey})`,
            );

            // Обработать событие и отправить уведомления
            await processEvent(event, routingKey);

            // Acknowledge message
            channel.ack(msg);
            recordEventMessage(routingKey, "processed");
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
              recordEventMessage(routingKey, "retried");
              logger.warn(
                `Requeued message for ${routingKey} with retry ${retryCount + 1}/${MAX_RETRY_COUNT}`,
              );
            } else {
              recordEventMessage(routingKey, "dropped");
              logger.error(
                `Event failed after ${retryCount} retries, dropping message`,
              );
              channel.ack(msg);
            }
          }
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
        rabbitMQConsumer().catch((err) => {
          logger.error("Reconnection failed:", err);
        });
      }, 5000);
    });

    logger.info("RabbitMQ consumer started successfully");
  } catch (error) {
    setRabbitConnectionState(false);
    logger.error("Failed to start RabbitMQ consumer:", error);
    // Попытка переподключения через 10 секунд
    setTimeout(() => {
      rabbitMQConsumer().catch((err) => {
        logger.error("Reconnection failed:", err);
      });
    }, 10000);
  }
}

/**
 * Обработать событие и отправить уведомления
 */
async function processEvent(event, _routingKey) {
  const { EventType, EntityId, Data } = event;

  try {
    // Определяем тип события и отправляем соответствующие уведомления
    switch (EventType) {
      case "project.created":
        await sendProjectNotifications(EntityId, Data, "project_created");
        break;
      case "project.updated":
        await sendProjectNotifications(EntityId, Data, "project_updated");
        break;
      case "project.archived":
      case "project.cancelled":
        await sendProjectNotifications(
          EntityId,
          Data,
          "project_status_changed",
        );
        break;
      case "team.member.joined":
        await sendTeamMemberNotifications(EntityId, Data, "member_joined");
        break;
      case "team.member.removed":
      case "team.member.left":
        await sendTeamMemberNotifications(EntityId, Data, "member_left");
        break;
      case "invitation.sent":
        await sendInvitationNotifications(EntityId, Data, "invitation_sent");
        break;
      case "invitation.accepted":
        await sendInvitationNotifications(
          EntityId,
          Data,
          "invitation_accepted",
        );
        break;
      case "showcase.published":
        sendShowcaseNotifications(EntityId, Data, "showcase_published");
        break;
      case "message.sent":
        sendMessageNotifications(EntityId, Data);
        break;
      default:
        logger.debug(`Event ${EventType} does not require notifications`);
    }
  } catch (error) {
    logger.error(`Error processing event ${EventType}:`, error);
    throw error; // Пробрасываем для retry логики
  }
}

/**
 * Отправить уведомления команде проекта
 */
async function sendProjectNotifications(projectId, data, eventType) {
  // TODO: Получить список участников команды из Core API
  // Пока что просто логируем
  logger.info(
    `Sending project notifications for project ${projectId}, event: ${eventType}`,
  );

  // В реальности здесь:
  // 1. Запрос к Core API для получения команды проекта
  // 2. Отправка email/push уведомлений каждому участнику
  // 3. Создание in-app уведомлений в БД

  // Пример: отправка email владельцу проекта
  if (data?.OwnerId) {
    await sendEmail({
      to: `${data.OwnerId}@devhunt.local`, // В реальности получить email из БД
      subject: `Project ${eventType.replace("_", " ")}`,
      html: `Your project has been ${eventType}.`,
    }).catch((err) => logger.warn("Failed to send email notification:", err));
  }
}

/**
 * Отправить уведомления о событиях команды
 */
async function sendTeamMemberNotifications(projectId, data, eventType) {
  logger.info(
    `Sending team notifications for project ${projectId}, event: ${eventType}`,
  );

  // Уведомление владельцу проекта
  if (data?.UserId) {
    // В реальности получить email владельца из БД
    await sendEmail({
      to: "owner@devhunt.local",
      subject: `Team member ${eventType}`,
      html: `A team member has ${eventType} your project.`,
    }).catch((err) => logger.warn("Failed to send email notification:", err));
  }
}

/**
 * Отправить уведомления о приглашениях
 */
async function sendInvitationNotifications(invitationId, data, eventType) {
  logger.info(
    `Sending invitation notifications for invitation ${invitationId}, event: ${eventType}`,
  );

  if (eventType === "invitation_sent") {
    // Уведомление получателю приглашения
    if (data?.InviteeId) {
      await sendEmail({
        to: `${data.InviteeId}@devhunt.local`,
        subject: "New project invitation",
        html: "You have been invited to join a project.",
      }).catch((err) => logger.warn("Failed to send email notification:", err));
    }
  } else if (eventType === "invitation_accepted") {
    // Уведомление отправителю приглашения
    if (data?.ProjectId) {
      // Получить владельца проекта и отправить уведомление
      logger.info(`Invitation accepted for project ${data.ProjectId}`);
    }
  }
}

/**
 * Отправить уведомления о showcase
 */
function sendShowcaseNotifications(projectId, data, eventType) {
  logger.info(
    `Sending showcase notifications for project ${projectId}, event: ${eventType}`,
  );

  // Уведомление команде проекта о публикации в showcase
  if (eventType === "showcase_published") {
    // В реальности получить команду и отправить уведомления
    logger.info(`Showcase published for project ${projectId}`);
  }
}

/**
 * Отправить уведомления о новых сообщениях
 */
function sendMessageNotifications(messageId, data) {
  logger.info(`Sending message notifications for message ${messageId}`);

  // В реальности:
  // 1. Получить участников чата из Core API
  // 2. Отправить push уведомления (кроме отправителя)
  // 3. Создать in-app уведомления

  if (data?.ConversationId && data?.SenderId) {
    logger.info(
      `New message in conversation ${data.ConversationId} from user ${data.SenderId}`,
    );
  }
}
