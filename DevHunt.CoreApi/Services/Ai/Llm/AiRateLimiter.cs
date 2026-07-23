using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Result of a rate-limit check. <see cref="Allowed"/> false means the user
/// has hit the cap for the current window; <see cref="ResetIn"/> tells them
/// when it rolls over.
/// </summary>
public sealed record AiRateLimitResult(bool Allowed, int Used, int Limit, TimeSpan ResetIn);

/// <summary>
/// Per-user per-minute cap on /ai requests. BYOK means the user pays the
/// token cost, so this is not a cost quota; it is an abuse circuit-breaker:
///   1. compromised JWT can't burn the user's provider credits at full speed,
///   2. accidental loops don't fan out SignalR broadcasts to every channel
///      participant N times a second,
///   3. the AiOperationLog table doesn't grow unboundedly per user.
/// 30 req/minute lets a heavy human user work without ever noticing the cap.
/// </summary>
public interface IAiRateLimiter
{
    /// <summary>
    /// Atomically increments the per-user counter for the current minute window and returns allow/deny.
    /// Uses Redis when available; falls back to in-process memory on Redis errors.
    /// </summary>
    Task<AiRateLimitResult> CheckAndIncrementAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// Fixed-window per-minute rate limiter backed by Redis with in-memory fallback.
/// </summary>
public sealed class AiRateLimiter : IAiRateLimiter
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IMemoryCache _memory;
    private readonly int _perMinuteLimit;
    private readonly ILogger<AiRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiRateLimiter"/> class.
    /// </summary>
    /// <param name="configuration">Configuration source for rate-limit settings.</param>
    /// <param name="memory">Project memory service that persists decisions.</param>
    /// <param name="logger">Logger for diagnostics and recoverable failures.</param>
    /// <param name="redis">Optional Redis connection for distributed counters.</param>
    public AiRateLimiter(
        IConfiguration configuration,
        IMemoryCache memory,
        ILogger<AiRateLimiter> logger,
        IConnectionMultiplexer? redis = null)
    {
        _memory = memory;
        _logger = logger;
        _redis = redis;
        _perMinuteLimit = configuration.GetValue("Ai:RateLimit:PerMinutePerUser", 30);
    }

    /// <inheritdoc />
    public async Task<AiRateLimitResult> CheckAndIncrementAsync(Guid userId, CancellationToken ct)
    {
        // Fixed-window per-minute counter keyed on yyyyMMddHHmm. Approximate
        // (a sliding window would be cleaner) but cheap and good enough for
        // catching loops. At the minute boundary you get at most 2x burst.
        var nowUtc = DateTime.UtcNow;
        var windowKey = nowUtc.ToString("yyyyMMddHHmm");
        var key = $"ai:rate:{userId:N}:{windowKey}";
        var resetIn = TimeSpan.FromSeconds(60 - nowUtc.Second);

        // Redis path: atomic INCR + EXPIRE so two concurrent calls never race
        // past the limit. Without atomicity a parallel-fetch attack could
        // sneak through with N concurrent allowed requests.
        if (_redis != null)
        {
            try
            {
                var db = _redis.GetDatabase();
                var used = (int)await db.StringIncrementAsync(key);
                if (used == 1)
                {
                    // First hit of the window: set expiry. Idempotent on race.
                    await db.KeyExpireAsync(key, resetIn);
                }

                return new AiRateLimitResult(used <= _perMinuteLimit, used, _perMinuteLimit, resetIn);
            }
            catch (RedisException ex)
            {
                // Redis flap should not lock users out; fall through to memory.
                _logger.LogWarning(ex, "Redis rate-limit read failed for user {UserId}; falling back to in-memory", userId);
            }
        }

        // In-memory fallback: per-process counter. Useless across replicas, but
        // dev runs single-process and prod has Redis. Still better than nothing
        // when Redis is briefly unreachable.
        var counter = _memory.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = resetIn;
            return new InMemoryCounter();
        })!;

        var snapshot = Interlocked.Increment(ref counter.Value);
        return new AiRateLimitResult(snapshot <= _perMinuteLimit, snapshot, _perMinuteLimit, resetIn);
    }

    /// <summary>
    /// Holds the process-local count for the active fixed-window fallback.
    /// </summary>
    private sealed class InMemoryCounter
    {
        /// <summary>Current count for the active fixed window.</summary>
        public int Value;
    }
}

/// <summary>
/// Thrown when a user has exceeded their AI request quota; caller surfaces this
/// as <c>429 Too Many Requests</c> with the exact reset window in the body.
/// </summary>
public sealed class AiRateLimitExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AiRateLimitExceededException"/> class.
    /// </summary>
    /// <param name="result">Rate-limit result that triggered the exception.</param>
    public AiRateLimitExceededException(AiRateLimitResult result)
        : base($"AI request rate limit exceeded ({result.Used}/{result.Limit}). Resets in {result.ResetIn.TotalSeconds:F0}s.")
    {
        Result = result;
    }

    /// <summary>Rate-limit snapshot attached to <see cref="AiRateLimitExceededException"/>.</summary>
    public AiRateLimitResult Result { get; }
}
