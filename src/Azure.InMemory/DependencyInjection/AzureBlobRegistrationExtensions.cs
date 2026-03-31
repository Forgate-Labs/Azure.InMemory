using Forgate.Azure.InMemory.Blob;
using Forgate.Azure.InMemory.Blob.InMemory;
using Forgate.Azure.InMemory.Blob.Sdk;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Forgate.Azure.InMemory.DependencyInjection;

public static class AzureBlobRegistrationExtensions
{
    public static IServiceCollection AddAzureBlobSdk(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureBlobModeAvailable(services, BlobRegistrationMode.Sdk);

        services.TryAddSingleton<IAzureBlobFactory>(static provider => new AzureBlobSdkFactory(
            ResolveRequiredAzureClient<BlobServiceClient>(provider)));

        return services;
    }

    public static IServiceCollection AddAzureBlobInMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureBlobModeAvailable(services, BlobRegistrationMode.InMemory);

        services.TryAddSingleton<InMemoryBlobState>();
        services.TryAddSingleton<IAzureBlobFactory, InMemoryBlobFactory>();

        return services;
    }

    private static void EnsureBlobModeAvailable(
        IServiceCollection services,
        BlobRegistrationMode requestedMode)
    {
        var existingRegistration = services
            .Where(static descriptor => descriptor.ServiceType == typeof(BlobRegistrationMarker))
            .Select(static descriptor => descriptor.ImplementationInstance as BlobRegistrationMarker)
            .LastOrDefault(static marker => marker is not null);

        if (existingRegistration is null)
        {
            services.AddSingleton(new BlobRegistrationMarker(requestedMode));
            return;
        }

        if (existingRegistration.Mode == requestedMode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Blob provider registration conflict: {existingRegistration.Mode.ToRegistrationMethodName()} has already been applied, " +
            $"so {requestedMode.ToRegistrationMethodName()} cannot also be used on the same IServiceCollection. " +
            "Choose exactly one Blob backend for a given service collection.");
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
            $"AddAzureBlobSdk() requires a DI-registered {typeof(T).Name}. " +
            $"Register {typeof(T).Name} in the host IServiceCollection before resolving {nameof(IAzureBlobFactory)}.");
    }

    private enum BlobRegistrationMode
    {
        Sdk,
        InMemory
    }

    private sealed record BlobRegistrationMarker(BlobRegistrationMode Mode);

    private static string ToRegistrationMethodName(this BlobRegistrationMode mode) => mode switch
    {
        BlobRegistrationMode.Sdk => nameof(AddAzureBlobSdk) + "()",
        BlobRegistrationMode.InMemory => nameof(AddAzureBlobInMemory) + "()",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Blob registration mode.")
    };
}
