using System.Collections.Concurrent;

namespace Azure.InMemory.KeyVault.InMemory;

public sealed class InMemoryKeyVaultState
{
    private readonly ConcurrentDictionary<string, InMemoryKeyVaultSecretEntry> _secrets =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> SecretNames => _secrets.Keys.OrderBy(static name => name).ToArray();

    public bool SecretExists(string name) => _secrets.ContainsKey(ValidateName(name, nameof(name)));

    public AzureKeyVaultSecretRecord SetSecret(string name, string value)
    {
        name = ValidateName(name, nameof(name));
        ArgumentNullException.ThrowIfNull(value);

        var record = new AzureKeyVaultSecretRecord(name, value, Guid.NewGuid().ToString("N"));
        _secrets[name] = new InMemoryKeyVaultSecretEntry(record, DateTimeOffset.UtcNow);

        return record;
    }

    public AzureKeyVaultSecretRecord? GetSecret(string name)
    {
        name = ValidateName(name, nameof(name));

        return _secrets.TryGetValue(name, out var entry)
            ? entry.Record with { }
            : null;
    }

    private static string ValidateName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    private sealed record InMemoryKeyVaultSecretEntry(
        AzureKeyVaultSecretRecord Record,
        DateTimeOffset UpdatedAt);
}
