/**
 * GitHub Issue Service
 * Bidirectional sync between DevHunt Tasks and GitHub Issues
 */

import axios from "axios";
import { logger } from "../utils/logger.js";
import { mergeInternalApiHeaders } from "../utils/internalApiAuth.js";

const GITHUB_API_URL = "https://api.github.com";
const CORE_API_URL = process.env.CORE_API_URL || "http://core-api:5000";

/**
 * Create a GitHub Issue from a DevHunt Task
 * @param {object} task - Task data from DevHunt
 * @param {object} integration - Integration config with repository info
 * @param {string} accessToken - GitHub access token
 * @returns {Promise<object|null>} Created issue data or null on failure
 */
export async function createGitHubIssue(task, integration, accessToken) {
  const repository = integration.Config?.repository;
  if (!repository) {
    logger.warn("No repository configured for GitHub integration");
    return null;
  }

  const [owner, repo] = repository.split("/");
  if (!owner || !repo) {
    logger.error(`Invalid repository format: ${repository}`);
    return null;
  }

  try {
    // Map priority to labels
    const labels = [];
    if (task.Priority) {
      const priorityMap = {
        urgent: "priority: critical",
        high: "priority: high",
        medium: "priority: medium",
        low: "priority: low",
      };
      if (priorityMap[task.Priority]) {
        labels.push(priorityMap[task.Priority]);
      }
    }

    // Add tags as labels
    if (task.Tags) {
      const tags = task.Tags.split(",").map((t) => t.trim()).filter(Boolean);
      labels.push(...tags);
    }

    // Build issue body with DevHunt link
    const body = buildIssueBody(task);

    const response = await axios.post(
      `${GITHUB_API_URL}/repos/${owner}/${repo}/issues`,
      {
        title: task.Title,
        body,
        labels,
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      }
    );

    const issue = response.data;
    logger.info(`Created GitHub Issue #${issue.number} for Task ${task.Id}`);

    // Update task with GitHub Issue info via Core API
    await updateTaskWithIssueInfo(task.Id, {
      gitHubIssueId: issue.id,
      gitHubIssueNumber: issue.number,
      gitHubIssueUrl: issue.html_url,
    });

    return issue;
  } catch (error) {
    logger.error("Failed to create GitHub Issue:", error.response?.data || error.message);
    return null;
  }
}

/**
 * Update a GitHub Issue from a DevHunt Task
 * @param {object} task - Task data with GitHubIssueNumber
 * @param {object} integration - Integration config
 * @param {string} accessToken - GitHub access token
 */
export async function updateGitHubIssue(task, integration, accessToken) {
  if (!task.GitHubIssueNumber) {
    logger.debug(`Task ${task.Id} has no linked GitHub Issue`);
    return null;
  }

  const repository = integration.Config?.repository;
  if (!repository) return null;

  const [owner, repo] = repository.split("/");

  try {
    const body = buildIssueBody(task);

    const response = await axios.patch(
      `${GITHUB_API_URL}/repos/${owner}/${repo}/issues/${task.GitHubIssueNumber}`,
      {
        title: task.Title,
        body,
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      }
    );

    logger.info(`Updated GitHub Issue #${task.GitHubIssueNumber}`);
    return response.data;
  } catch (error) {
    logger.error("Failed to update GitHub Issue:", error.response?.data || error.message);
    return null;
  }
}

/**
 * Close a GitHub Issue when Task is completed
 * @param {object} task - Task data with GitHubIssueNumber
 * @param {object} integration - Integration config
 * @param {string} accessToken - GitHub access token
 */
export async function closeGitHubIssue(task, integration, accessToken) {
  if (!task.GitHubIssueNumber) {
    logger.debug(`Task ${task.Id} has no linked GitHub Issue to close`);
    return null;
  }

  const repository = integration.Config?.repository;
  if (!repository) return null;

  const [owner, repo] = repository.split("/");

  try {
    const response = await axios.patch(
      `${GITHUB_API_URL}/repos/${owner}/${repo}/issues/${task.GitHubIssueNumber}`,
      {
        state: "closed",
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      }
    );

    logger.info(`Closed GitHub Issue #${task.GitHubIssueNumber}`);
    return response.data;
  } catch (error) {
    logger.error("Failed to close GitHub Issue:", error.response?.data || error.message);
    return null;
  }
}

