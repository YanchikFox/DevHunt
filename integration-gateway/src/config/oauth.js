/**
 * OAuth Configuration
 * Настройки для различных OAuth провайдеров
 */

export function getOAuthConfig(provider) {
  const configs = {
    github: {
      clientId: process.env.GITHUB_CLIENT_ID,
      clientSecret: process.env.GITHUB_CLIENT_SECRET,
      authUrl: "https://github.com/login/oauth/authorize",
      tokenUrl: "https://github.com/login/oauth/access_token",
      userInfoUrl: "https://api.github.com/user",
      scope: "read:user user:email repo",
      responseType: null,
    },
    gitlab: {
      clientId: process.env.GITLAB_CLIENT_ID,
      clientSecret: process.env.GITLAB_CLIENT_SECRET,
      authUrl:
        process.env.GITLAB_AUTH_URL || "https://gitlab.com/oauth/authorize",
      tokenUrl:
        process.env.GITLAB_TOKEN_URL || "https://gitlab.com/oauth/token",
      userInfoUrl:
        process.env.GITLAB_USER_INFO_URL || "https://gitlab.com/api/v4/user",
      scope: "read_user read_repository api",
      responseType: "code",
    },
  };

  const config = configs[provider.toLowerCase()];

  if (!config) {
    return null;
  }

  // Validate required fields
  if (!config.clientId || !config.clientSecret) {
    console.warn(`OAuth credentials not configured for ${provider}`);
    return null;
  }

  return config;
}
