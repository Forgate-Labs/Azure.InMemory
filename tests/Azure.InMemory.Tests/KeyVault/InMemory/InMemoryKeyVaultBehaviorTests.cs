using Azure.InMemory.DependencyInjection;
using Azure.InMemory.KeyVault;
using Azure.InMemory.KeyVault.InMemory;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.InMemory.Tests.KeyVault.InMemory;

public sealed class InMemoryKeyVaultBehaviorTests
{
    [Fact]
    public async Task SecretStoreRoundTripsASecretThroughTheResolvedFactoryAndSharedState()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureKeyVaultFactory>();
        var state = provider.GetRequiredService<InMemoryKeyVaultState>();
        var store = factory.GetSecretStore();

        Assert.False(state.SecretExists("api-key"));
        Assert.Empty(state.SecretNames);

        var stored = await store.SetSecretAsync("api-key", "super-secret-value");
        var retrieved = await store.GetSecretAsync("api-key");

        Assert.Equal("api-key", stored.Name);
        Assert.Equal("super-secret-value", stored.Value);
        Assert.False(string.IsNullOrWhiteSpace(stored.Version));
        Assert.NotNull(retrieved);
        Assert.Equal(stored, retrieved);
        Assert.True(state.SecretExists("api-key"));
        Assert.Equal(["api-key"], state.SecretNames);
    }

    [Fact]
    public async Task MissingSecretLookupReturnsNullWithoutSynthesizingPlaceholderState()
    {
        using var provider = CreateServiceProvider();
        var state = provider.GetRequiredService<InMemoryKeyVaultState>();
        var store = provider.GetRequiredService<IAzureKeyVaultFactory>().GetSecretStore();

        var missing = await store.GetSecretAsync("missing-secret");

        Assert.Null(missing);
        Assert.False(state.SecretExists("missing-secret"));
        Assert.Empty(state.SecretNames);
    }

    [Fact]
    public async Task OverwritingTheSameLogicalSecretIsCaseInsensitiveAndReturnsANewLatestVersion()
    {
        using var provider = CreateServiceProvider();
        var state = provider.GetRequiredService<InMemoryKeyVaultState>();
        var store = provider.GetRequiredService<IAzureKeyVaultFactory>().GetSecretStore();

        var first = await store.SetSecretAsync("Api-Key", "first-secret-value");
        var second = await store.SetSecretAsync("api-key", "latest-secret-value");
        var retrieved = await store.GetSecretAsync("API-KEY");
        var logicalName = Assert.Single(state.SecretNames);

        Assert.NotEqual(first.Version, second.Version);
        Assert.False(string.IsNullOrWhiteSpace(first.Version));
        Assert.False(string.IsNullOrWhiteSpace(second.Version));
        Assert.NotNull(retrieved);
        Assert.Equal(second, retrieved);
        Assert.Equal("latest-secret-value", retrieved.Value);
        Assert.True(state.SecretExists("Api-Key"));
        Assert.True(state.SecretExists("api-key"));
        Assert.Equal("api-key", second.Name);
        Assert.Equal("Api-Key", logicalName, ignoreCase: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task GetSecretRejectsBlankSecretNames(string secretName)
    {
        using var provider = CreateServiceProvider();
        var store = provider.GetRequiredService<IAzureKeyVaultFactory>().GetSecretStore();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.GetSecretAsync(secretName));

        Assert.Contains("name", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task SetSecretRejectsBlankSecretNames(string secretName)
    {
        using var provider = CreateServiceProvider();
        var store = provider.GetRequiredService<IAzureKeyVaultFactory>().GetSecretStore();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SetSecretAsync(secretName, "super-secret-value"));

        Assert.Contains("name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetSecretRejectsNullSecretValues()
    {
        using var provider = CreateServiceProvider();
        var store = provider.GetRequiredService<IAzureKeyVaultFactory>().GetSecretStore();

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SetSecretAsync("api-key", null!));

        Assert.Equal("value", exception.ParamName);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureKeyVaultInMemory();
        return services.BuildServiceProvider();
    }
}
