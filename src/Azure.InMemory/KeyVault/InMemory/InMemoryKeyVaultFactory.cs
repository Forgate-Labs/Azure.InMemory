namespace Azure.InMemory.KeyVault.InMemory;

public sealed class InMemoryKeyVaultFactory : IAzureKeyVaultFactory
{
    public InMemoryKeyVaultFactory(InMemoryKeyVaultState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public InMemoryKeyVaultState State { get; }

    public IAzureKeyVaultSecretStore GetSecretStore() => new InMemoryAzureKeyVaultSecretStore(State);

    private sealed class InMemoryAzureKeyVaultSecretStore(InMemoryKeyVaultState state) : IAzureKeyVaultSecretStore
    {
        private readonly InMemoryKeyVaultState _state = state ?? throw new ArgumentNullException(nameof(state));

        public Task<AzureKeyVaultSecretRecord> SetSecretAsync(
            string name,
            string value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_state.SetSecret(name, value));
        }

        public Task<AzureKeyVaultSecretRecord?> GetSecretAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_state.GetSecret(name));
        }
    }
}
