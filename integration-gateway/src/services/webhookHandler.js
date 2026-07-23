/**
 * Webhook Handler Service
 * Processes incoming webhooks from GitHub/GitLab and syncs with DevHunt
 */

import axios from "axios";
import { logger } from "../utils/logger.js";
import {
  buildInternalApiHeadersForUrl,
  mergeInternalApiHeaders,
} from "../utils/internalApiAuth.js";

const CORE_API_URL = process.env.CORE_API_URL || "http://core-api:8080";
const CODE_ANALYZER_URL = process.env.CODE_ANALYZER_URL || "http://code-analyzer:8090";
const ANALYZER_API_SECRET = process.env.ANALYZER_API_SECRET || "";
const BOT_USERNAME = process.env.GITHUB_BOT_USERNAME || "devhunt-bot";

/**
 * Process GitHub webhook event
 * @param {string} eventType - GitHub event type (issues, pull_request, push, etc.)
 * @param {object} payload - Webhook payload
 * @returns {Promise<object>} Processing result
 */
export async function processGitHubWebhook(eventType, payload) {
  logger.info(`Processing GitHub webhook: ${eventType}`);

  // Check if this action was triggered by our own bot to avoid infinite loops
  if (isOwnBotAction(payload)) {
    logger.debug("Ignoring webhook triggered by our own bot");
    return { processed: false, reason: "own_bot_action" };
  }

  switch (eventType) {
    case "issues":
      return await handleIssuesEvent(payload);
    case "issue_comment":
      return await handleIssueCommentEvent(payload);
    case "push":
      return await handlePushEvent(payload);
    case "pull_request":
      return await handlePullRequestEvent(payload);
    default:
      logger.debug(`Unhandled GitHub event type: ${eventType}`);
      return { processed: false, reason: "unhandled_event" };
  }
}

/**
 * Handle GitHub issues events (opened, closed, edited, assigned, etc.)
 */
async function handleIssuesEvent(payload) {
  const { action, issue, repository } = payload;
  const repoFullName = repository?.full_name;

  logger.info(`GitHub Issue event: ${action} for #${issue?.number} in ${repoFullName}`);

  // Find integration by repository
  const integration = await findIntegrationByRepository(repoFullName);
  if (!integration) {
    logger.debug(`No integration found for repository ${repoFullName}`);
    return { processed: false, reason: "no_integration" };
  }

  switch (action) {
    case "opened":
      return await handleIssueOpened(issue, integration);
    case "closed":
      return await handleIssueClosed(issue, integration);
    case "reopened":
      return await handleIssueReopened(issue, integration);
    case "edited":
      return await handleIssueEdited(issue, integration);
    case "assigned":
    case "unassigned":
      return await handleIssueAssignmentChanged(issue, integration);
    case "labeled":
    case "unlabeled":
      return await handleIssueLabelsChanged(issue, integration);
    default:
      logger.debug(`Unhandled issue action: ${action}`);
      return { processed: false, reason: "unhandled_action" };
  }
}

/**
 * Handle new issue opened on GitHub - create task in DevHunt
 */
async function handleIssueOpened(issue, integration) {
  // Check if this issue was created by DevHunt (has our metadata)
  if (isDevHuntCreatedIssue(issue)) {
    logger.debug(`Issue #${issue.number} was created by DevHunt, skipping`);
    return { processed: false, reason: "devhunt_created" };
  }

  try {
    // Create task in DevHunt via Core API
    const taskData = {
      projectId: integration.ProjectId,
      title: issue.title,
      description: issue.body || "",
      priority: extractPriorityFromLabels(issue.labels),
      tags: extractTagsFromLabels(issue.labels),
      gitHubIssueId: issue.id,
      gitHubIssueNumber: issue.number,
      gitHubIssueUrl: issue.html_url,
    };

    // I-18: Correct endpoint — was /api/tasks/from-github (doesn't exist)
    const createTaskUrl = `${CORE_API_URL}/api/internal/github-tasks/projects/${integration.ProjectId}/tasks`;
    const response = await axios.post(createTaskUrl, taskData, {
      headers: mergeInternalApiHeaders(createTaskUrl),
    });

    logger.info(`Created task ${response.data?.id} from GitHub Issue #${issue.number}`);
    return { processed: true, taskId: response.data?.id };
  } catch (error) {
    logger.error(`Failed to create task from GitHub Issue #${issue.number}:`, error.message);
    return { processed: false, error: error.message };
  }
}

/**
 * Handle issue closed on GitHub - mark task as completed
 */
