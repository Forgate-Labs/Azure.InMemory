using Azure;
using Azure.Core;
using Forgate.Azure.InMemory.Blob;
using Forgate.Azure.InMemory.Blob.InMemory;
using Forgate.Azure.InMemory.Blob.Sdk;
using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.KeyVault;
using Forgate.Azure.InMemory.KeyVault.InMemory;
using Forgate.Azure.InMemory.KeyVault.Sdk;
using Forgate.Azure.InMemory.ServiceBus;
using Forgate.Azure.InMemory.ServiceBus.InMemory;
using Forgate.Azure.InMemory.ServiceBus.Sdk;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace Forgate.Azure.InMemory.Tests.DependencyInjection;

public sealed class MixedProviderCompositionTests
{
    [Fact]
    public void AddAzureRegistrationsSupportsServiceBusInMemoryBlobSdkAndKeyVaultInMemoryInOneServiceCollection()
    {
        var services = CreateServiceCollectionWithAllSdkClients();

        services.AddAzureServiceBusInMemory();
        services.AddAzureBlobSdk();
        services.AddAzureKeyVaultInMemory();

        using var provider = services.BuildServiceProvider();

        var serviceBusFactory = Assert.IsType<InMemoryServiceBusFactory>(provider.GetRequiredService<IAzureServiceBusFactory>());
        var blobFactory = provider.GetRequiredService<IAzureBlobFactory>();
        var keyVaultFactory = Assert.IsType<InMemoryKeyVaultFactory>(provider.GetRequiredService<IAzureKeyVaultFactory>());
        var serviceBusState = provider.GetRequiredService<InMemoryServiceBusState>();
        var keyVaultState = provider.GetRequiredService<InMemoryKeyVaultState>();

        Assert.IsType<AzureBlobSdkFactory>(blobFactory);
        Assert.Same(serviceBusState, serviceBusFactory.State);
        Assert.Same(keyVaultState, keyVaultFactory.State);
        Assert.Null(provider.GetService<InMemoryBlobState>());
    }

    [Fact]
    public void AddAzureRegistrationsSupportsServiceBusSdkBlobInMemoryAndKeyVaultSdkInOneServiceCollection()
    {
        var services = CreateServiceCollectionWithAllSdkClients();

        services.AddAzureServiceBusSdk();
        services.AddAzureBlobInMemory();
        services.AddAzureKeyVaultSdk();

        using var provider = services.BuildServiceProvider();

        var serviceBusFactory = provider.GetRequiredService<IAzureServiceBusFactory>();
        var blobFactory = Assert.IsType<InMemoryBlobFactory>(provider.GetRequiredService<IAzureBlobFactory>());
        var keyVaultFactory = provider.GetRequiredService<IAzureKeyVaultFactory>();
        var blobState = provider.GetRequiredService<InMemoryBlobState>();

        Assert.IsType<AzureServiceBusSdkFactory>(serviceBusFactory);
        Assert.Same(blobState, blobFactory.State);
        Assert.IsType<AzureKeyVaultSdkFactory>(keyVaultFactory);
        Assert.Null(provider.GetService<InMemoryServiceBusState>());
        Assert.Null(provider.GetService<InMemoryKeyVaultState>());
    }

    [Fact]
    public void ServiceBusConflictsStillFailAfterBlobAndKeyVaultChooseDifferentBackends()
    {
        var services = CreateServiceCollectionWithAllSdkClients();
        services.AddAzureBlobInMemory();
        services.AddAzureKeyVaultSdk();
        services.AddAzureServiceBusSdk();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureServiceBusInMemory());

        Assert.Contains("AddAzureServiceBusSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddAzureServiceBusInMemory()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Choose exactly one Service Bus backend", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BlobConflictsStillFailAfterServiceBusAndKeyVaultChooseDifferentBackends()
    {
        var services = CreateServiceCollectionWithAllSdkClients();
        services.AddAzureServiceBusInMemory();
        services.AddAzureKeyVaultSdk();
        services.AddAzureBlobSdk();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureBlobInMemory());

        Assert.Contains("AddAzureBlobSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddAzureBlobInMemory()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Choose exactly one Blob backend", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyVaultConflictsStillFailAfterServiceBusAndBlobChooseDifferentBackends()
    {
        var services = CreateServiceCollectionWithAllSdkClients();
        services.AddAzureServiceBusSdk();
        services.AddAzureBlobInMemory();
        services.AddAzureKeyVaultSdk();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureKeyVaultInMemory());

        Assert.Contains("AddAzureKeyVaultSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddAzureKeyVaultInMemory()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Choose exactly one Key Vault backend", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceCollection CreateServiceCollectionWithAllSdkClients()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ServiceBusClient>(new TestServiceBusClient());
        services.AddSingleton<ServiceBusAdministrationClient>(new TestServiceBusAdministrationClient());
        services.AddSingleton(new BlobServiceClient(new Uri("https://unit-tests.example.com/test-account"), new AzureSasCredential("sig")));
        services.AddSingleton(new SecretClient(new Uri("https://unit-tests.vault.azure.net/"), new TestTokenCredential()));
        return services;
    }

    private sealed class TestServiceBusClient : ServiceBusClient;

    private sealed class TestServiceBusAdministrationClient : ServiceBusAdministrationClient;

    private sealed class TestTokenCredential : TokenCredential
    {
        private static readonly AccessToken AccessToken = new("unit-test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => AccessToken;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AccessToken);
    }
}
