/**
 * OAuth Routes
 * GitHub, GitLab OAuth2 flow
 */

import express from "express";
import axios from "axios";
import { logger } from "../utils/logger.js";
import { getOAuthConfig } from "../config/oauth.js";
import { validateRedirectUri } from "../utils/urlValidation.js";

const router = express.Router();

const OAUTH_REDIRECT_BASE =
  process.env.OAUTH_REDIRECT_BASE || "http://localhost:7002/api/integrations";

/**
 * Get OAuth authorization URL (POST для совместимости с Core API)
 */
router.post("/authorize", (req, res) => {
  try {
    const {
      ServiceType,
      serviceType,
      ProjectId,
      projectId,
      RedirectUri,
      redirectUri,
    } = req.body;
    const provider = (ServiceType || serviceType || "").toLowerCase();
    const projectIdValue = ProjectId || projectId;
    const redirectUriValue = RedirectUri || redirectUri;

    if (!provider) {
      return res.status(400).json({ error: "ServiceType is required" });
    }

    // Генерируем state на основе projectId
    const state = projectIdValue
      ? `${projectIdValue}:${Date.now()}`
      : `state:${Date.now()}`;

    const config = getOAuthConfig(provider);
    if (!config) {
      return res
        .status(400)
        .json({ error: `Unsupported provider: ${provider}` });
    }

    const redirectUriFinal =
      redirectUriValue || `${OAUTH_REDIRECT_BASE}/oauth/${provider}/callback`;

    // SEC-014: Validate user-supplied redirect URI to prevent open redirect / token theft
    if (redirectUriValue) {
      const allowedOrigins = (process.env.ALLOWED_REDIRECT_ORIGINS || OAUTH_REDIRECT_BASE)
        .split(",")
        .map((o) => o.trim())
        .filter(Boolean)
        .map((o) => { try { return new URL(o).origin; } catch { return o; } });
      const uriCheck = validateRedirectUri(redirectUriValue, allowedOrigins);
      if (!uriCheck.valid) {
        return res.status(400).json({ error: `Invalid RedirectUri: ${uriCheck.reason}` });
      }
    }

    const params = new URLSearchParams({
      client_id: config.clientId,
      redirect_uri: redirectUriFinal,
      state,
      scope: config.scope,
      ...(config.responseType && { response_type: config.responseType }),
    });

    const authUrl = `${config.authUrl}?${params.toString()}`;

    logger.info(`OAuth URL generated for ${provider}`, {
      state,
      projectId: projectIdValue,
    });

    return res.json({
      oauthUrl: authUrl,
      provider,
      state,
    });
  } catch (error) {
    logger.error("Error generating OAuth URL:", error);
    return res
      .status(500)
      .json({ error: error.message || "Internal server error" });
  }
});

/**
 * Get OAuth authorization URL (GET for backward compatibility)
 */
router.get("/:provider/url", (req, res) => {
  try {
    const { provider } = req.params;
    const { state, redirect_uri } = req.query;

    if (!state) {
      return res.status(400).json({ error: "State parameter is required" });
    }

    const config = getOAuthConfig(provider);
    if (!config) {
      return res
        .status(400)
        .json({ error: `Unsupported provider: ${provider}` });
    }

    const redirectUri =
      redirect_uri || `${OAUTH_REDIRECT_BASE}/oauth/${provider}/callback`;

    // SEC-014: Validate user-supplied redirect URI
    if (redirect_uri) {
      const allowedOrigins = (process.env.ALLOWED_REDIRECT_ORIGINS || OAUTH_REDIRECT_BASE)
        .split(",")
        .map((o) => o.trim())
        .filter(Boolean)
        .map((o) => { try { return new URL(o).origin; } catch { return o; } });
      const uriCheck = validateRedirectUri(redirect_uri, allowedOrigins);
      if (!uriCheck.valid) {
        return res.status(400).json({ error: `Invalid redirect_uri: ${uriCheck.reason}` });
      }
    }

    const params = new URLSearchParams({
      client_id: config.clientId,
      redirect_uri: redirectUri,
      state,
      scope: config.scope,
      ...(config.responseType && { response_type: config.responseType }),
    });

    const authUrl = `${config.authUrl}?${params.toString()}`;

    logger.info(`OAuth URL generated for ${provider}`, { state });

    return res.json({
      authUrl,
      provider,
      state,
    });
  } catch (error) {
    logger.error("Error generating OAuth URL:", error);
    return res
      .status(500)
      .json({ error: error.message || "Internal server error" });
  }
});

