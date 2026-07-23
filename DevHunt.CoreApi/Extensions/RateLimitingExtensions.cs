using AspNetCoreRateLimit;

namespace DevHunt.CoreApi.Extensions;

/// <summary>
/// Extension methods for rate limiting configuration
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Configures rate limiting with Redis backend for distributed systems
    /// </summary>
    public static IServiceCollection AddDistributedRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        // Use Redis for distributed rate limiting (required for horizontal scaling)
        var redisConnection = configuration.GetConnectionString("RedisConnection");
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var defaultRedisEnabled = !environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !environment.Equals("Test", StringComparison.OrdinalIgnoreCase);
        var redisEnabled = configuration.GetValue("Features:Redis:Enabled", defaultRedisEnabled);

        if (redisEnabled && !string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
            });
            services.AddSingleton<IIpPolicyStore, DistributedCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, DistributedCacheRateLimitCounterStore>();
            services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
        }
        else
        {
            // Fallback to in-memory only for development/testing
            services.AddMemoryCache();
            services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
            services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
            services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
        }

        return services;
    }
}

