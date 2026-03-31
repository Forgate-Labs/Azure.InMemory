namespace Azure.InMemory.Blob.InMemory;

public sealed class InMemoryBlobFactory : IAzureBlobFactory
{
    public InMemoryBlobFactory(InMemoryBlobState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public InMemoryBlobState State { get; }

    public IAzureBlobContainer GetContainer(string containerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        State.EnsureContainer(containerName);
        return new InMemoryBlobContainer(State, containerName);
    }

    private sealed class InMemoryBlobContainer(
        InMemoryBlobState state,
        string containerName) : IAzureBlobContainer
    {
        private readonly InMemoryBlobState _state = state ?? throw new ArgumentNullException(nameof(state));
        private readonly string _containerName = string.IsNullOrWhiteSpace(containerName)
            ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName))
            : containerName;

        public IAzureBlobClient GetBlobClient(string blobName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
            return new InMemoryAzureBlobClient(_state, _containerName, blobName);
        }
    }

    private sealed class InMemoryAzureBlobClient(
        InMemoryBlobState state,
        string containerName,
        string blobName) : IAzureBlobClient
    {
        private readonly InMemoryBlobState _state = state ?? throw new ArgumentNullException(nameof(state));
        private readonly string _containerName = string.IsNullOrWhiteSpace(containerName)
            ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(containerName))
            : containerName;
        private readonly string _blobName = string.IsNullOrWhiteSpace(blobName)
            ? throw new ArgumentException("Value cannot be null or whitespace.", nameof(blobName))
            : blobName;

        public Task UploadAsync(
            BinaryData content,
            bool overwrite = false,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _state.Upload(_containerName, _blobName, content, overwrite, contentType);
            return Task.CompletedTask;
        }

        public Task<AzureBlobDownloadResult?> DownloadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_state.Download(_containerName, _blobName));
        }

        public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_state.BlobExists(_containerName, _blobName));
        }
    }
}
