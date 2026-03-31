using System.Collections.Concurrent;

namespace Azure.InMemory.Blob.InMemory;

public sealed class InMemoryBlobState
{
    private readonly ConcurrentDictionary<string, InMemoryBlobContainerState> _containers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ContainerNames => _containers.Keys.OrderBy(static name => name).ToArray();

    public bool ContainerExists(string containerName) =>
        _containers.ContainsKey(ValidateName(containerName, nameof(containerName)));

    public bool BlobExists(string containerName, string blobName)
    {
        containerName = ValidateName(containerName, nameof(containerName));
        blobName = ValidateName(blobName, nameof(blobName));

        return _containers.TryGetValue(containerName, out var container)
            && container.Blobs.ContainsKey(blobName);
    }

    public AzureBlobDownloadResult? Download(string containerName, string blobName)
    {
        containerName = ValidateName(containerName, nameof(containerName));
        blobName = ValidateName(blobName, nameof(blobName));

        if (!_containers.TryGetValue(containerName, out var container)
            || !container.Blobs.TryGetValue(blobName, out var blob))
        {
            return null;
        }

        return new AzureBlobDownloadResult(Clone(blob.Content), blob.ContentType);
    }

    public void Upload(string containerName, string blobName, BinaryData content, bool overwrite, string? contentType)
    {
        containerName = ValidateName(containerName, nameof(containerName));
        blobName = ValidateName(blobName, nameof(blobName));
        ArgumentNullException.ThrowIfNull(content);

        var container = _containers.GetOrAdd(containerName, static _ => new InMemoryBlobContainerState());
        var blobEntry = new InMemoryBlobEntry(Clone(content), contentType, DateTimeOffset.UtcNow);

        if (!overwrite && !container.Blobs.TryAdd(blobName, blobEntry))
        {
            throw new InvalidOperationException(
                $"Blob '{blobName}' already exists in container '{containerName}'. Pass overwrite: true to replace the existing content.");
        }

        if (overwrite)
        {
            container.Blobs[blobName] = blobEntry;
        }
    }

    internal void EnsureContainer(string containerName)
    {
        containerName = ValidateName(containerName, nameof(containerName));
        _containers.GetOrAdd(containerName, static _ => new InMemoryBlobContainerState());
    }

    private static BinaryData Clone(BinaryData data) => BinaryData.FromBytes(data.ToArray());

    private static string ValidateName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    private sealed class InMemoryBlobContainerState
    {
        public ConcurrentDictionary<string, InMemoryBlobEntry> Blobs { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record InMemoryBlobEntry(BinaryData Content, string? ContentType, DateTimeOffset LastModified);
}
