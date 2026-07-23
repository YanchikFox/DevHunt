using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Collections.Generic;
using System.Text;
using AspNetCoreRateLimit;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Prometheus;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using AspNet.Security.OAuth.GitHub;
using DevHunt.AuthService.Security;
using DevHunt.AuthService.Services;
using Serilog;

EnvLoader.Load();
var builder = WebApplication.CreateBuilder(args);

OAuthStateValidator.EnsureProductionStateSecret(builder.Configuration, builder.Environment);

// Secrets Management: Load from Azure Key Vault (production) or environment variables, then appsettings
var configBuilder = builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// ARCHITECTURE: Azure Key Vault integration for production secrets management (ARCH-006)
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrEmpty(keyVaultUri) && builder.Environment.IsProduction())
{
    try
    {
        configBuilder.AddAzureKeyVault(
            new Uri(keyVaultUri),
            new DefaultAzureCredential(),
            new AzureKeyVaultConfigurationOptions
            {
                ReloadInterval = TimeSpan.FromMinutes(5)
            });
        
        builder.Logging.AddConsole();
        builder.Logging.AddFilter("Azure", LogLevel.Warning);
    }
    catch (Exception ex)
    {
        builder.Logging.AddConsole();
        // Warning will be logged by Serilog after configuration
        Console.WriteLine($"Warning: Failed to connect to Azure Key Vault at {keyVaultUri}. Falling back to environment variables. Error: {ex.Message}");
    }
}
else
{
    configBuilder.AddUserSecrets<Program>(optional: true);
}

var Configuration = builder.Configuration;
var authServiceName = builder.Configuration["Tracing:ServiceName"] ?? "DevHunt.AuthService";
// Logging: Serilog with optional OpenSearch sink and enriched properties
builder.Host.UseSerilog(DevHunt.AuthService.Extensions.LoggingExtensions.ConfigureSerilog);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: authServiceName, serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = context => !context.Request.Path.StartsWithSegments("/metrics");
        })
        .AddHttpClientInstrumentation()
        // Jaeger exporter removed - using OpenObserve for all observability
        .AddOtlpExporter("openobserve", options =>
        {
            var endpoint = builder.Configuration["OpenObserve:OtlpTracesEndpoint"];
            if (string.IsNullOrEmpty(endpoint))
            {
                // Default to secure endpoint or require configuration
                // Using https by default to avoid CS-S1001
                endpoint = "https://openobserve:5080/api/default/otlp/v1/traces";
            }
            
            var openObserveUser = builder.Configuration["OpenObserve:User"] ?? "admin@devhunt.local";
            var openObservePassword = builder.Configuration["OpenObserve:Password"] ?? "ChangeMe123!";
            
            // SECURITY FIX (SEC-001): Validate OpenObserve password in production
            var isWeakPassword = string.IsNullOrEmpty(openObservePassword) ||
                openObservePassword.Length < 32 ||
                openObservePassword == "ChangeMe123!" ||
                openObservePassword.Contains("default", StringComparison.OrdinalIgnoreCase) ||
                openObservePassword.Contains("changeme", StringComparison.OrdinalIgnoreCase);

            if (builder.Environment.IsProduction() && isWeakPassword)
            {
                throw new InvalidOperationException(
                    "OPENOBSERVE_ROOT_PASSWORD must be set to a strong value (32+ chars) in production. " +
                    "Default password 'ChangeMe123!' is NOT allowed.");
            }
            
            var openObserveCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{openObserveUser}:{openObservePassword}"));

            options.Endpoint = new Uri(endpoint);
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            // NOTE: Headers in OpenTelemetry 1.10.0 is a string, not Dictionary
            // Format: "key1=value1,key2=value2" or just the header value
            options.Headers = $"Authorization=Basic {openObserveCredentials}";
        }));

// 1. Connect the database
builder.Services.AddDbContext<DevHuntDbContext>(options =>
    options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient();

// CORS configuration - Different policies for Development and Production
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:3000", "http://localhost:3001" };

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
                  .AllowCredentials(); // Required when using WithOrigins
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
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                  .AllowCredentials()
                  // Set preflight cache duration
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });
    }
});

// JWT Authentication for refresh token endpoint
var authBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = Configuration["Jwt:Key"];
        
        // SECURITY FIX (SEC-004): Validate JWT key in production
        if (builder.Environment.IsProduction())
        {
            if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
            {
                throw new InvalidOperationException(
                    "JWT:Key must be set to a strong value (32+ characters) in production. " +
                    "Never use default keys in production!");
            }
            
            // Check for common weak patterns
            var weakPatterns = new[] { "secret", "default", "changeme", "123456", "password" };
            if (weakPatterns.Any(pattern => jwtKey.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "JWT:Key contains weak password patterns. Use a cryptographically strong random key.");
            }
        }
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Configuration["Jwt:Issuer"],
            ValidAudience = Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!))
        };
        
        // R7: Read JWT from httpOnly cookie (XSS protection) with fallback to Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Priority 1: Read from httpOnly cookie (most secure - XSS protection)
                if (string.IsNullOrEmpty(context.Token))
                {
                    context.Token = context.Request.Cookies["access_token"];
                }
                
                // Priority 2: Fallback to Authorization header (backward compatibility for API clients)
                // This is automatically handled by JWT middleware
                
                return Task.CompletedTask;
            }
        };
    });

