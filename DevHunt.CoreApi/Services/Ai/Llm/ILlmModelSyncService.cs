namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Counts from synchronizing provider-reported models into the local <c>LlmModels</c> table.
/// </summary>
/// <param name="Provider">Provider id that was synced.</param>
/// <param name="Seen">Models returned by the provider.</param>
/// <param name="Created">New registry rows inserted.</param>
/// <param name="Updated">Existing rows updated.</param>
/// <param name="MissingPricing">Models seen without price metadata.</param>
public sealed record LlmModelSyncResult(
    string Provider,
    int Seen,
    int Created,
    int Updated,
    int MissingPricing);

/// <summary>
/// Pulls model metadata from a provider using the caller's stored API key
/// and merges it into the shared model registry.
/// </summary>
public interface ILlmModelSyncService
{
    /// <summary>
    /// Lists models from the provider with the user's decrypted key and upserts registry rows.
    /// </summary>
    /// <param name="userId">Owner of the BYOK key.</param>
    /// <param name="providerId">Provider to sync.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LlmModelSyncResult> SyncFromUserKeyAsync(Guid userId, string providerId, CancellationToken ct);
}
