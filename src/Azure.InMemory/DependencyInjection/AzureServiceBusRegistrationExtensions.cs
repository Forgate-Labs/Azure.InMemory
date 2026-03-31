using Azure.InMemory.ServiceBus;
using Azure.InMemory.ServiceBus.InMemory;
using Azure.InMemory.ServiceBus.Sdk;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Azure.InMemory.DependencyInjection;

public static class AzureServiceBusRegistrationExtensions
{
    public static IServiceCollection AddAzureServiceBusSdk(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureServiceBusModeAvailable(services, ServiceBusRegistrationMode.Sdk);

        services.TryAddSingleton<IAzureServiceBusFactory>(static provider => new AzureServiceBusSdkFactory(
            ResolveRequiredAzureClient<ServiceBusClient>(provider),
            ResolveRequiredAzureClient<ServiceBusAdministrationClient>(provider)));

        return services;
    }

    public static IServiceCollection AddAzureServiceBusInMemory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        EnsureServiceBusModeAvailable(services, ServiceBusRegistrationMode.InMemory);

        services.TryAddSingleton<InMemoryServiceBusState>();
        services.TryAddSingleton<IAzureServiceBusFactory, InMemoryServiceBusFactory>();

        return services;
    }

    private static void EnsureServiceBusModeAvailable(
        IServiceCollection services,
        ServiceBusRegistrationMode requestedMode)
    {
        var existingRegistration = services
            .Where(static descriptor => descriptor.ServiceType == typeof(ServiceBusRegistrationMarker))
            .Select(static descriptor => descriptor.ImplementationInstance as ServiceBusRegistrationMarker)
            .LastOrDefault(static marker => marker is not null);

        if (existingRegistration is null)
        {
            services.AddSingleton(new ServiceBusRegistrationMarker(requestedMode));
            return;
        }

        if (existingRegistration.Mode == requestedMode)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Service Bus provider registration conflict: {existingRegistration.Mode.ToRegistrationMethodName()} has already been applied, " +
            $"so {requestedMode.ToRegistrationMethodName()} cannot also be used on the same IServiceCollection. " +
            "Choose exactly one Service Bus backend for a given service collection.");
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
            $"AddAzureServiceBusSdk() requires a DI-registered {typeof(T).Name}. " +
            $"Register {typeof(T).Name} in the host IServiceCollection before resolving {nameof(IAzureServiceBusFactory)}.");
    }

    private enum ServiceBusRegistrationMode
    {
        Sdk,
        InMemory
    }

    private sealed record ServiceBusRegistrationMarker(ServiceBusRegistrationMode Mode);

    private static string ToRegistrationMethodName(this ServiceBusRegistrationMode mode) => mode switch
    {
        ServiceBusRegistrationMode.Sdk => nameof(AddAzureServiceBusSdk) + "()",
        ServiceBusRegistrationMode.InMemory => nameof(AddAzureServiceBusInMemory) + "()",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Service Bus registration mode.")
    };
}
