/**
 * Push Notification Service
 * Send push notifications через FCM (Firebase Cloud Messaging) или другой провайдер
 */

import { logger } from "../utils/logger.js";

const PUSH_PROVIDER = process.env.PUSH_PROVIDER || "fcm"; // fcm, apns, etc.
const FCM_SERVER_KEY = process.env.FCM_SERVER_KEY;

/**
 * Send push notification
 * @param {object} options - { to: device token, title: string, body: string, data: object }
 * @returns {Promise<object>} Send result
 */
export async function sendPush(options) {
  const { to, title, body, data } = options;

  if (!to || !title || !body) {
    return {
      success: false,
      error: "Device token, title and body are required",
    };
  }

  try {
    switch (PUSH_PROVIDER.toLowerCase()) {
      case "fcm":
        return await sendViaFCM(to, title, body, data);
      case "apns":
        return sendViaAPNS(to, title, body, data);
      default:
        logger.warn(
          `Push provider ${PUSH_PROVIDER} not implemented, using mock`,
        );
        return sendViaMock(to, title, body, data);
    }
  } catch (error) {
    logger.error("Push notification sending error:", error);
    return {
      success: false,
      error: error.message || "Failed to send push notification",
    };
  }
}

/**
 * Отправка через FCM (Firebase Cloud Messaging)
 */
async function sendViaFCM(deviceToken, title, body, data) {
  if (!FCM_SERVER_KEY) {
    logger.warn("FCM server key not configured, using mock");
    return sendViaMock(deviceToken, title, body, data);
  }

  try {
    const axios = (await import("axios")).default;
    const response = await axios.post(
      "https://fcm.googleapis.com/fcm/send",
      {
        to: deviceToken,
        notification: {
          title,
          body,
          sound: "default",
        },
        data: data || {},
        priority: "high",
      },
      {
        headers: {
          Authorization: `key=${FCM_SERVER_KEY}`,
          "Content-Type": "application/json",
        },
      },
    );

    if (response.data.success === 1 || response.data.results?.[0]?.message_id) {
      logger.info(
        `Push notification sent via FCM to device ${deviceToken.substring(0, 20)}...`,
      );

      return {
        success: true,
        id:
          response.data.multicast_id ||
          response.data.results?.[0]?.message_id ||
          `fcm_${Date.now()}`,
        provider: "fcm",
        sentAt: new Date().toISOString(),
      };
    } else {
      throw new Error(
        `FCM error: ${response.data.results?.[0]?.error || "Unknown error"}`,
      );
    }
  } catch (error) {
    logger.error("FCM API error:", error);
    if (error.response) {
      throw new Error(
        `FCM error: ${error.response.status} - ${error.response.data?.error || error.message}`,
      );
    }
    throw error;
  }
}

/**
 * Отправка через APNS (Apple Push Notification Service)
 */
function sendViaAPNS(deviceToken, title, body, data) {
  // TODO: Implement APNS integration
  logger.warn("APNS push notifications not yet implemented, using mock");
  return sendViaMock(deviceToken, title, body, data);
}

/**
 * Mock отправка (для разработки)
 */
function sendViaMock(deviceToken, title, body, _data) {
  logger.info(
    `[MOCK PUSH] To: ${deviceToken.substring(0, 20)}..., Title: ${title}, Body: ${body.substring(0, 50)}...`,
  );

  // В development mode просто логируем
  return {
    success: true,
    id: `push_mock_${Date.now()}`,
    provider: "mock",
    sentAt: new Date().toISOString(),
    note: "Push notification sent in mock mode (not actually sent)",
  };
}
