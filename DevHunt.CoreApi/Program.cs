using DevHunt.CoreApi.Extensions;
using DevHunt.CoreApi.Middleware;
using DevHunt.CoreApi.Services;
using DevHunt.CoreApi.Services.Ai;
using DevHunt.CoreApi.Services.Ai.Llm;
using DevHunt.CoreApi.Services.Ai.Llm.Providers;
using DevHunt.CoreApi.Services.Ai.Llm.Tools;
using DevHunt.CoreApi.Services.Projects;
using DevHunt.CoreApi.Services.Users;
using DevHunt.CoreApi.Services.Tasks;
using DevHunt.CoreApi.Services.Profile;
using DevHunt.CoreApi.Services.Integrations;
using DevHunt.CoreApi.Services.Chat;
using DevHunt.CoreApi.Services.CodeAnalysis;
using DevHunt.CoreApi.Services.Badges;
using DevHunt.CoreApi.Services.Moderation;
using DevHunt.CoreApi.Security;
using DevHunt.CoreApi.Filters;
using DevHunt.CoreApi.Hubs;
using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Configuration;
using DevHunt.Infrastructure.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Antiforgery;
using Prometheus;
using Serilog;
using Microsoft.AspNetCore.CookiePolicy;
using AspNetCoreRateLimit;

// ============================================================================
// DevHunt Core API - Application Entry Point
// ============================================================================
// Architecture: ASP.NET Core Web API (Core API microservice per SRS v1.0)
// Technology Stack: .NET 10.0, Entity Framework Core, PostgreSQL
//
// Core Modules:
// - Authentication & Authorization (JWT/OAuth2)
// - Projects & Teams Management
// - Notifications & Moderation
// - Security & Rate Limiting
//
// SRS Use Cases:
// - UC-1: Registration and Profile Creation
// - UC-2: Project Creation
// - UC-3: Team Search and Member Recruitment
// - UC-4: Project Collaboration
// - UC-5: Project Publishing and Showcase
// - UC-6: Mentorship and Company Interactions
// ============================================================================

EnvLoader.Load();
var builder = WebApplication.CreateBuilder(args);

// Configuration: Load from Azure Key Vault (production) or environment variables
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddSecretsConfiguration(builder.Environment);

var Configuration = builder.Configuration;
var allowedCorsOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

// Logging: Serilog with Search Service (OpenSearch) sink
builder.Host.UseSerilog(LoggingExtensions.ConfigureSerilog);

// OpenTelemetry for distributed tracing
builder.Services.AddOpenTelemetryTracing(Configuration);

// Rate limiting with Redis backend for distributed systems
builder.Services.AddDistributedRateLimiting(Configuration);

// Database
builder.Services.AddDatabaseContext(Configuration);

