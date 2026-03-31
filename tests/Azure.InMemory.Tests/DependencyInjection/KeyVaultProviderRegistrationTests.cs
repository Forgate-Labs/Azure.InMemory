using Azure;
using Azure.Core;
using Forgate.Azure.InMemory.DependencyInjection;
using Forgate.Azure.InMemory.KeyVault;
using Forgate.Azure.InMemory.KeyVault.InMemory;
using Forgate.Azure.InMemory.KeyVault.Sdk;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Forgate.Azure.InMemory.Tests.DependencyInjection;

public sealed class KeyVaultProviderRegistrationTests
{
    [Fact]
    public void AddAzureKeyVaultSdkResolvesTheSdkFactoryAgainstDiRegisteredSecretClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new SecretClient(new Uri("https://unit-tests.vault.azure.net/"), new TestTokenCredential()));

        services.AddAzureKeyVaultSdk();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAzureKeyVaultFactory>();

        Assert.IsType<AzureKeyVaultSdkFactory>(factory);
    }

    [Fact]
    public void AddAzureKeyVaultInMemoryResolvesTheInMemoryFactoryAndSharedStateRoot()
    {
        var services = new ServiceCollection();
        services.AddAzureKeyVaultInMemory();

        using var provider = services.BuildServiceProvider();
        var factory = Assert.IsType<InMemoryKeyVaultFactory>(provider.GetRequiredService<IAzureKeyVaultFactory>());
        var resolvedAgain = provider.GetRequiredService<IAzureKeyVaultFactory>();
        var state = provider.GetRequiredService<InMemoryKeyVaultState>();
        var stateAgain = provider.GetRequiredService<InMemoryKeyVaultState>();

        Assert.IsType<InMemoryKeyVaultFactory>(resolvedAgain);
        Assert.Same(factory, resolvedAgain);
        Assert.Same(state, stateAgain);
        Assert.Same(state, factory.State);
    }

    [Fact]
    public void AddAzureKeyVaultSdkWithoutASecretClientFailsWithActionableText()
    {
        var services = new ServiceCollection();
        services.AddAzureKeyVaultSdk();

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IAzureKeyVaultFactory>());

        Assert.Contains("AddAzureKeyVaultSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SecretClient), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingRegistrationsFailFastWithActionableMessage()
    {
        var services = new ServiceCollection();
        services.AddAzureKeyVaultSdk();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAzureKeyVaultInMemory());

        Assert.Contains("AddAzureKeyVaultSdk()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddAzureKeyVaultInMemory()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Choose exactly one Key Vault backend", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestTokenCredential : TokenCredential
    {
        private static readonly AccessToken AccessToken = new("unit-test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => AccessToken;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AccessToken);
    }
}
