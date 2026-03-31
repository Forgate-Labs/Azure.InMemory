using Azure.InMemory.Blob;
using Azure.InMemory.Blob.InMemory;
using Azure.InMemory.Blob.Sdk;
using Azure.InMemory.DependencyInjection;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.InMemory.Tests.DependencyInjection;

public sealed class BlobProviderRegistrationTests
{
    [Fact]
    public void AddAzureBlobSdkResolvesTheSdkFactoryAgainstDiRegisteredBlobServiceClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BlobServiceClient(new Uri("https://unit-tests.example.com/test-account"), new AzureSasCredential("sig")));

        services.AddAzureBlobSdk();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();

        Assert.IsType<AzureBlobSdkFactory>(factory);
    }

    [Fact]
    public void AddAzureBlobInMemoryResolvesTheInMemoryFactoryAndSharedStateRoot()
    {
        var services = new ServiceCollection();
        services.AddAzureBlobInMemory();

        using var provider = services.BuildServiceProvider();
        var factory = Assert.IsType<InMemoryBlobFactory>(provider.GetRequiredService<IAzureBlobFactory>());
        var resolvedAgain = provider.GetRequiredService<IAzureBlobFactory>();
        var state = provider.GetRequiredService<InMemoryBlobState>();
        var stateAgain = provider.GetRequiredService<InMemoryBlobState>();

        Assert.IsType<InMemoryBlobFactory>(resolvedAgain);
        Assert.Same(factory, resolvedAgain);
        Assert.Same(state, stateAgain);
        Assert.Same(state, factory.State);
    }

    [Fact]
    public void AddAzureBlobSdkWithoutABlobServiceClientFailsWithActionableText()
    {
        var services = new ServiceCollection();
        services.AddAzureBlobSdk();

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IAzureBlobFactory>());

        Assert.Contains("AddAzureBlobSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(BlobServiceClient), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingRegistrationsFailFastWithActionableMessage()
    {
        var services = new ServiceCollection();
        services.AddAzureBlobSdk();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureBlobInMemory());

        Assert.Contains("AddAzureBlobSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddAzureBlobInMemory()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Choose exactly one Blob backend", exception.Message, StringComparison.Ordinal);
    }
}