/**
 * Reopen a GitHub Issue when Task is moved back from done
 * @param {object} task - Task data with GitHubIssueNumber
 * @param {object} integration - Integration config
 * @param {string} accessToken - GitHub access token
 */
export async function reopenGitHubIssue(task, integration, accessToken) {
  if (!task.GitHubIssueNumber) return null;

  const repository = integration.Config?.repository;
  if (!repository) return null;

  const [owner, repo] = repository.split("/");

  try {
    const response = await axios.patch(
      `${GITHUB_API_URL}/repos/${owner}/${repo}/issues/${task.GitHubIssueNumber}`,
      {
        state: "open",
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      }
    );

    logger.info(`Reopened GitHub Issue #${task.GitHubIssueNumber}`);
    return response.data;
  } catch (error) {
    logger.error("Failed to reopen GitHub Issue:", error.response?.data || error.message);
    return null;
  }
}

/**
 * Assign a GitHub Issue to a user
 * @param {object} task - Task with AssignedToUser containing GithubUsername
 * @param {object} integration - Integration config
 * @param {string} accessToken - GitHub access token
 */
export async function assignGitHubIssue(task, integration, accessToken) {
  if (!task.GitHubIssueNumber) return null;

  const githubUsername = task.AssignedToUser?.GithubUsername;
  if (!githubUsername) {
    logger.debug("Assigned user has no GitHub username, skipping assignment");
    return null;
  }

  const repository = integration.Config?.repository;
  if (!repository) return null;

  const [owner, repo] = repository.split("/");

  try {
    const response = await axios.post(
      `${GITHUB_API_URL}/repos/${owner}/${repo}/issues/${task.GitHubIssueNumber}/assignees`,
      {
        assignees: [githubUsername],
      },
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      }
    );

    logger.info(`Assigned GitHub Issue #${task.GitHubIssueNumber} to ${githubUsername}`);
    return response.data;
  } catch (error) {
    logger.error("Failed to assign GitHub Issue:", error.response?.data || error.message);
    return null;
  }
}

/**
 * Build issue body with task description and DevHunt metadata
 */
function buildIssueBody(task) {
  let body = task.Description || "";

  // Add DevHunt metadata footer
  body += "\n\n---\n";
  body += `_Synced from [DevHunt](${process.env.FRONTEND_URL || "https://devhunt.app"})_\n`;
  body += `_Task ID: ${task.Id}_`;

  if (task.Deadline) {
    const deadline = new Date(task.Deadline).toLocaleDateString();
    body += `\n_Deadline: ${deadline}_`;
  }

  if (task.EstimatedHours) {
    body += `\n_Estimated: ${task.EstimatedHours}h_`;
  }

  return body;
}

/**
 * Update task in Core API with GitHub Issue info
 */
async function updateTaskWithIssueInfo(taskId, issueInfo) {
  try {
    const githubLinkUrl = `${CORE_API_URL}/api/tasks/${taskId}/github-link`;
    await axios.patch(githubLinkUrl, issueInfo, {
      headers: mergeInternalApiHeaders(githubLinkUrl),
    });
    logger.debug(`Updated task ${taskId} with GitHub Issue info`);
  } catch (error) {
    logger.error(`Failed to update task ${taskId} with Issue info:`, error.message);
  }
}

/**
 * Check if an event was triggered by our own bot to avoid infinite loops
 * @param {object} webhookPayload - GitHub webhook payload
 * @param {string} botUsername - Our GitHub App/bot username
 */
export function isOwnBotAction(webhookPayload, botUsername) {
  const sender = webhookPayload?.sender?.login;
  if (!sender) return false;

  // Check if sender is our bot or contains [bot] suffix
  return sender === botUsername ||
         sender.endsWith("[bot]") ||
         sender.includes("devhunt");
}
