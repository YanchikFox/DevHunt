using DevHunt.CoreApi.Services;
using DevHunt.Infrastructure;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using Polly;
using Polly.Extensions.Http;

namespace DevHunt.CoreApi.Extensions;

/// <summary>
/// Extension methods for infrastructure services configuration
/// </summary>
public static class InfrastructureExtensions
{
    /// <summary>
    /// Configures Redis cache
    /// </summary>
    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("RedisConnection")
            ?? configuration["RedisConnection"];

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var defaultRedisEnabled = !environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !environment.Equals("Test", StringComparison.OrdinalIgnoreCase);
        var redisEnabled = configuration.GetValue("Features:Redis:Enabled", defaultRedisEnabled);

        // If Redis isn't explicitly configured, fall back to in-memory cache.
        // This keeps CoreApi usable without Docker/infrastructure.
        if (!redisEnabled || string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddDistributedMemoryCache();
            services.AddScoped<ICacheService, CacheService>();
            return services;
        }

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
        });

        // Optional: ConnectionMultiplexer is used only for pattern deletion.
        // Configure it to not abort the app when Redis is unavailable.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(redisConnection);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            options.SyncTimeout = 5000;
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddScoped<ICacheService, CacheService>();
        return services;
    }

    /// <summary>
    /// Configures SignalR with Redis backplane
    /// </summary>
    public static ISignalRServerBuilder AddSignalRWithRedis(
        this ISignalRServerBuilder signalrBuilder,
        IConfiguration configuration)
    {
        // Only enable Redis backplane when Redis is explicitly configured.
        var redisConnection =
            configuration.GetConnectionString("RedisConnection")
            ?? configuration["RedisConnection"];

        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var defaultRedisEnabled = !environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && !environment.Equals("Test", StringComparison.OrdinalIgnoreCase);
        var redisEnabled = configuration.GetValue("Features:Redis:Enabled", defaultRedisEnabled);

        if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnection))
        {
            signalrBuilder.AddStackExchangeRedis(redisConnection, options =>
            {
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("DevHunt:SignalR"); // Prefix for Redis Pub/Sub
            });
        }

        return signalrBuilder;
    }

    /// <summary>
    /// Configures HTTP clients with circuit breaker and retry policies
    /// </summary>
    public static IServiceCollection AddResilientHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ML Service Client (HTTP client for integration with ML Service - Python/FastAPI)
        // ARCHITECTURE: Circuit breaker pattern for resilience
        var mlServicePolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30)
            )
            .WrapAsync(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddHttpClient<IMLServiceClient, MLServiceClient>(client =>
        {
            var baseUrl = configuration["MLService:BaseUrl"] ?? "http://ml-service:8000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(mlServicePolicy);

        // Integration Gateway Client
        var integrationGatewayPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30)
            )
            .WrapAsync(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddHttpClient<IIntegrationGatewayClient, IntegrationGatewayClient>(client =>
        {
            var baseUrl = configuration["IntegrationGateway:BaseUrl"] ?? "http://integration-gateway:5002";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(integrationGatewayPolicy);

        // Notification Service Client
        var notificationServicePolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30)
            )
            .WrapAsync(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.AddHttpClient<INotificationServiceClient, NotificationServiceClient>(client =>
        {
            var baseUrl = configuration["NotificationService:BaseUrl"] ?? "http://notification-service:5003";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(notificationServicePolicy);

        return services;
    }
}

