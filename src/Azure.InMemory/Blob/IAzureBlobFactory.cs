namespace Forgate.Azure.InMemory.Blob;

public interface IAzureBlobFactory
{
    IAzureBlobContainer GetContainer(string containerName);
}

public interface IAzureBlobContainer
{
    IAzureBlobClient GetBlobClient(string blobName);
}

public interface IAzureBlobClient
{
    Task UploadAsync(
        BinaryData content,
        bool overwrite = false,
        string? contentType = null,
        CancellationToken cancellationToken = default);

    Task<AzureBlobDownloadResult?> DownloadAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
}

public sealed record AzureBlobDownloadResult(BinaryData Content, string? ContentType);
