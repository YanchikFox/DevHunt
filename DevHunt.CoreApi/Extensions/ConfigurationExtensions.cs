using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

namespace DevHunt.CoreApi.Extensions;

/// <summary>
/// Extension methods for application configuration
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Configures secrets management from Azure Key Vault (production) or environment variables
    /// </summary>
    public static IConfigurationBuilder AddSecretsConfiguration(
        this IConfigurationBuilder configBuilder,
        IWebHostEnvironment environment)
    {
        var keyVaultUri = configBuilder.Build()["KeyVault:Uri"];

        // ARCHITECTURE: Azure Key Vault integration for production secrets management (ARCH-006)
        if (!string.IsNullOrEmpty(keyVaultUri) && environment.IsProduction())
        {
            try
            {
                // Use DefaultAzureCredential which supports:
                // - Managed Identity (Azure VMs, App Services, Kubernetes)
                // - Visual Studio, Azure CLI, Azure PowerShell
                // - Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID)
                configBuilder.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential(),
                    new AzureKeyVaultConfigurationOptions
                    {
                        ReloadInterval = TimeSpan.FromMinutes(5) // Refresh secrets every 5 minutes
                    });
            }
            catch (Exception ex)
            {
                // Log but don't fail startup if Key Vault is unavailable
                // Fall back to environment variables
                Console.WriteLine($"Warning: Failed to connect to Azure Key Vault at {keyVaultUri}. Error: {ex.Message}");
            }
        }
        else
        {
            // Development: Use User Secrets
            // Note: Program class is in the same assembly
            var assembly = typeof(ConfigurationExtensions).Assembly;
            configBuilder.AddUserSecrets(assembly, optional: true);
        }

        return configBuilder;
    }
}

