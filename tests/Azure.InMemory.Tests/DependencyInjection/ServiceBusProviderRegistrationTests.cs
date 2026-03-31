using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.ServiceBus;
using Forgate.Azure.InMemory.ServiceBus.InMemory;
using Forgate.Azure.InMemory.ServiceBus.Sdk;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;

namespace Forgate.Azure.InMemory.Tests.DependencyInjection;

public sealed class ServiceBusProviderRegistrationTests
{
    [Fact]
    public void AddAzureServiceBusSdkResolvesTheSdkFactoryAgainstDiRegisteredAzureClients()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ServiceBusClient>(new TestServiceBusClient());
        services.AddSingleton<ServiceBusAdministrationClient>(new TestServiceBusAdministrationClient());

        services.AddAzureServiceBusSdk();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAzureServiceBusFactory>();

        Assert.IsType<AzureServiceBusSdkFactory>(factory);
    }

    [Fact]
    public void AddAzureServiceBusInMemoryResolvesTheInMemoryFactoryAndSharedStateRoot()
    {
        var services = new ServiceCollection();
        services.AddAzureServiceBusInMemory();

        using var provider = services.BuildServiceProvider();
        var factory = Assert.IsType<InMemoryServiceBusFactory>(provider.GetRequiredService<IAzureServiceBusFactory>());
        var resolvedAgain = provider.GetRequiredService<IAzureServiceBusFactory>();
        var state = provider.GetRequiredService<InMemoryServiceBusState>();
        var stateAgain = provider.GetRequiredService<InMemoryServiceBusState>();

        Assert.IsType<InMemoryServiceBusFactory>(resolvedAgain);
        Assert.Same(factory, resolvedAgain);
        Assert.Same(state, stateAgain);
        Assert.Same(state, factory.State);
    }

    [Fact]
    public void AddAzureServiceBusSdkWithoutAServiceBusClientFailsWithActionableText()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ServiceBusAdministrationClient>(new TestServiceBusAdministrationClient());
        services.AddAzureServiceBusSdk();

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IAzureServiceBusFactory>());

        Assert.Contains("AddAzureServiceBusSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ServiceBusClient), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAzureServiceBusSdkWithoutAServiceBusAdministrationClientFailsWithActionableText()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ServiceBusClient>(new TestServiceBusClient());
        services.AddAzureServiceBusSdk();

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IAzureServiceBusFactory>());

        Assert.Contains("AddAzureServiceBusSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ServiceBusAdministrationClient), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingRegistrationsFailFastWithActionableMessage()
    {
        var services = new ServiceCollection();
        services.AddAzureServiceBusSdk();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureServiceBusInMemory());

        Assert.Contains("AddAzureServiceBusSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddAzureServiceBusInMemory()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Choose exactly one Service Bus backend", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestServiceBusClient : ServiceBusClient;

    private sealed class TestServiceBusAdministrationClient : ServiceBusAdministrationClient;
}
