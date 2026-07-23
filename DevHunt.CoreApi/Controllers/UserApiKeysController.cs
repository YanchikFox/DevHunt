using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Services.Ai.Llm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// BYOK endpoints — users manage their own LLM API keys here. Plaintext keys
/// only ever travel inbound; outbound responses always carry masked
/// <see cref="UserApiKeyView"/>.
/// </summary>
[ApiController]
[Route("api/me/api-keys")]
[Authorize]
public class UserApiKeysController : ControllerBase
{
    private readonly IUserApiKeyService _service;
    private readonly IAuditService _audit;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserApiKeysController"/> class.
    /// </summary>
    /// <param name="service">Service that stores, validates, masks, and deletes user-owned provider keys.</param>
    /// <param name="audit">Audit service used to record non-sensitive BYOK lifecycle events.</param>
    public UserApiKeysController(IUserApiKeyService service, IAuditService audit)
    {
        _service = service;
        _audit = audit;
    }

    /// <summary>
    /// Lists the current user's saved provider keys using masked key views only.
    /// </summary>
    /// <param name="ct">Cancellation token for the key lookup.</param>
    /// <returns>Masked key metadata, or 401 when the user ID claim is absent.</returns>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var keys = await _service.ListAsync(userId.Value, ct);
        return Ok(keys);
    }

    /// <summary>
    /// Creates or replaces one of the current user's provider API keys after model validation.
    /// </summary>
    /// <param name="request">Provider, API key, and optional model preferences to validate and store.</param>
    /// <param name="ct">Cancellation token for validation, persistence, and audit logging.</param>
    /// <returns>
    /// The masked key view for created or replaced keys; returns 401 without a user claim, validation errors for invalid models,
    /// 400 for unsupported providers or invalid keys, and 500 for unrecognized service statuses.
    /// </returns>
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertUserApiKeyRequest request, CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var result = await _service.UpsertAsync(userId.Value, request, ct);

        switch (result.Status)
        {
            case UpsertUserApiKeyStatus.Created:
            case UpsertUserApiKeyStatus.Replaced:
                // Audit only the non-sensitive view — KeyHint is safe, ApiKey itself
                // never reaches this path. Keeps a paper trail for "who/when added".
                await _audit.LogActionAsync(
                    userId,
                    result.Status == UpsertUserApiKeyStatus.Created ? "byok.key.created" : "byok.key.replaced",
                    nameof(DevHunt.Infrastructure.Models.UserApiKey),
                    result.Key?.Id,
                    $"provider={result.Key?.Provider} hint={result.Key?.KeyHint}",
                    null,
                    ct);
                return Ok(result.Key);

            case UpsertUserApiKeyStatus.InvalidKey:
                return BadRequest(new { error = result.ErrorMessage ?? "Invalid key." });

            case UpsertUserApiKeyStatus.UnsupportedProvider:
                return BadRequest(new { error = result.ErrorMessage ?? "Unsupported provider." });

            default:
                return StatusCode(500);
        }
    }

    /// <summary>
    /// Deletes one of the current user's saved provider API keys.
    /// </summary>
    /// <param name="id">API key record to remove when owned by the current user.</param>
    /// <param name="ct">Cancellation token for deletion and audit logging.</param>
    /// <returns>204 when deleted, 401 without a user claim, or 404 when the key does not belong to the user.</returns>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = SecurityHelpers.GetUserId(User);
        if (!userId.HasValue) return Unauthorized();

        var deleted = await _service.DeleteAsync(userId.Value, id, ct);
        if (!deleted) return NotFound();

        await _audit.LogActionAsync(
            userId,
            "byok.key.deleted",
            nameof(DevHunt.Infrastructure.Models.UserApiKey),
            id,
            null,
            null,
            ct);

        return NoContent();
    }
}