// CORS - Different policies for Development and Production
builder.Services.AddCors(options =>
{
    // Development: More permissive for local development
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("DevCors", policy =>
        {
            policy.WithOrigins(allowedCorsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    }
    else
    {
        // Production: Strict CORS policy
        options.AddPolicy("ProductionCors", policy =>
        {
            // Only allow specific origins from configuration
            policy.WithOrigins(allowedCorsOrigins)
                  // Only allow specific headers (not AllowAnyHeader)
                  .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "X-CSRF-TOKEN")
                  // Only allow specific HTTP methods
                  .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
                  .AllowCredentials()
                  // Set preflight cache duration
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });
    }
});

// Authentication & Authorization
builder.Services.AddJwtAuthentication(Configuration);
builder.Services.AddAuthorizationPolicies();

// SignalR (WebSocket for real-time chat and notifications)
var signalrBuilder = builder.Services.AddSignalR(options =>
{
    // SECURITY: Disable detailed errors in production (SEC-019)
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    // SECURITY: Limit message size to prevent DoS (SEC-019)
    options.MaximumReceiveMessageSize = 8192; // 8KB
});
signalrBuilder.AddSignalRWithRedis(Configuration);

// Application Services
builder.Services.AddSingleton<IProfanityFilterService, ProfanityFilterService>();
builder.Services.AddScoped<ProfanityFilter>();
builder.Services.AddScoped<ProfanityCensorFilter>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<IPresenceService, PresenceService>();
builder.Services.AddSingleton<IInternalServiceAuthenticator, InternalServiceAuthenticator>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFeatureFlagService, CachedFeatureFlagService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IUserStatsService, UserStatsService>();
builder.Services.AddHttpContextAccessor(); // For AuditService
builder.Services.AddScoped<INotificationHelperService, NotificationHelperService>();
// P2: IHttpClientFactory — prevents socket exhaustion vs new HttpClient() per-call
builder.Services.AddHttpClient("sendgrid", c =>
{
    c.BaseAddress = new Uri("https://api.sendgrid.com/");
});
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IAiEntitlementService, AllowAllAiEntitlementService>();
builder.Services.Configure<AiPlanningOptions>(Configuration.GetSection("AiPlanning"));
builder.Services.AddScoped<IAiPlanValidator, AiPlanValidator>();
builder.Services.AddScoped<IAiPlanApplier, AiPlanApplier>();
builder.Services.AddScoped<IAiStrategyRouter, AiStrategyRouter>();
builder.Services.AddScoped<IAiPlannerStrategy, V1AiPlannerStrategy>();
builder.Services.AddScoped<IAiCapabilityPolicy, AllowAllAiCapabilityPolicy>();
builder.Services.AddScoped<IAiUsageMeter, DbAiUsageMeter>();
builder.Services.AddScoped<IAiPlanningService, AiPlanningService>();
builder.Services.AddScoped<IAiPlanDraftService, AiPlanDraftService>();

// BYOK / multi-provider LLM layer.
builder.Services.AddDefaultLlmProviders();
builder.Services.AddSingleton<ILlmProviderRegistry, LlmProviderRegistry>();
builder.Services.AddScoped<IUserApiKeyService, UserApiKeyService>();
builder.Services.AddScoped<ILlmModelRegistryService, LlmModelRegistryService>();
builder.Services.AddScoped<ILlmModelSyncService, LlmModelSyncService>();
builder.Services.AddScoped<ILlmChatService, LlmChatService>();
builder.Services.AddScoped<LlmModelSeeder>();
builder.Services.AddHostedService<AiMessageDetailsPruneWorker>();

// LLM chat runtime: rate limiting, skills, project memory/context, cancellation and tools.
builder.Services.AddSingleton<IAiInFlightRegistry, AiInFlightRegistry>();
builder.Services.AddSingleton<IAiSkillResolver, AiSkillResolver>();
builder.Services.AddScoped<IAiRateLimiter, AiRateLimiter>();
builder.Services.AddScoped<IProjectContextBuilder, ProjectContextBuilder>();
builder.Services.AddScoped<IProjectMemoryService, ProjectMemoryService>();
builder.Services.AddScoped<IAiToolDispatcher, AiToolDispatcher>();
builder.Services.AddScoped<IAiToolExecutionService, AiToolExecutionService>();
builder.Services.AddScoped<IAiTool, CreateTaskTool>();
builder.Services.AddScoped<IAiTool, UpdateTaskTool>();
builder.Services.AddScoped<IAiTool, MoveTaskTool>();
builder.Services.AddScoped<IAiTool, DeleteTaskTool>();
builder.Services.AddScoped<IAiTool, CreateMultipleTasksTool>();
builder.Services.AddScoped<IAiTool, MoveMultipleTasksTool>();
builder.Services.AddScoped<IAiTool, DeleteMultipleTasksTool>();
builder.Services.AddScoped<IAiTool, UpdateProjectTool>();
builder.Services.AddScoped<IAiTool, RecordDecisionTool>();
builder.Services.AddScoped<IAiTool, ReadDocumentTool>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IProjectChannelService, ProjectChannelService>();
builder.Services.AddScoped<IChannelMemberService, ChannelMemberService>();
builder.Services.AddScoped<IBadgesService, BadgesService>();
builder.Services.AddScoped<IAchievementTriggerService, AchievementTriggerService>();

// User Services (extracted from UsersController for CodeScene improvements)
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IUserFollowService, UserFollowService>();
builder.Services.AddScoped<IUserActivityService, UserActivityService>();
builder.Services.AddScoped<IUserSearchService, UserSearchService>();
builder.Services.AddScoped<IUserServices, UserServicesFacade>();

// Project Services (extracted from ProjectsController for better testability)
builder.Services.AddScoped<ITechStackMatcher, TechStackMatcher>();
builder.Services.AddScoped<IProjectFilterService, ProjectFilterService>();
builder.Services.AddScoped<IProjectAuthorizationPolicy, ProjectAuthorizationPolicy>();
builder.Services.AddScoped<IProjectPermissionService, ProjectPermissionService>();
builder.Services.AddScoped<IProjectServices, ProjectServicesFacade>();
builder.Services.AddScoped<IProjectTeamService, ProjectTeamService>();
builder.Services.AddScoped<IProjectNewsService, ProjectNewsService>();
builder.Services.AddScoped<IProjectIssuesService, ProjectIssuesService>();

// Task Services (extracted from TasksController for CodeScene improvements)
builder.Services.AddScoped<ITaskAuthorizationService, TaskAuthorizationService>();

// Integration Services (extracted from IntegrationsController for CodeScene improvements)
builder.Services.AddScoped<IIntegrationAuthorizationService, IntegrationAuthorizationService>();
builder.Services.AddScoped<IOAuthCallbackHandler, OAuthCallbackHandler>();

// Profile Services (extracted from ProfileController to reduce constructor over-injection)
builder.Services.AddScoped<IProfileServices, ProfileServicesFacade>();

// Infrastructure Services
builder.Services.AddSingleton<ReadWriteDbContextFactory>();
builder.Services.AddRedisCache(Configuration);
builder.Services.AddMemoryCache(); // for MaintenanceModeMiddleware in-process cache

// REL-002: Outbox Pattern for reliable event delivery
// Enable only when RabbitMQ is explicitly configured.
var rabbitMqConnectionString = Configuration["RabbitMQ:ConnectionString"];
var eventBusFeatureEnabled = Configuration.GetValue("Features:EventBus:Enabled", true);
EventBusStartupValidation.EnsureProductionConfiguration(
    builder.Environment.IsProduction(),
    eventBusFeatureEnabled,
    rabbitMqConnectionString);

var isEventBusEnabled = eventBusFeatureEnabled
    && !string.IsNullOrWhiteSpace(rabbitMqConnectionString);

var eventBusRuntimeState = new EventBusRuntimeState(isEventBusEnabled);
builder.Services.AddSingleton(eventBusRuntimeState);
EventBusMetrics.Configure(isEventBusEnabled);

if (isEventBusEnabled)
{
    // Register RabbitMQ service but don't use it directly - it's used by background worker
    builder.Services.AddSingleton<RabbitMQEventBusService>();
    builder.Services.AddScoped<IOutboxEventService, OutboxEventService>();
    builder.Services.AddScoped<IEventBusService, OutboxEventBusDecorator>(); // Decorator saves to outbox
    builder.Services.AddHostedService<OutboxEventProcessorWorker>(); // Worker publishes from outbox
}
else
{
    builder.Services.AddSingleton<IEventBusService, NoOpEventBusService>();
    if (!builder.Environment.IsProduction())
    {
        Log.Warning(
            "Event bus running in noop mode (event_bus_mode=noop). Set RabbitMQ:ConnectionString to publish domain events.");
    }
}

builder.Services.AddHostedService<UnverifiedAccountCleanupWorker>();
builder.Services.AddScoped<ICodeAnalysisEmbeddingJobService, CodeAnalysisEmbeddingJobService>();
builder.Services.AddScoped<ICodeAnalysisEmbeddingGenerationService, CodeAnalysisEmbeddingGenerationService>();
builder.Services.AddHostedService<CodeAnalysisEmbeddingJobWorker>();
builder.Services.AddScoped<IObjectStorageService, S3ObjectStorageService>();
builder.Services.AddSingleton<IMetricsService, PrometheusMetricsService>();

// HTTP Clients with Circuit Breaker & Retry
builder.Services.AddResilientHttpClients(Configuration);

// CSRF Protection
// SEC-011: Disable user identity binding to avoid token invalidation on login/logout
// Security is still maintained through:
// 1. Cryptographically generated tokens
// 2. Double Submit Cookie pattern (cookie + header must match)
// 3. JWT authentication for actual user authorization
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SuppressXFrameOptionsHeader = false;
});

