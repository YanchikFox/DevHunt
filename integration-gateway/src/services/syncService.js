/**
 * Sync Service
 * Data synchronization between DevHunt and external services (GitHub, GitLab)
 */

import axios from "axios";
import { logger } from "../utils/logger.js";
import { mergeInternalApiHeaders } from "../utils/internalApiAuth.js";

const CORE_API_URL = process.env.CORE_API_URL || "http://core-api:8080";

/**
 * Synchronize data from an external service
 * @param {string} integrationId - Integration ID in DevHunt
 * @param {string} serviceType - Service type (github, gitlab)
 * @param {object} config - Integration configuration (from Integration.Config)
 * @param {string} accessToken - Access token for external API
 * @returns {Promise<object>} Synchronization result
 */
export async function syncIntegrationData(
  integrationId,
  serviceType,
  config,
  accessToken,
) {
  try {
    logger.info(
      `Starting sync for integration ${integrationId}, service ${serviceType}`,
    );

    const serviceTypeLower = serviceType.toLowerCase();
    let syncResult = null;

    switch (serviceTypeLower) {
      case "github":
        syncResult = await syncGitHubData(integrationId, config, accessToken);
        break;
      case "gitlab":
        syncResult = await syncGitLabData(integrationId, config, accessToken);
        break;
      default:
        throw new Error(`Unsupported service type: ${serviceType}`);
    }

    // Обновить LastSyncAt в Core API
    try {
      const syncStatusUrl = `${CORE_API_URL}/api/integrations/${integrationId}/sync-status`;
      await axios.put(
        syncStatusUrl,
        {
          lastSyncAt: new Date().toISOString(),
          syncResult,
        },
        { headers: mergeInternalApiHeaders(syncStatusUrl) },
      );
    } catch (error) {
      logger.warn(`Failed to update sync status in Core API: ${error.message}`);
      // Не критично, продолжаем
    }

    logger.info(`Sync completed for integration ${integrationId}`);
    return syncResult;
  } catch (error) {
    logger.error(`Sync failed for integration ${integrationId}:`, error);
    throw error;
  }
}

/**
 * Data synchronization из GitHub
 */
