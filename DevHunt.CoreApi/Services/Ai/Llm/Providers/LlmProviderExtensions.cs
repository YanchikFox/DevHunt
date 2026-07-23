using Microsoft.Extensions.DependencyInjection;

namespace DevHunt.CoreApi.Services.Ai.Llm.Providers;

/// <summary>
/// DI helpers - register every supported OpenAI-compatible service in one
/// place so adding a new one (Fireworks, Cerebras, ...) is a one-liner.
/// </summary>
public static class LlmProviderExtensions
{
    /// <summary>
    /// Registers a named OpenAI-compatible <see cref="ILlmProvider"/> with a dedicated HTTP client.
    /// </summary>
    public static IServiceCollection AddOpenAiCompatibleProvider(
        this IServiceCollection services,
        OpenAiCompatibleConfig config)
    {
        // Each provider gets a named HttpClient - gives us per-provider
        // telemetry naming and lets resilience policies be tuned individually.
        services.AddHttpClient($"llm:{config.ProviderId}");

        services.AddSingleton<ILlmProvider>(sp => new OpenAiCompatibleProvider(
            config,
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<ILogger<OpenAiCompatibleProvider>>()));

        return services;
    }

    /// <summary>
    /// Registers all the providers we support out of the box. Each entry is
    /// a single line - the OpenAI-compatible adapter handles them all.
    /// </summary>
    public static IServiceCollection AddDefaultLlmProviders(this IServiceCollection services)
    {
        // OpenAI itself.
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "openai",
            DisplayName: "OpenAI",
            BaseUrl: new Uri("https://api.openai.com/v1/")));

        // Groq - fastest, OpenAI-compatible, supports llama / mixtral / qwen.
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "groq",
            DisplayName: "Groq",
            BaseUrl: new Uri("https://api.groq.com/openai/v1/")));

        // DeepSeek - strong reasoning models at very low prices.
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "deepseek",
            DisplayName: "DeepSeek",
            BaseUrl: new Uri("https://api.deepseek.com/")));

        // OpenRouter - proxies to ~300 models. Sends back attribution headers
        // we should populate so the user's account isn't rate-limited as
        // anonymous traffic.
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "openrouter",
            DisplayName: "OpenRouter",
            BaseUrl: new Uri("https://openrouter.ai/api/v1/"),
            DefaultHeaders: new Dictionary<string, string>
            {
                ["HTTP-Referer"] = "https://devhunt.app",
                ["X-Title"] = "DevHunt",
            }));

        // xAI - Grok models, OpenAI-compatible API.
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "xai",
            DisplayName: "xAI (Grok)",
            BaseUrl: new Uri("https://api.x.ai/v1/")));

        // Mistral - Codestral, Mistral Large.
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "mistral",
            DisplayName: "Mistral",
            BaseUrl: new Uri("https://api.mistral.ai/v1/")));

        // Together - open-weight models hosted (Llama, Qwen, ...).
        services.AddOpenAiCompatibleProvider(new OpenAiCompatibleConfig(
            ProviderId: "together",
            DisplayName: "Together",
            BaseUrl: new Uri("https://api.together.xyz/v1/")));

        services.AddHttpClient("llm:anthropic");
        services.AddSingleton<ILlmProvider, AnthropicProvider>();

        services.AddHttpClient("llm:gemini");
        services.AddSingleton<ILlmProvider, GeminiProvider>();

        return services;
    }
}