// Input size limits (Request size protection)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB max
    options.ValueLengthLimit = 4 * 1024 * 1024; // 4MB per field
    options.ValueCountLimit = 1000; // Max fields
    options.MemoryBufferThreshold = Int32.MaxValue;
});

#if NET9_0_OR_GREATER && !NET10_0_OR_GREATER
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});
#endif

// Health Checks
var healthChecks = builder.Services.AddHealthChecks()
    .AddNpgSql(Configuration.GetConnectionString("DefaultConnection") ?? "", name: "postgresql")
    .AddCheck<EventBusHealthCheck>("event_bus", failureStatus: HealthStatus.Degraded, tags: ["ready"]);

var redisConnection = Configuration.GetConnectionString("RedisConnection")
    ?? Configuration["RedisConnection"];
var redisEnabled = Configuration.GetValue("Features:Redis:Enabled", !builder.Environment.IsDevelopment());
if (redisEnabled && !string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis");
}

// Response Compression (gzip/brotli)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Enable compression for HTTPS
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    // Exclude specific MIME types from compression
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "application/xml", "text/plain", "text/css", "application/javascript" });
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

// Controllers & API Documentation
builder.Services.AddControllers(options =>
{
    options.MaxModelBindingCollectionSize = 100; // Max array/list size

    // SEC-011: Global CSRF validation filter for state-changing requests
    // Applied to POST, PUT, DELETE, PATCH - skips safe methods (GET, HEAD, OPTIONS)
    options.Filters.Add<GlobalCsrfValidationFilter>();
})
.AddJsonOptions(options =>
{
    // Configure JSON serialization to use camelCase for property names
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    // Accept both camelCase and PascalCase from clients (frontend sends PascalCase via toPascalCase interceptor)
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);

    // Include XML comments for Swagger documentation
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// NOTE: Database migrations are now handled by DevHunt.DatabaseMigrator job
// This prevents race conditions when multiple service instances start simultaneously

// SECURITY: Swagger only in Development/Staging, never in Production (SEC-011)
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DevHunt API v1");
        options.RoutePrefix = "swagger";
    });
}