async function syncGitHubData(integrationId, config, accessToken) {
  const repository = config?.repository || config?.Repository; // format: "owner/repo"

  if (!repository) {
    throw new Error("Repository not configured in integration config");
  }

  const [owner, repo] = repository.split("/");
  if (!owner || !repo) {
    throw new Error(
      `Invalid repository format: ${repository}. Expected: owner/repo`,
    );
  }

  const results = {
    branches: [],
    commits: [],
    issues: [],
    pullRequests: [],
    contributors: [],
  };

  try {
    // 1. Синхронизировать branches
    const branchesResponse = await axios.get(
      `https://api.github.com/repos/${owner}/${repo}/branches`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
      },
    );
    results.branches = branchesResponse.data.map((b) => ({
      name: b.name,
      sha: b.commit.sha,
      protected: b.protected,
    }));

    // 2. Синхронизировать последние commits (максимум 30)
    const commitsResponse = await axios.get(
      `https://api.github.com/repos/${owner}/${repo}/commits`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
        params: {
          per_page: 30,
          page: 1,
        },
      },
    );
    results.commits = commitsResponse.data.map((c) => ({
      sha: c.sha,
      message: c.commit.message,
      author: c.commit.author.name,
      date: c.commit.author.date,
      url: c.html_url,
    }));

    // 3. Синхронизировать issues (open issues)
    const issuesResponse = await axios.get(
      `https://api.github.com/repos/${owner}/${repo}/issues`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
        params: {
          state: "open",
          per_page: 50,
          page: 1,
        },
      },
    );
    results.issues = issuesResponse.data
      .filter((i) => !i.pull_request) // Только issues, не PR
      .map((i) => ({
        number: i.number,
        title: i.title,
        body: i.body,
        state: i.state,
        labels: i.labels.map((l) => l.name),
        created_at: i.created_at,
        updated_at: i.updated_at,
        url: i.html_url,
      }));

    // 4. Синхронизировать pull requests (open)
    const prsResponse = await axios.get(
      `https://api.github.com/repos/${owner}/${repo}/pulls`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
        params: {
          state: "open",
          per_page: 20,
          page: 1,
        },
      },
    );
    results.pullRequests = prsResponse.data.map((pr) => ({
      number: pr.number,
      title: pr.title,
      state: pr.state,
      created_at: pr.created_at,
      updated_at: pr.updated_at,
      url: pr.html_url,
      head: pr.head.ref,
      base: pr.base.ref,
    }));

    // 5. Синхронизировать contributors
    const contributorsResponse = await axios.get(
      `https://api.github.com/repos/${owner}/${repo}/contributors`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
          Accept: "application/vnd.github.v3+json",
        },
        params: {
          per_page: 30,
          page: 1,
        },
      },
    );
    results.contributors = contributorsResponse.data.map((c) => ({
      login: c.login,
      contributions: c.contributions,
      avatar_url: c.avatar_url,
    }));

    logger.info(
      `GitHub sync completed: ${results.branches.length} branches, ${results.commits.length} commits, ${results.issues.length} issues, ${results.pullRequests.length} PRs`,
    );

    return {
      success: true,
      serviceType: "github",
      repository,
      syncedAt: new Date().toISOString(),
      data: results,
      summary: {
        branches: results.branches.length,
        commits: results.commits.length,
        issues: results.issues.length,
        pullRequests: results.pullRequests.length,
        contributors: results.contributors.length,
      },
    };
  } catch (error) {
    logger.error("GitHub sync error:", error);
    if (error.response) {
      throw new Error(
        `GitHub API error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}

// I-04 (SSRF): Validate GitLab URL to prevent internal network access
const ALLOWED_GITLAB_HOST = /^https:\/\/(gitlab\.com|[\w.-]+\.gitlab\.com)(\/|$)/;
const PRIVATE_IP = /^(localhost|127\.|10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.)/;

function validateGitLabUrl(url) {
  if (!url) return "https://gitlab.com/api/v4";
  if (!ALLOWED_GITLAB_HOST.test(url)) {
    throw new Error(`Invalid GitLab URL: only public GitLab hosts are allowed (got: ${url})`);
  }
  const parsed = new URL(url);
  if (PRIVATE_IP.test(parsed.hostname)) {
    throw new Error(`GitLab URL points to a private network address: ${parsed.hostname}`);
  }
  return url;
}

/**
 * Data synchronization из GitLab
 */
async function syncGitLabData(integrationId, config, accessToken) {
  const projectId = config?.projectId; // GitLab project ID
  const gitlabUrl = validateGitLabUrl(config?.gitlabUrl); // I-04: validate before use

  if (!projectId) {
    throw new Error("GitLab project ID not configured in integration config");
  }

  const results = {
    branches: [],
    commits: [],
    issues: [],
    mergeRequests: [],
    contributors: [],
  };

  try {
    // 1. Синхронизировать branches
    const branchesResponse = await axios.get(
      `${gitlabUrl}/projects/${projectId}/repository/branches`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      },
    );
    results.branches = branchesResponse.data.map((b) => ({
      name: b.name,
      commit: b.commit.id,
      protected: b.protected,
    }));

    // 2. Синхронизировать последние commits
    const commitsResponse = await axios.get(
      `${gitlabUrl}/projects/${projectId}/repository/commits`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
        params: {
          per_page: 30,
          page: 1,
        },
      },
    );
    results.commits = commitsResponse.data.map((c) => ({
      id: c.id,
      message: c.message,
      author: c.author_name,
      date: c.created_at,
      url: c.web_url,
    }));

    // 3. Синхронизировать issues (opened)
    const issuesResponse = await axios.get(
      `${gitlabUrl}/projects/${projectId}/issues`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
        params: {
          state: "opened",
          per_page: 50,
          page: 1,
        },
      },
    );
    results.issues = issuesResponse.data.map((i) => ({
      iid: i.iid,
      title: i.title,
      description: i.description,
      state: i.state,
      labels: i.labels,
      created_at: i.created_at,
      updated_at: i.updated_at,
      web_url: i.web_url,
    }));

    // 4. Синхронизировать merge requests (opened)
    const mrsResponse = await axios.get(
      `${gitlabUrl}/projects/${projectId}/merge_requests`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
        params: {
          state: "opened",
          per_page: 20,
          page: 1,
        },
      },
    );
    results.mergeRequests = mrsResponse.data.map((mr) => ({
      iid: mr.iid,
      title: mr.title,
      state: mr.state,
      created_at: mr.created_at,
      updated_at: mr.updated_at,
      web_url: mr.web_url,
      source_branch: mr.source_branch,
      target_branch: mr.target_branch,
    }));

    // 5. Синхронизировать contributors (members)
    const membersResponse = await axios.get(
      `${gitlabUrl}/projects/${projectId}/members`,
      {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
        params: {
          per_page: 30,
          page: 1,
        },
      },
    );
    results.contributors = membersResponse.data.map((m) => ({
      username: m.username,
      name: m.name,
      access_level: m.access_level,
    }));

    logger.info(
      `GitLab sync completed: ${results.branches.length} branches, ${results.commits.length} commits, ${results.issues.length} issues, ${results.mergeRequests.length} MRs`,
    );

    return {
      success: true,
      serviceType: "gitlab",
      projectId,
      syncedAt: new Date().toISOString(),
      data: results,
      summary: {
        branches: results.branches.length,
        commits: results.commits.length,
        issues: results.issues.length,
        mergeRequests: results.mergeRequests.length,
        contributors: results.contributors.length,
      },
    };
  } catch (error) {
    logger.error("GitLab sync error:", error);
    if (error.response) {
      throw new Error(
        `GitLab API error: ${error.response.status} - ${error.response.data?.message || error.message}`,
      );
    }
    throw error;
  }
}
