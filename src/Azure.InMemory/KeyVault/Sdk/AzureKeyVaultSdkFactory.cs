using Azure;
using Azure.Security.KeyVault.Secrets;

namespace Forgate.Azure.InMemory.KeyVault.Sdk;

public sealed class AzureKeyVaultSdkFactory : IAzureKeyVaultFactory
{
    private readonly SecretClient _secretClient;

    public AzureKeyVaultSdkFactory(SecretClient secretClient)
    {
        _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));
    }

    public IAzureKeyVaultSecretStore GetSecretStore() => new AzureKeyVaultSdkSecretStore(_secretClient);

    private sealed class AzureKeyVaultSdkSecretStore(SecretClient secretClient) : IAzureKeyVaultSecretStore
    {
        private readonly SecretClient _secretClient = secretClient ?? throw new ArgumentNullException(nameof(secretClient));

        public async Task<AzureKeyVaultSecretRecord> SetSecretAsync(
            string name,
            string value,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(value);

            var response = await _secretClient.SetSecretAsync(name, value, cancellationToken).ConfigureAwait(false);
            return ToRecord(response.Value);
        }

        public async Task<AzureKeyVaultSecretRecord?> GetSecretAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            try
            {
                var response = await _secretClient.GetSecretAsync(name, cancellationToken: cancellationToken).ConfigureAwait(false);
                return ToRecord(response.Value);
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                return null;
            }
        }

        private static AzureKeyVaultSecretRecord ToRecord(KeyVaultSecret secret) =>
            new(secret.Name, secret.Value, secret.Properties.Version);
    }
}