/**
 * OAuth callback - exchange code for token (POST для совместимости с Core API)
 */
router.post("/callback", async (req, res) => {
  try {
    const { ServiceType, serviceType, Code, code, State, state } = req.body;
    const provider = (ServiceType || serviceType || "").toLowerCase();
    const codeValue = Code || code;
    const stateValue = State || state;

    if (!provider) {
      return res.status(400).json({ error: "ServiceType is required" });
    }

    if (!codeValue) {
      return res.status(400).json({ error: "Code is required" });
    }

    const config = getOAuthConfig(provider);
    if (!config) {
      return res
        .status(400)
        .json({ error: `Unsupported provider: ${provider}` });
    }

    // Exchange code for access token
    const tokenResponse = await axios.post(
      config.tokenUrl,
      {
        client_id: config.clientId,
        client_secret: config.clientSecret,
        code: codeValue,
        redirect_uri: `${OAUTH_REDIRECT_BASE}/oauth/${provider}/callback`,
        state: stateValue,
      },
      {
        headers: {
          Accept: "application/json",
        },
      },
    );

    const accessToken =
      tokenResponse.data.access_token || tokenResponse.data.accessToken;

    if (!accessToken) {
      logger.error(
        `No access token in response from ${provider}`,
        tokenResponse.data,
      );
      return res.status(500).json({ error: "Failed to obtain access token" });
    }

    // Get user info
    let userInfo = null;
    try {
      const userResponse = await axios.get(config.userInfoUrl, {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      });
      userInfo = userResponse.data;
    } catch (err) {
      logger.warn(`Failed to fetch user info from ${provider}:`, err);
      userInfo = null;
    }

    logger.info(`OAuth callback successful for ${provider}`);

    return res.json({
      success: true,
      provider,
      accessToken,
      refreshToken: tokenResponse.data.refresh_token || null,
      expiresIn: tokenResponse.data.expires_in || null,
      userInfo,
      state: stateValue,
    });
  } catch (error) {
    logger.error("OAuth callback error:", error);
    if (error.response) {
      logger.error("OAuth provider response:", error.response.data);
    }
    return res.status(500).json({
      error: error.message || "Internal server error",
      details: error.response?.data || null,
    });
  }
});

/**
 * OAuth callback - exchange code for token (GET for backward compatibility)
 */
router.get("/:provider/callback", async (req, res) => {
  try {
    const { provider } = req.params;
    const { code, state, error } = req.query;

    if (error) {
      logger.warn(`OAuth error from ${provider}:`, error);
      return res.status(400).json({ error: `OAuth error: ${error}` });
    }

    if (!code) {
      return res.status(400).json({ error: "Authorization code is required" });
    }

    const config = getOAuthConfig(provider);
    if (!config) {
      return res
        .status(400)
        .json({ error: `Unsupported provider: ${provider}` });
    }

    // Exchange code for access token
    const tokenResponse = await axios.post(
      config.tokenUrl,
      {
        client_id: config.clientId,
        client_secret: config.clientSecret,
        code,
        redirect_uri: `${OAUTH_REDIRECT_BASE}/oauth/${provider}/callback`,
        state,
      },
      {
        headers: {
          Accept: "application/json",
        },
      },
    );

    const accessToken =
      tokenResponse.data.access_token || tokenResponse.data.accessToken;

    if (!accessToken) {
      logger.error(
        `No access token in response from ${provider}`,
        tokenResponse.data,
      );
      return res.status(500).json({ error: "Failed to obtain access token" });
    }

    // Get user info
    let userInfo = null;
    try {
      const userResponse = await axios.get(config.userInfoUrl, {
        headers: {
          Authorization: `Bearer ${accessToken}`,
        },
      });
      userInfo = userResponse.data;
    } catch (err) {
      logger.warn(`Failed to fetch user info from ${provider}:`, err);
      userInfo = null;
    }

    logger.info(`OAuth callback successful for ${provider}`);

    return res.json({
      success: true,
      provider,
      accessToken,
      refreshToken: tokenResponse.data.refresh_token || null,
      expiresIn: tokenResponse.data.expires_in || null,
      userInfo,
      state,
    });
  } catch (error) {
    logger.error(`OAuth callback error for ${req.params.provider}:`, error);
    if (error.response) {
      logger.error("OAuth provider response:", error.response.data);
    }
    return res.status(500).json({
      error: error.message || "Internal server error",
      details: error.response?.data || null,
    });
  }
});

export { router as oauthRouter };
