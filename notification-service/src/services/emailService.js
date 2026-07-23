/**
 * Email Service
 * Поддержка SendGrid и SMTP
 */

import sgMail from "@sendgrid/mail";
import nodemailer from "nodemailer";
import { logger } from "../utils/logger.js";
import { renderTemplate } from "../utils/templateRenderer.js";

const EMAIL_PROVIDER = process.env.EMAIL_PROVIDER || "smtp";
const SENDGRID_API_KEY = process.env.SENDGRID_API_KEY;
const SMTP_HOST = process.env.SMTP_HOST || "smtp.gmail.com";
const SMTP_PORT = parseInt(process.env.SMTP_PORT || "587");
const SMTP_USER = process.env.SMTP_USER;
const SMTP_PASSWORD = process.env.SMTP_PASSWORD;
const FROM_EMAIL = process.env.FROM_EMAIL || "noreply@devhunt.com";
const FROM_NAME = process.env.FROM_NAME || "DevHunt";

let smtpTransporter = null;

/**
 * Initialize SMTP transporter
 */
function initSMTP() {
  if (smtpTransporter) return smtpTransporter;

  if (!SMTP_USER || !SMTP_PASSWORD) {
    logger.warn("SMTP credentials not configured, email sending may fail");
  }

  smtpTransporter = nodemailer.createTransport({
    host: SMTP_HOST,
    port: SMTP_PORT,
    secure: SMTP_PORT === 465, // true for 465, false for other ports
    auth: {
      user: SMTP_USER,
      pass: SMTP_PASSWORD,
    },
  });

  return smtpTransporter;
}

/**
 * Initialize SendGrid
 */
function initSendGrid() {
  if (SENDGRID_API_KEY) {
    sgMail.setApiKey(SENDGRID_API_KEY);
    return true;
  }
  logger.warn("SendGrid API key not configured");
  return false;
}

/**
 * Send email via SendGrid
 */
async function sendViaSendGrid(options) {
  try {
    const html = options.template
      ? renderTemplate(options.template, options.data || {})
      : options.html;

    const msg = {
      to: options.to,
      from: {
        email: FROM_EMAIL,
        name: FROM_NAME,
      },
      subject: options.subject,
      html,
      ...(options.text && { text: options.text }),
    };

    await sgMail.send(msg);
    logger.info(`Email sent via SendGrid to ${options.to}`);
    return { success: true, provider: "sendgrid" };
  } catch (error) {
    logger.error("SendGrid error:", error);
    if (error.response) {
      logger.error("SendGrid response:", error.response.body);
    }
    throw error;
  }
}

/**
 * Send email via SMTP
 */
async function sendViaSMTP(options) {
  try {
    const transporter = initSMTP();

    const html = options.template
      ? renderTemplate(options.template, options.data || {})
      : options.html;

    const mailOptions = {
      from: `"${FROM_NAME}" <${FROM_EMAIL}>`,
      to: options.to,
      subject: options.subject,
      html,
      ...(options.text && { text: options.text }),
    };

    const info = await transporter.sendMail(mailOptions);
    logger.info(
      `Email sent via SMTP to ${options.to}, messageId: ${info.messageId}`,
    );
    return { success: true, provider: "smtp", messageId: info.messageId };
  } catch (error) {
    logger.error("SMTP error:", error);
    throw error;
  }
}

/**
 * Send email using configured provider
 */
export async function sendEmail(options) {
  try {
    // Validate required fields
    if (!options.to) {
      throw new Error("Recipient email is required");
    }
    if (!options.subject) {
      throw new Error("Email subject is required");
    }
    if (!options.html && !options.template) {
      throw new Error("Email content (html or template) is required");
    }

    // Choose provider
    if (EMAIL_PROVIDER === "sendgrid") {
      if (initSendGrid()) {
        return await sendViaSendGrid(options);
      } else {
        logger.warn("SendGrid not configured, falling back to SMTP");
        return await sendViaSMTP(options);
      }
    } else {
      // Default to SMTP
      return await sendViaSMTP(options);
    }
  } catch (error) {
    logger.error("Failed to send email:", error);
    return {
      success: false,
      error: error.message || "Unknown error",
      message: "Failed to send email",
    };
  }
}
