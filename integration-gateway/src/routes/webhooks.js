/**
 * Webhook Routes
 * Signature verification for GitHub, GitLab webhooks
 */

import express from "express";
import crypto from "crypto";
import { logger } from "../utils/logger.js";
import { recordWebhookVerify } from "../metrics.js";
import { processGitHubWebhook } from "../services/webhookHandler.js";

const router = express.Router();

/**
 * Verify webhook signature
 */
router.post("/verify-signature", (req, res) => {
  try {
    const { serviceType, payload, signature } = req.body;

    if (!serviceType || !payload || !signature) {
      return res.status(400).json({
        isValid: false,
        error: "Missing required fields: serviceType, payload, signature",
      });
    }

    const secret = getWebhookSecret(serviceType);
    if (!secret) {
      logger.warn(`Webhook secret not configured for ${serviceType}`);
      recordWebhookVerify(serviceType || "unknown", "missing_secret");
      return res.json({
        isValid: false,
        error: "Webhook secret not configured",
      });
    }

    const isValid = verifySignature(serviceType, payload, signature, secret);

    logger.info(`Webhook signature verification for ${serviceType}:`, {
      isValid,
    });
    recordWebhookVerify(serviceType || "unknown", isValid ? "success" : "failure");

    return res.json({
      isValid,
      serviceType,
    });
  } catch (error) {
    logger.error("Error verifying webhook signature:", error);
    return res.status(500).json({
      isValid: false,
      error: error.message || "Internal server error",
    });
  }
});

/**
 * GitHub webhook endpoint
 */
router.post("/github", async (req, res) => {
  const signature =
    req.headers["x-hub-signature-256"] || req.headers["x-hub-signature"];
  const rawBody = req.rawBody ?? Buffer.from(JSON.stringify(req.body));
  const secret = process.env.GITHUB_WEBHOOK_SECRET;

  // Signature verification is optional in dev mode
  if (secret) {
    if (!signature) {
      logger.warn("GitHub webhook: missing signature");
      recordWebhookVerify("github", "missing_secret");
      return res.status(401).json({ error: "Missing signature" });
    }

    const isValid = verifyGitHubSignature(rawBody, signature, secret);

    if (!isValid) {
      logger.warn("GitHub webhook: invalid signature");
      recordWebhookVerify("github", "failure");
      return res.status(401).json({ error: "Invalid signature" });
    }
  } else {
    logger.warn("GitHub webhook: no secret configured, skipping verification");
  }

  const eventType = req.headers["x-github-event"];
  const deliveryId = req.headers["x-github-delivery"];

  logger.info("GitHub webhook received:", { event: eventType, delivery: deliveryId });
  recordWebhookVerify("github", "success");

  // Process the webhook asynchronously
  try {
    const result = await processGitHubWebhook(eventType, req.body);
    return res.json({
      success: true,
      message: "Webhook processed",
      event: eventType,
      result,
    });
  } catch (error) {
    logger.error("GitHub webhook processing error:", error);
    // Still return 200 to prevent GitHub from retrying
    return res.json({
      success: false,
      message: "Webhook received but processing failed",
      error: error.message,
    });
  }
});

/**
 * GitLab webhook endpoint
 */
router.post("/gitlab", (req, res) => {
  const token = req.headers["x-gitlab-token"];
  const secret = process.env.GITLAB_WEBHOOK_SECRET;

  if (!token || !secret) {
    logger.warn("GitLab webhook: missing token or secret");
    recordWebhookVerify("gitlab", "missing_secret");
    return res.status(401).json({ error: "Missing token or secret" });
  }

  if (!secureCompare(token, secret)) {
    logger.warn("GitLab webhook: invalid token");
    recordWebhookVerify("gitlab", "failure");
    return res.status(401).json({ error: "Invalid token" });
  }

  logger.info("GitLab webhook received:", {
    event: req.headers["x-gitlab-event"],
  });
  recordWebhookVerify("gitlab", "success");

  return res.json({ success: true, message: "Webhook received" });
});

/**
 * Get webhook secret for service type
 */
function getWebhookSecret(serviceType) {
  const secrets = {
    github: process.env.GITHUB_WEBHOOK_SECRET,
    gitlab: process.env.GITLAB_WEBHOOK_SECRET,
  };
  return secrets[serviceType.toLowerCase()];
}

/**
 * Verify webhook signature based on service type
 */
function verifySignature(serviceType, payload, signature, secret) {
  switch (serviceType.toLowerCase()) {
    case "github":
      return verifyGitHubSignature(
        typeof payload === "string" ? payload : JSON.stringify(payload),
        signature,
        secret,
      );
    case "gitlab":
      return verifyGitLabSignature(
        typeof payload === "string" ? payload : JSON.stringify(payload),
        signature,
        secret,
      );
    default:
      logger.warn(
        `Unknown service type for signature verification: ${serviceType}`,
      );
      return false;
  }
}

/**
 * Verify GitHub webhook signature (SHA256)
 */
function verifyGitHubSignature(payload, signature, secret) {
  try {
    const hmac = crypto.createHmac("sha256", secret);
    const digest = `sha256=${hmac.update(payload).digest("hex")}`;
    return crypto.timingSafeEqual(Buffer.from(signature), Buffer.from(digest));
  } catch (error) {
    logger.error("Error verifying GitHub signature:", error);
    return false;
  }
}

/**
 * Verify GitLab webhook signature
 */
function verifyGitLabSignature(payload, signature, secret) {
  try {
    // GitLab uses the secret as a token in header, not HMAC
    // But for consistency, we can also verify HMAC if provided
    if (secureCompare(signature, secret)) {
      return true;
    }

    // Try HMAC verification
    const hmac = crypto.createHmac("sha256", secret);
    const digest = hmac.update(payload).digest("hex");
    return secureCompare(signature, digest);
  } catch (error) {
    logger.error("Error verifying GitLab signature:", error);
    return false;
  }
}

function secureCompare(a, b) {
  const aBuf = Buffer.from(a ?? "", "utf8");
  const bBuf = Buffer.from(b ?? "", "utf8");
  if (aBuf.length !== bBuf.length) {
    return false;
  }

  return crypto.timingSafeEqual(aBuf, bBuf);
}

export { router as webhookRouter };
