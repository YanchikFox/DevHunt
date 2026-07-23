/**
 * SMS Service
 * Send SMS notifications via Twilio or another provider
 */

import { logger } from "../utils/logger.js";

const SMS_PROVIDER = process.env.SMS_PROVIDER || "twilio"; // twilio, aws-sns, etc.
const TWILIO_ACCOUNT_SID = process.env.TWILIO_ACCOUNT_SID;
const TWILIO_AUTH_TOKEN = process.env.TWILIO_AUTH_TOKEN;
const TWILIO_PHONE_NUMBER = process.env.TWILIO_PHONE_NUMBER;

/**
 * Send SMS
 * @param {object} options - { to: phone number, message: text }
 * @returns {Promise<object>} Send result
 */
export async function sendSMS(options) {
  const { to, message } = options;

  if (!to || !message) {
    return {
      success: false,
      error: "Phone number and message are required",
    };
  }

  // Phone number validation (basic)
  const phoneRegex = /^\+?[1-9]\d{1,14}$/;
  if (!phoneRegex.test(to.replace(/\s/g, ""))) {
    return {
      success: false,
      error: "Invalid phone number format. Expected: +1234567890",
    };
  }

  try {
    switch (SMS_PROVIDER.toLowerCase()) {
      case "twilio":
        return await sendViaTwilio(to, message);
      case "aws-sns":
        return sendViaAWSSNS(to, message);
      default:
        logger.warn(`SMS provider ${SMS_PROVIDER} not implemented, using mock`);
        return sendViaMock(to, message);
    }
  } catch (error) {
    logger.error("SMS sending error:", error);
    return {
      success: false,
      error: error.message || "Failed to send SMS",
    };
  }
}

/**
 * Send via Twilio
 */
async function sendViaTwilio(to, message) {
  if (!TWILIO_ACCOUNT_SID || !TWILIO_AUTH_TOKEN || !TWILIO_PHONE_NUMBER) {
    logger.warn("Twilio credentials not configured, using mock");
    return sendViaMock(to, message);
  }

  try {
    // Twilio SDK would be: const twilio = require('twilio');
    // For now, we'll use axios to call Twilio REST API
    const axios = (await import("axios")).default;
    const response = await axios.post(
      `https://api.twilio.com/2010-04-01/Accounts/${TWILIO_ACCOUNT_SID}/Messages.json`,
      new URLSearchParams({
        From: TWILIO_PHONE_NUMBER,
        To: to,
        Body: message,
      }),
      {
        auth: {
          username: TWILIO_ACCOUNT_SID,
          password: TWILIO_AUTH_TOKEN,
        },
        headers: {
          "Content-Type": "application/x-www-form-urlencoded",
        },
      },
    );

    logger.info(`SMS sent via Twilio to ${to}: ${response.data.sid}`);

    return {
      success: true,
      id: response.data.sid,
      provider: "twilio",
      sentAt: new Date().toISOString(),
    };
  } catch (error) {
    logger.error("Twilio API error:", error);
    if (error.response) {
      throw new Error(
        `Twilio error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}

/**
 * Send via AWS SNS
 */
function sendViaAWSSNS(to, message) {
  // TODO: Implement AWS SNS integration
  logger.warn("AWS SNS SMS not yet implemented, using mock");
  return sendViaMock(to, message);
}

/**
 * Mock send (for development)
 */
function sendViaMock(to, message) {
  logger.info(`[MOCK SMS] To: ${to}, Message: ${message.substring(0, 50)}...`);

  // In development mode just log it
  return {
    success: true,
    id: `sms_mock_${Date.now()}`,
    provider: "mock",
    sentAt: new Date().toISOString(),
    note: "SMS sent in mock mode (not actually sent)",
  };
}
