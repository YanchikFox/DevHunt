using System.Net.Http.Headers;
using System.Text;
using Serilog;
using Serilog.Sinks.Http;

using Serilog.Formatting.Compact;

namespace DevHunt.AuthService.Extensions;

public static class LoggingExtensions
{
    public static void ConfigureSerilog(HostBuilderContext ctx, LoggerConfiguration lc)
    {
        if (ctx.Configuration.GetValue<bool>("Documentation:OpenApiExport"))
        {
            lc.WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
            return;
        }

        lc.ReadFrom.Configuration(ctx.Configuration)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("application", "auth-service")
          .Enrich.WithProperty("environment", ctx.HostingEnvironment.EnvironmentName)
          .Enrich.WithProperty("service", "auth-service");

        // OpenObserve HTTP sink for centralized logging
        var openObserveEndpoint = ctx.Configuration["OpenObserve:LogsEndpoint"];
        var openObserveUser = ctx.Configuration["OpenObserve:User"];
        var openObservePassword = ctx.Configuration["OpenObserve:Password"];

        if (!string.IsNullOrEmpty(openObserveEndpoint) &&
            !string.IsNullOrEmpty(openObserveUser) &&
            !string.IsNullOrEmpty(openObservePassword))
        {
            lc.WriteTo.Http(
                requestUri: openObserveEndpoint,
                queueLimitBytes: 1_000_000,
                httpClient: new OpenObserveHttpClient(openObserveUser, openObservePassword),
                textFormatter: new RenderedCompactJsonFormatter()
            );
        }

        // Console JSON for local debugging/aggregation
        lc.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
}

/// <summary>
/// Custom HTTP client for OpenObserve with Basic Auth
/// </summary>
public class OpenObserveHttpClient : IHttpClient
{
    private readonly HttpClient _httpClient;

    public OpenObserveHttpClient(string user, string password)
    {
        // nosemgrep: devhunt-new-httpclient-instantiation — Serilog sink is created once at startup;
        // IHttpClientFactory is unavailable in pre-DI logging context. This is a controlled singleton.
        _httpClient = new HttpClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    public void Configure(IConfiguration configuration) { }

    public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream, CancellationToken cancellationToken)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await _httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();
}
