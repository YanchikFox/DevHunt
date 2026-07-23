namespace DevHunt.CoreApi.Services.Ai;

/// <summary>
/// Discriminator for <see cref="AiPlanningResult{T}"/> outcomes mapped to HTTP status codes by controllers.
/// </summary>
public enum AiPlanningResultType
{
    Ok,
    NotFound,
    Forbidden,
    BadRequest,
    Conflict,
    InvalidAiResponse,
    RateLimitExceeded,
    Error
}

/// <summary>
/// Typed service-layer result for AI planning operations.
/// Factory methods encode the outcome kind; callers inspect <see cref="Type"/> before using <see cref="Payload"/>.
/// </summary>
/// <typeparam name="T">Success payload type.</typeparam>
/// <param name="Type">Outcome discriminator.</param>
/// <param name="Payload">Success value when <see cref="Type"/> is <see cref="AiPlanningResultType.Ok"/>.</param>
/// <param name="Error">Primary error message for non-OK outcomes.</param>
/// <param name="Details">Optional validation or AI-response detail lines.</param>
public sealed record AiPlanningResult<T>(
    AiPlanningResultType Type,
    T? Payload = default,
    string? Error = null,
    IReadOnlyList<string>? Details = null)
{
    /// <summary>Successful result carrying a payload.</summary>
    /// <param name="payload">Response data.</param>
    /// <returns>Result with <see cref="AiPlanningResultType.Ok"/>.</returns>
    public static AiPlanningResult<T> Ok(T payload) => new(AiPlanningResultType.Ok, payload);

    /// <summary>Project, plan, or related entity was not found.</summary>
    /// <param name="error">User-facing message.</param>
    public static AiPlanningResult<T> NotFound(string error) => new(AiPlanningResultType.NotFound, Error: error);

    /// <summary>Caller lacks permission or capability for the operation.</summary>
    /// <param name="error">User-facing message.</param>
    public static AiPlanningResult<T> Forbidden(string error) => new(AiPlanningResultType.Forbidden, Error: error);

    /// <summary>Request failed validation before calling upstream AI.</summary>
    /// <param name="error">Primary validation message.</param>
    /// <param name="details">Optional field-level detail lines.</param>
    public static AiPlanningResult<T> BadRequest(string error, IReadOnlyList<string>? details = null) =>
        new(AiPlanningResultType.BadRequest, Error: error, Details: details);

    /// <summary>Resource exists but is in a state that blocks the operation (for example plan already applying).</summary>
    /// <param name="error">User-facing message.</param>
    public static AiPlanningResult<T> Conflict(string error) => new(AiPlanningResultType.Conflict, Error: error);

    /// <summary>Upstream AI returned a plan that failed structural validation.</summary>
    /// <param name="error">Summary message.</param>
    /// <param name="details">Validation errors from <see cref="AiPlanValidator"/>.</param>
    public static AiPlanningResult<T> InvalidAi(string error, IReadOnlyList<string> details) =>
        new(AiPlanningResultType.InvalidAiResponse, Error: error, Details: details);

    /// <summary>ML or LLM provider returned HTTP 429 or a rate-limit error.</summary>
    /// <param name="error">User-facing retry message.</param>
    public static AiPlanningResult<T> RateLimit(string error) => new(AiPlanningResultType.RateLimitExceeded, Error: error);

    /// <summary>Unexpected failure after validation (parse errors, unhandled exceptions surfaced to caller).</summary>
    /// <param name="error">User-facing message.</param>
    public static AiPlanningResult<T> ErrorResult(string error) => new(AiPlanningResultType.Error, Error: error);
}
