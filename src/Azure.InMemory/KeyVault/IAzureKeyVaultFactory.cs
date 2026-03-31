namespace Forgate.Azure.InMemory.KeyVault;

public interface IAzureKeyVaultFactory
{
    IAzureKeyVaultSecretStore GetSecretStore();
}

public interface IAzureKeyVaultSecretStore
{
    Task<AzureKeyVaultSecretRecord> SetSecretAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default);

    Task<AzureKeyVaultSecretRecord?> GetSecretAsync(
        string name,
        CancellationToken cancellationToken = default);
}

public sealed record AzureKeyVaultSecretRecord(string Name, string Value, string? Version = null);
