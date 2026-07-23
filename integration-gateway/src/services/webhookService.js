/**
 * Webhook Service
 * Creates and deletes webhooks in GitHub/GitLab via their APIs
 */

import axios from "axios";
import { logger } from "../utils/logger.js";

/**
 * Create a webhook in GitHub
 * @param {string} repository - Repository in format "owner/repo"
 * @param {string} webhookUrl - URL to receive webhook events
 * @param {string[]} events - Events to subscribe to (push, pull_request, issues, etc.)
 * @param {string} accessToken - GitHub access token
 * @returns {Promise<object>} Webhook data
 */
export async function createGitHubWebhook(
  repository,
  webhookUrl,
  events,
  accessToken,
) {
  const [owner, repo] = repository.split("/");
  if (!owner || !repo) {
    throw new Error(
      `Invalid repository format: ${repository}. Expected: owner/repo`,
    );
  }

  try {
    // Generate secret for webhook
    const crypto = await import("crypto");
    const webhookSecret = crypto.randomBytes(32).toString("hex");

    const response = await axios.post(
      `https://api.github.com/repos/${owner}/${repo}/hooks`,
      {
        name: "web",
        active: true,
        events: events || ["push", "pull_request", "issues", "release"],
        config: {
          url: webhookUrl,
          content_type: "json",
          secret: webhookSecret,
          insecure_ssl: "0", // Use HTTPS
        },
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      },
    );

    logger.info(
      `GitHub webhook created: ${response.data.id} for ${repository}`,
    );

    return {
      webhookId: response.data.id.toString(),
      webhookUrl: response.data.config.url,
      secret: webhookSecret, // Important: save in Integration.Config
      events: response.data.events,
      createdAt: response.data.created_at,
    };
  } catch (error) {
    logger.error("Failed to create GitHub webhook:", error);
    if (error.response) {
      throw new Error(
        `GitHub API error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}

/**
 * Delete a webhook in GitHub
 * @param {string} repository - Repository in format "owner/repo"
 * @param {string|number} webhookId - Webhook ID
 * @param {string} accessToken - GitHub access token
 * @returns {Promise<boolean>} Deletion success
 */
export async function deleteGitHubWebhook(repository, webhookId, accessToken) {
  const [owner, repo] = repository.split("/");
  if (!owner || !repo) {
    throw new Error(
      `Invalid repository format: ${repository}. Expected: owner/repo`,
    );
  }

  try {
    await axios.delete(
      `https://api.github.com/repos/${owner}/${repo}/hooks/${webhookId}`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      },
    );

    logger.info(`GitHub webhook deleted: ${webhookId} for ${repository}`);
    return true;
  } catch (error) {
    logger.error("Failed to delete GitHub webhook:", error);
    if (error.response && error.response.status === 404) {
      // Webhook already deleted, consider success
      logger.warn(
        `GitHub webhook ${webhookId} not found, assuming already deleted`,
      );
      return true;
    }
    if (error.response) {
      throw new Error(
        `GitHub API error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}

/**
 * Create a webhook in GitLab
 * @param {string|number} projectId - GitLab project ID
 * @param {string} webhookUrl - URL to receive webhook events
 * @param {string[]} events - Events to subscribe to
 * @param {string} accessToken - GitLab access token
 * @param {string} gitlabUrl - GitLab URL (default: https://gitlab.com/api/v4)
 * @returns {Promise<object>} Webhook data
 */
export async function createGitLabWebhook(
  projectId,
  webhookUrl,
  events,
  accessToken,
  gitlabUrl = "https://gitlab.com/api/v4",
) {
  try {
    // Generate secret for webhook
    const crypto = await import("crypto");
    const webhookSecret = crypto.randomBytes(32).toString("hex");

    // GitLab uses token as secret, but we can use a separate secret
    const response = await axios.post(
      `${gitlabUrl}/projects/${projectId}/hooks`,
      {
        url: webhookUrl,
        push_events: events?.includes("push") || true,
        issues_events: events?.includes("issues") || false,
        merge_requests_events: events?.includes("merge_request") || true,
        tag_push_events: events?.includes("tag_push") || false,
        note_events: events?.includes("note") || false,
        job_events: events?.includes("job") || false,
        pipeline_events: events?.includes("pipeline") || false,
        wiki_page_events: events?.includes("wiki_page") || false,
        enable_ssl_verification: true,
        token: webhookSecret, // GitLab uses token as secret
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      },
    );

    logger.info(
      `GitLab webhook created: ${response.data.id} for project ${projectId}`,
    );

    return {
      webhookId: response.data.id.toString(),
      webhookUrl: response.data.url,
      secret: webhookSecret, // Important: save in Integration.Config
      events: response.data,
      createdAt: response.data.created_at,
    };
  } catch (error) {
    logger.error("Failed to create GitLab webhook:", error);
    if (error.response) {
      throw new Error(
        `GitLab API error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}

/**
 * Delete a webhook in GitLab
 * @param {string|number} projectId - GitLab project ID
 * @param {string|number} webhookId - Webhook ID
 * @param {string} accessToken - GitLab access token
 * @param {string} gitlabUrl - GitLab URL (default: https://gitlab.com/api/v4)
 * @returns {Promise<boolean>} Deletion success
 */
export async function deleteGitLabWebhook(
  projectId,
  webhookId,
  accessToken,
  gitlabUrl = "https://gitlab.com/api/v4",
) {
  try {
    await axios.delete(
      `${gitlabUrl}/projects/${projectId}/hooks/${webhookId}`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      },
    );

    logger.info(
      `GitLab webhook deleted: ${webhookId} for project ${projectId}`,
    );
    return true;
  } catch (error) {
    logger.error("Failed to delete GitLab webhook:", error);
    if (error.response && error.response.status === 404) {
      // Webhook already deleted, consider success
      logger.warn(
        `GitLab webhook ${webhookId} not found, assuming already deleted`,
      );
      return true;
    }
    if (error.response) {
      throw new Error(
        `GitLab API error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}
