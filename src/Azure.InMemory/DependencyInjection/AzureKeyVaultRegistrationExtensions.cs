using Azure.InMemory.KeyVault;
using Azure.InMemory.KeyVault.InMemory;
using Azure.InMemory.KeyVault.Sdk;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Azure.InMemory.DependencyInjection;

public static class AzureKeyVaultRegistrationExtensions
{
    public static IServiceCollection AddAzureKeyVaultSdk(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureKeyVaultModeAvailable(services, KeyVaultRegistrationMode.Sdk);

        services.TryAddSingleton<IAzureKeyVaultFactory>(static provider => new AzureKeyVaultSdkFactory(
            ResolveRequiredAzureClient<SecretClient>(provider)));

        return services;
    }

    public static IServiceCollection AddAzureKeyVaultInMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureKeyVaultModeAvailable(services, KeyVaultRegistrationMode.InMemory);

        services.TryAddSingleton<InMemoryKeyVaultState>();
        services.TryAddSingleton<IAzureKeyVaultFactory, InMemoryKeyVaultFactory>();

        return services;
    }

    private static void EnsureKeyVaultModeAvailable(
        IServiceCollection services,
        KeyVaultRegistrationMode requestedMode)
    {
        var existingRegistration = services
            .Where(static descriptor => descriptor.ServiceType == typeof(KeyVaultRegistrationMarker))
            .Select(static descriptor => descriptor.ImplementationInstance as KeyVaultRegistrationMarker)
            .LastOrDefault(static marker => marker is not null);

        if (existingRegistration is null)
        {
            services.AddSingleton(new KeyVaultRegistrationMarker(requestedMode));
            return;
        }

        if (existingRegistration.Mode == requestedMode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Key Vault provider registration conflict: {existingRegistration.Mode.ToRegistrationMethodName()} has already been applied, " +
            $"so {requestedMode.ToRegistrationMethodName()} cannot also be used on the same IServiceCollection. " +
            "Choose exactly one Key Vault backend for a given service collection.");
    }

    private static T ResolveRequiredAzureClient<T>(IServiceProvider provider)
        where T : class
    {
        var client = provider.GetService<T>();
        if (client is not null)
        {
            return client;
        }

        throw new InvalidOperationException(
            $"AddAzureKeyVaultSdk() requires a DI-registered {typeof(T).Name}. " +
            $"Register {typeof(T).Name} in the host IServiceCollection before resolving {nameof(IAzureKeyVaultFactory)}.");
    }

    private enum KeyVaultRegistrationMode
    {
        Sdk,
        InMemory
    }

    private sealed record KeyVaultRegistrationMarker(KeyVaultRegistrationMode Mode);

    private static string ToRegistrationMethodName(this KeyVaultRegistrationMode mode) => mode switch
    {
        KeyVaultRegistrationMode.Sdk => nameof(AddAzureKeyVaultSdk) + "()",
        KeyVaultRegistrationMode.InMemory => nameof(AddAzureKeyVaultInMemory) + "()",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Key Vault registration mode.")
    };
}