async function handleIssueClosed(issue, integration) {
  try {
    // Find task by GitHub Issue ID
    const task = await findTaskByGitHubIssue(integration.ProjectId, issue.id);
    if (!task) {
      logger.debug(`No task found for GitHub Issue #${issue.number}`);
      return { processed: false, reason: "task_not_found" };
    }

    // Update task status to completed
    // I-18: Correct endpoint path
    const completeUrl = `${CORE_API_URL}/api/internal/github-tasks/projects/${integration.ProjectId}/tasks/${task.id}/complete`;
    await axios.patch(completeUrl, {}, {
      headers: mergeInternalApiHeaders(completeUrl),
    });

    logger.info(`Marked task ${task.id} as completed from GitHub Issue #${issue.number}`);
    return { processed: true, taskId: task.id };
  } catch (error) {
    logger.error(`Failed to complete task from GitHub Issue #${issue.number}:`, error.message);
    return { processed: false, error: error.message };
  }
}

/**
 * Handle issue reopened on GitHub - reopen task
 */
async function handleIssueReopened(issue, integration) {
  try {
    const task = await findTaskByGitHubIssue(integration.ProjectId, issue.id);
    if (!task) {
      return { processed: false, reason: "task_not_found" };
    }

    // I-18: Correct endpoint path
    const reopenUrl = `${CORE_API_URL}/api/internal/github-tasks/projects/${integration.ProjectId}/tasks/${task.id}/reopen`;
    await axios.patch(reopenUrl, {}, {
      headers: mergeInternalApiHeaders(reopenUrl),
    });

    logger.info(`Reopened task ${task.id} from GitHub Issue #${issue.number}`);
    return { processed: true, taskId: task.id };
  } catch (error) {
    logger.error(`Failed to reopen task from GitHub Issue #${issue.number}:`, error.message);
    return { processed: false, error: error.message };
  }
}

/**
 * Handle issue edited on GitHub - update task
 */
async function handleIssueEdited(issue, integration) {
  try {
    const task = await findTaskByGitHubIssue(integration.ProjectId, issue.id);
    if (!task) {
      return { processed: false, reason: "task_not_found" };
    }

    // I-18: Correct endpoint path
    const contentUrl = `${CORE_API_URL}/api/internal/github-tasks/projects/${integration.ProjectId}/tasks/${task.id}/content`;
    await axios.patch(
      contentUrl,
      {
        title: issue.title,
        description: issue.body || "",
      },
      { headers: mergeInternalApiHeaders(contentUrl) },
    );

    logger.info(`Updated task ${task.id} from GitHub Issue #${issue.number}`);
    return { processed: true, taskId: task.id };
  } catch (error) {
    logger.error(`Failed to update task from GitHub Issue #${issue.number}:`, error.message);
    return { processed: false, error: error.message };
  }
}

/**
 * Handle issue assignment changes
 */
async function handleIssueAssignmentChanged(issue, integration) {
  // TODO: Map GitHub username to DevHunt user and update task assignment
  logger.debug(`Issue #${issue.number} assignment changed, sync not implemented yet`);
  return { processed: false, reason: "not_implemented" };
}

/**
 * Handle issue labels changes
 */
async function handleIssueLabelsChanged(issue, integration) {
  try {
    const task = await findTaskByGitHubIssue(integration.ProjectId, issue.id);
    if (!task) {
      return { processed: false, reason: "task_not_found" };
    }

    const priority = extractPriorityFromLabels(issue.labels);
    const tags = extractTagsFromLabels(issue.labels);

    // I-18: Correct endpoint path
    const labelsUrl = `${CORE_API_URL}/api/internal/github-tasks/projects/${integration.ProjectId}/tasks/${task.id}/labels`;
    await axios.patch(labelsUrl, { priority, tags }, {
      headers: mergeInternalApiHeaders(labelsUrl),
    });

    logger.info(`Updated task ${task.id} labels from GitHub Issue #${issue.number}`);
    return { processed: true, taskId: task.id };
  } catch (error) {
    logger.error(`Failed to update task labels:`, error.message);
    return { processed: false, error: error.message };
  }
}

/**
 * Handle issue comment events (for future AI integration)
 */
async function handleIssueCommentEvent(payload) {
  // TODO: Could trigger AI assistant to respond or update task
  logger.debug("Issue comment event received, not processed");
  return { processed: false, reason: "not_implemented" };
}

/**
 * Handle push events - link commits to tasks + trigger code analysis
 */
