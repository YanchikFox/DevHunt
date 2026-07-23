using System.Text.Json;
using DevHunt.Infrastructure.Models;

namespace DevHunt.CoreApi.Services.Integrations;

/// <summary>
/// Public integration metadata returned to API clients (no secrets).
/// </summary>
/// <param name="Id">Integration identifier.</param>
/// <param name="ProjectId">Owning project identifier.</param>
/// <param name="ServiceType">Provider type (github, gitlab, jira).</param>
/// <param name="Config">Sanitized provider configuration without tokens or webhook secrets.</param>
/// <param name="IsActive">Whether the integration is enabled.</param>
/// <param name="CreatedAt">Creation timestamp (UTC).</param>
/// <param name="LastSyncAt">Last successful sync timestamp, if any.</param>
/// <param name="UpdatedAt">Last update timestamp (UTC).</param>
/// <param name="HasAccessToken">True when an encrypted access token is stored server-side.</param>
public record IntegrationResponseDto(
    Guid Id,
    Guid ProjectId,
    string ServiceType,
    Dictionary<string, object>? Config,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastSyncAt,
    DateTime UpdatedAt,
    bool HasAccessToken);

/// <summary>
/// Maps <see cref="Integration"/> entities to client-safe response DTOs.
/// </summary>
public static class IntegrationResponseMapper
{
    private static readonly HashSet<string> RedactedConfigKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token",
        "accessToken",
        "webhook_secret",
        "webhookSecret",
        "secret",
        "token",
        "password",
    };

    /// <summary>
    /// Builds a response DTO without encrypted tokens or sensitive config values.
    /// </summary>
    /// <param name="integration">Integration entity loaded from the database.</param>
    /// <returns>Client-safe integration projection.</returns>
    public static IntegrationResponseDto ToDto(Integration integration) =>
        new(
            integration.Id,
            integration.ProjectId,
            integration.ServiceType,
            SanitizeConfig(integration.Config),
            integration.IsActive,
            integration.CreatedAt,
            integration.LastSyncAt,
            integration.UpdatedAt,
            !string.IsNullOrEmpty(integration.AccessTokenEncrypted));

    private static Dictionary<string, object>? SanitizeConfig(Dictionary<string, object>? config)
    {
        if (config == null) return null;

        var sanitized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in config)
        {
            sanitized[key] = RedactedConfigKeys.Contains(key) ? "***" : NormalizeConfigValue(value);
        }

        return sanitized;
    }

    private static object NormalizeConfigValue(object value) =>
        value switch
        {
            JsonElement { ValueKind: JsonValueKind.String } el => el.GetString() ?? string.Empty,
            JsonElement el => el.ToString(),
            _ => value,
        };
}