var googleClientId = Configuration["Authentication:Google:ClientId"];
var googleClientSecret = Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    authBuilder.AddGoogle("Google", options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/api/auth/callback/google";
    });
}

var githubClientId = Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = Configuration["Authentication:GitHub:ClientSecret"];
if (!string.IsNullOrEmpty(githubClientId) && !string.IsNullOrEmpty(githubClientSecret))
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.CallbackPath = "/api/auth/callback/github";
        options.Scope.Add("user:email");
    });
}

builder.Services.AddAuthorization();

// SECURITY: Rate limiting for auth endpoints (SEC-010) - using Redis for distributed systems
builder.Services.AddOptions();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Use Redis for distributed rate limiting (required for horizontal scaling)
var redisConnection = Configuration.GetConnectionString("RedisConnection");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
    builder.Services.AddSingleton<IIpPolicyStore, DistributedCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, DistributedCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
}
else
{
    // Fallback to in-memory only for development/testing
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
    builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
    builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
}

// SECURITY FIX (R4): Register RefreshTokenService for secure token management
builder.Services.AddScoped<DevHunt.AuthService.Services.IRefreshTokenService, DevHunt.AuthService.Services.RefreshTokenService>();
builder.Services.AddScoped<IEmailService, MailKitEmailService>();
builder.Services.AddScoped<IAuthValidationService, AuthValidationService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddScoped<IOAuthExchangeService, OAuthExchangeService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddSingleton<ITotpService, TotpService>();
// Sub-facades to reduce constructor over-injection
builder.Services.AddScoped<ITokenServices, TokenServices>();
builder.Services.AddScoped<IUserAuthServices, UserAuthServices>();
builder.Services.AddScoped<IAuthServices, AuthServicesFacade>();

// SEC-011: CSRF Protection for state-changing auth operations
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllers(options =>
{
    // SEC-011: Global CSRF validation for state-changing requests
    options.Filters.Add<DevHunt.AuthService.Filters.AuthCsrfValidationFilter>();
});
builder.Services.AddEndpointsApiExplorer();

// Health checks
var healthChecksBuilder = builder.Services.AddHealthChecks();
var dbConnection = Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(dbConnection))
{
    healthChecksBuilder.AddNpgSql(dbConnection);
}
// --- SWAGGER CONFIGURATION FOR JWT ---
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);

    // 1. Security scheme definition (Bearer Token)
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter 'Bearer', a space, and then your token",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // 2. Add security requirement for all endpoints
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});
// --- END OF SWAGGER CONFIGURATION ---

var app = builder.Build();

// NOTE: Database migrations are now handled by DevHunt.DatabaseMigrator job
// This prevents race conditions when multiple service instances start simultaneously
// Remove ApplyMigrationsAsync call - migrations run as a separate init job

// SECURITY: Swagger only in Development/Staging, never in Production (SEC-011)
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DevHunt Auth API v1");
        options.RoutePrefix = "swagger";
    });
}

// Use appropriate CORS policy based on environment
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");
}
else
{
    app.UseCors("ProductionCors");
}

// Enrich logs with trace/user info
app.UseMiddleware<DevHunt.AuthService.Middleware.LogEnrichmentMiddleware>();

// Observability: Prometheus HTTP metrics
app.UseMiddleware<DevHunt.AuthService.Middleware.MetricsMiddleware>();

// SECURITY: Rate limiting (must be before authentication)
app.UseIpRateLimiting();

// Enable authentication/authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SEC-011: CSRF token endpoint for SPA clients
app.MapGet("/api/auth/csrf-token", (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(context);
    return Results.Ok(new { requestToken = tokens.RequestToken });
}).AllowAnonymous();

// Health check endpoint
app.MapHealthChecks("/health").AllowAnonymous();

// Prometheus metrics endpoint
// SEC-008: Protected metrics endpoint - only accessible from localhost or with metrics token
app.MapGet("/metrics", async (HttpContext context) =>
{
    if (!DevHunt.Infrastructure.Security.MetricsAuthorization.IsAuthorized(
            context.Connection.RemoteIpAddress,
            context.Request.Headers["X-Metrics-Token"].FirstOrDefault()))
    {
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Forbidden: Metrics endpoint requires local access or valid token");
        return;
    }

    context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
    
    // Use prometheus-net built-in metrics
    using var stream = new MemoryStream();
    await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
    stream.Position = 0;
    using var reader = new StreamReader(stream);
    var metricsText = await reader.ReadToEndAsync();
    await context.Response.WriteAsync(metricsText);
}).AllowAnonymous();

app.Run();