async function handlePushEvent(payload) {
  const { commits, repository, ref, after: headSha } = payload;
  const repoFullName = repository?.full_name;

  // 1. Link commits to tasks (existing logic)
  for (const commit of commits || []) {
    const taskRefs = extractTaskReferences(commit.message);
    if (taskRefs.length > 0) {
      logger.info(`Found task references in commit ${commit.id}: ${taskRefs.join(", ")}`);
      // TODO: Link commits to tasks, update progress
    }
  }

  // 2. Trigger code analysis on push to default/main branches
  const branch = ref?.replace("refs/heads/", "");
  if (!branch) {
    return { processed: true, commits: commits?.length || 0 };
  }

  const integration = await findIntegrationByRepository(repoFullName);
  if (!integration || !integration.isActive) {
    logger.debug(`No active integration for ${repoFullName}, skipping analysis`);
    return { processed: true, commits: commits?.length || 0 };
  }

  // Only analyze main/master/develop branches (avoid spam from feature branches)
  const analyzableBranches = ["main", "master", "develop"];
  const config = typeof integration.configJson === "string"
    ? JSON.parse(integration.configJson)
    : integration.configJson || integration.config || {};
  const customBranches = config.analysisBranches || [];
  const targetBranches = [...analyzableBranches, ...customBranches];

  if (!targetBranches.includes(branch)) {
    logger.debug(`Push to ${branch} — not in analysis targets, skipping`);
    return { processed: true, commits: commits?.length || 0, analysisSkipped: true };
  }

  // Fire code analysis asynchronously (don't block webhook response)
  triggerCodeAnalysis(integration, repoFullName, branch, headSha).catch((err) => {
    logger.error(`Code analysis failed for ${repoFullName}@${branch}:`, err.message);
  });

  return { processed: true, commits: commits?.length || 0, analysisTriggered: true };
}

/**
 * Trigger code analysis via the DevHunt Analyzer service
 * @param {object} integration - Integration object with id, projectId
 * @param {string} repository - Repository "owner/repo"
 * @param {string} branch - Branch name
 * @param {string} commitSha - Commit SHA (optional)
 * @param {string} [directAccessToken] - Pre-decrypted access token (skips decrypt call)
 */
export async function triggerCodeAnalysis(integration, repository, branch, commitSha, directAccessToken) {
  const repoUrl = `https://github.com/${repository}`;

  // Get the access token for private repo cloning
  let accessToken = directAccessToken || null;
  if (!accessToken && integration.accessTokenEncrypted) {
    try {
      const decryptUrl = `${CORE_API_URL}/api/integrations/${integration.id}/decrypt-token`;
      const tokenResp = await axios.get(decryptUrl, {
        headers: buildInternalApiHeadersForUrl(decryptUrl),
      });
      accessToken = tokenResp.data?.accessToken;
    } catch (err) {
      logger.error(`Failed to get access token for integration ${integration.id}:`, err.message);
      return;
    }
  }

  logger.info(`Triggering code analysis for ${repository}@${branch} (${commitSha?.slice(0, 7)})`);

  // Read user-configured exclude patterns from integration config
  const integrationConfig = typeof integration.configJson === "string"
    ? JSON.parse(integration.configJson)
    : integration.configJson || integration.config || {};
  const excludePatterns = integrationConfig.excludePatterns || [];

  // Call the code analyzer
  const analyzerPayload = {
    repository_url: repoUrl,
    branch,
    commit_sha: commitSha,
    ...(accessToken && { access_token: accessToken }),
    ...(excludePatterns.length > 0 && { exclude_patterns: excludePatterns }),
  };

  const headers = { "Content-Type": "application/json" };
  if (ANALYZER_API_SECRET) {
    headers["Authorization"] = `Bearer ${ANALYZER_API_SECRET}`;
  }

  const analysisResp = await axios.post(
    `${CODE_ANALYZER_URL}/analyze`,
    analyzerPayload,
    { headers, timeout: 300_000 }, // 5 min timeout for large repos
  );

  const analysisResult = analysisResp.data;
  logger.info(
    `Analysis complete for ${repository}: ${analysisResult.total_issues} issues found`,
  );

  // Save the analysis result to Core API
  const resultsUrl = `${CORE_API_URL}/api/internal/code-analysis/results`;
  await axios.post(
    resultsUrl,
    {
      integrationId: integration.id,
      projectId: integration.projectId,
      repository,
      branch,
      commitSha,
      totalIssues: analysisResult.total_issues || 0,
      totalFiles: analysisResult.files_analyzed || analysisResult.total_files || 0,
      analysisTimeMs: analysisResult.duration_seconds
        ? Math.round(analysisResult.duration_seconds * 1000)
        : (analysisResult.analysis_time_ms || 0),
      severityCounts: analysisResult.issues_by_severity || analysisResult.severity_counts || {},
      categoryCounts: analysisResult.issues_by_category || analysisResult.category_counts || {},
      issues: analysisResult.issues || [],
    },
    {
      headers: mergeInternalApiHeaders(resultsUrl, {
        "Content-Type": "application/json",
      }),
      maxBodyLength: 10 * 1024 * 1024, // 10 MB
    },
  );

  logger.info(`Analysis result saved for ${repository}@${branch}`);
}

