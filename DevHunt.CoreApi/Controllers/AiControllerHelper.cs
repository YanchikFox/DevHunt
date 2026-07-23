using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using DevHunt.CoreApi.Services.Ai;
using DevHunt.CoreApi.Security;

namespace DevHunt.CoreApi.Controllers;

/// <summary>
/// Centralizes identity extraction and HTTP result mapping for AI planning controllers.
/// </summary>
public static class AiControllerHelper
{
    /// <summary>
    /// Executes an AI planning action and maps the planning result to an HTTP response.
    /// </summary>
    /// <typeparam name="T">The expected AI planning payload type.</typeparam>
    /// <param name="user">Principal that supplies the required user ID and admin flag.</param>
    /// <param name="action">Service operation to run with the current user ID and admin status.</param>
    /// <returns>
    /// <see cref="OkObjectResult"/> when the service succeeds, <see cref="UnauthorizedResult"/> when the user ID claim is missing,
    /// or an error result from <see cref="MapErrorResult{T}"/>.
    /// </returns>
    public static async Task<IActionResult> ExecuteAiAction<T>(
        ClaimsPrincipal user,
        Func<Guid, bool, Task<AiPlanningResult<T>>> action)
    {
        // We use the helper directly, assuming the controller called this means they are authorized
        // but we still need the ID.
        var userId = SecurityHelpers.GetUserId(user);
        if (!userId.HasValue)
        {
            return new UnauthorizedResult();
        }

        var isAdmin = SecurityHelpers.IsAdmin(user);

        var result = await action(userId.Value, isAdmin);

        if (result.Type == AiPlanningResultType.Ok)
        {
            return new OkObjectResult(result.Payload);
        }

        return MapErrorResult(result);
    }

    /// <summary>
    /// Describes the HTTP status code and detail exposure policy for an AI planning error type.
    /// </summary>
    /// <param name="StatusCode">HTTP status code returned for the planning error.</param>
    /// <param name="IncludeDetails">Whether the service-provided details are safe to include in the response body.</param>
    private record ErrorMapping(int StatusCode, bool IncludeDetails);

    private static readonly IReadOnlyDictionary<AiPlanningResultType, ErrorMapping> Mappings =
        new Dictionary<AiPlanningResultType, ErrorMapping>
        {
            [AiPlanningResultType.NotFound] = new(404, false),
            [AiPlanningResultType.Forbidden] = new(403, false),
            [AiPlanningResultType.BadRequest] = new(400, true),
            [AiPlanningResultType.Conflict] = new(409, false),
            [AiPlanningResultType.InvalidAiResponse] = new(502, true),
            [AiPlanningResultType.RateLimitExceeded] = new(429, false)
        };

    /// <summary>
    /// Maps a failed AI planning result to the corresponding HTTP response.
    /// </summary>
    /// <typeparam name="T">The expected AI planning payload type.</typeparam>
    /// <param name="result">Planning result whose <see cref="AiPlanningResult{T}.Type"/> determines the status code.</param>
    /// <returns>
    /// An <see cref="ObjectResult"/> containing the error message, optional details for client-correctable failures,
    /// and a status code selected from the planning-result mapping table.
    /// </returns>
    public static IActionResult MapErrorResult<T>(AiPlanningResult<T> result)
    {
        if (!Mappings.TryGetValue(result.Type, out var mapping))
        {
            mapping = new ErrorMapping(500, false);
        }

        var errorResponse = new
        {
            error = result.Error ?? "AI planning failed",
            details = mapping.IncludeDetails ? result.Details : null
        };

        return new ObjectResult(errorResponse) { StatusCode = mapping.StatusCode };
    }
}