// Response Compression (must be before other middleware that writes responses)
app.UseResponseCompression();

// Use appropriate CORS policy based on environment
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}
else
{
    app.UseCors("ProductionCors");
}

// Observability: Correlation IDs for request tracing
app.UseCorrelationId();
// Enrich logs with trace/user info
app.UseMiddleware<DevHunt.CoreApi.Middleware.LogEnrichmentMiddleware>();

// Observability: Prometheus metrics
app.UseMiddleware<MetricsMiddleware>();

// Security: File upload validation (SEC-018)
app.UseMiddleware<FileUploadValidationMiddleware>();

// Security: Security headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// SEC-011: CSRF token middleware - generates tokens for all requests
app.UseCsrfToken();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// SECURITY FIX (SEC-010): Rate limiting BEFORE user active check
// This prevents brute force attacks on inactive users
app.UseMiddleware<IpRateLimitMiddleware>();

// Security: Check if authenticated user is active
// This runs after rate limiting to prevent bypass attacks
app.UseMiddleware<UserActiveCheckMiddleware>();

// Maintenance Mode: block non-admin API traffic when platform is under maintenance
// Runs after authentication so admin/superadmin role claims are available
app.UseMiddleware<MaintenanceModeMiddleware>();

// Health check endpoint
app.MapHealthChecks("/health").AllowAnonymous();

// SEC-011: CSRF token endpoint - returns token for SPA clients
app.MapGet("/api/csrf-token", (HttpContext context, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
}).AllowAnonymous()
  .WithSummary("Returns an antiforgery token for SPA clients.")
  .WithName("GetCsrfToken");

// Feature flags endpoint - returns flag states for authenticated users (UI gating)
app.MapGet("/api/feature-flags", async (IFeatureFlagService featureFlags, CancellationToken ct) =>
{
    var flags = await featureFlags.GetAllAsync(ct);
    return Results.Ok(flags);
}).RequireAuthorization()
  .WithSummary("Returns all feature flag states for authenticated users.")
  .WithName("GetFeatureFlags");

// Prometheus metrics endpoint
// SEC-008: Protected metrics endpoint - only accessible from localhost or with metrics token
app.MapGet("/metrics", async (HttpContext context) =>
{
    if (!MetricsAuthorization.IsAuthorized(
            context.Connection.RemoteIpAddress,
            context.Request.Headers["X-Metrics-Token"].FirstOrDefault()))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Forbidden: Metrics endpoint requires local access or valid token");
        return;
    }

    context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";

    using var stream = new MemoryStream();
    await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
    stream.Position = 0;
    using var reader = new StreamReader(stream);
    var metricsText = await reader.ReadToEndAsync();
    await context.Response.WriteAsync(metricsText);
}).AllowAnonymous();

// SignalR Hubs (WebSocket for real-time communication)
app.MapHub<ChatHub>("/chatHub");
app.MapHub<NotificationHub>("/notificationHub");

app.MapControllers();

// BYOK model catalog: insert missing starter rows without overwriting ops edits.
if (!Configuration.GetValue<bool>("Documentation:OpenApiExport"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var llmModelSeeder = scope.ServiceProvider.GetRequiredService<LlmModelSeeder>();
        await llmModelSeeder.SeedAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to seed LLM model registry. API will continue with existing model rows.");
    }
}

// REL-001: Initialize Object Storage buckets on startup with graceful degradation
// If bucket initialization fails, the service will continue in degraded mode
// Health checks will reflect this status
var initializeBucketsOnStartup = Configuration.GetValue("ObjectStorage:InitializeBucketsOnStartup", true);
if (initializeBucketsOnStartup)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();
        await objectStorage.InitializeBucketsAsync();
        app.Logger.LogInformation("✅ Object Storage buckets initialized successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "⚠️ Failed to initialize Object Storage buckets. Service running in DEGRADED mode - uploads may fail.");
        // Don't fail startup - graceful degradation allows API to serve read-only requests
    }
}

await app.RunAsync();