/**
 * Handle pull request events
 */
async function handlePullRequestEvent(payload) {
  // TODO: Link PRs to tasks, update status when merged
  logger.debug("Pull request event received, not processed");
  return { processed: false, reason: "not_implemented" };
}

// ============================================================================
// Helper functions
// ============================================================================

/**
 * Check if the webhook was triggered by our own bot
 */
function isOwnBotAction(payload) {
  const sender = payload?.sender?.login;
  if (!sender) return false;

  // I-15: Use exact match instead of includes() — "iLoveDevHunt" would be a false positive
  const lowerSender = sender.toLowerCase();
  return (
    sender === BOT_USERNAME ||
    sender.endsWith("[bot]") ||
    lowerSender === "devhunt" ||
    lowerSender === "devhunt-bot"
  );
}

/**
 * Check if an issue was created by DevHunt (has our metadata in body)
 */
function isDevHuntCreatedIssue(issue) {
  const body = issue?.body || "";
  return body.includes("_Synced from [DevHunt]") || body.includes("_Task ID:");
}

/**
 * Find integration by repository name
 */
async function findIntegrationByRepository(repoFullName) {
  try {
    const byRepoUrl = `${CORE_API_URL}/api/integrations/by-repository`;
    const response = await axios.get(byRepoUrl, {
      params: { repository: repoFullName },
      headers: buildInternalApiHeadersForUrl(byRepoUrl),
    });
    return response.data;
  } catch (error) {
    if (error.response?.status !== 404) {
      logger.error(`Failed to find integration for ${repoFullName}:`, error.message);
    }
    return null;
  }
}

/**
 * Find task by GitHub Issue ID
 */
async function findTaskByGitHubIssue(projectId, gitHubIssueId) {
  try {
    // I-18: Correct endpoint path — was /api/tasks/by-github-issue (doesn't exist)
    const byIssueUrl = `${CORE_API_URL}/api/internal/github-tasks/projects/${projectId}/tasks/by-issue/${gitHubIssueId}`;
    const response = await axios.get(byIssueUrl, {
      headers: buildInternalApiHeadersForUrl(byIssueUrl),
    });
    return response.data;
  } catch (error) {
    if (error.response?.status !== 404) {
      logger.error(`Failed to find task by GitHub Issue ${gitHubIssueId}:`, error.message);
    }
    return null;
  }
}

/**
 * Extract priority from GitHub labels
 */
function extractPriorityFromLabels(labels) {
  const priorityLabels = {
    "priority: critical": "urgent",
    "priority: high": "high",
    "priority: medium": "medium",
    "priority: low": "low",
    "urgent": "urgent",
    "high priority": "high",
    "low priority": "low",
  };

  for (const label of labels || []) {
    const name = label.name?.toLowerCase();
    if (priorityLabels[name]) {
      return priorityLabels[name];
    }
  }

  return "medium";
}

/**
 * Extract tags from GitHub labels (excluding priority labels)
 */
function extractTagsFromLabels(labels) {
  const priorityPrefixes = ["priority:", "urgent", "high priority", "low priority"];

  return (labels || [])
    .map((l) => l.name)
    .filter((name) => !priorityPrefixes.some((p) => name.toLowerCase().startsWith(p)))
    .join(",");
}

/**
 * Extract task references from commit message
 */
function extractTaskReferences(message) {
  const refs = [];

  // Match patterns like "fixes #123", "closes #456", "task:uuid"
  const patterns = [
    /(?:fix(?:es)?|close[sd]?|resolve[sd]?)\s+#(\d+)/gi,
    /task[:\s]([a-f0-9-]{36})/gi,
  ];

  for (const pattern of patterns) {
    let match;
    while ((match = pattern.exec(message)) !== null) {
      refs.push(match[1]);
    }
  }

  return refs;
}
