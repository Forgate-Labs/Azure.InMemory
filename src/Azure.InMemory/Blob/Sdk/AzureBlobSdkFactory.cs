using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Forgate.Azure.InMemory.Blob.Sdk;

public sealed class AzureBlobSdkFactory : IAzureBlobFactory
{
    private readonly BlobServiceClient _serviceClient;

    public AzureBlobSdkFactory(BlobServiceClient serviceClient)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
    }

    public IAzureBlobContainer GetContainer(string containerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
        return new AzureBlobSdkContainer(_serviceClient.GetBlobContainerClient(containerName));
    }

    private sealed class AzureBlobSdkContainer : IAzureBlobContainer
    {
        private readonly BlobContainerClient _containerClient;

        public AzureBlobSdkContainer(BlobContainerClient containerClient)
        {
            _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        }

        public IAzureBlobClient GetBlobClient(string blobName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
            return new AzureBlobSdkBlobClient(_containerClient.GetBlobClient(blobName));
        }
    }

    private sealed class AzureBlobSdkBlobClient : IAzureBlobClient
    {
        private readonly BlobClient _blobClient;

        public AzureBlobSdkBlobClient(BlobClient blobClient)
        {
            _blobClient = blobClient ?? throw new ArgumentNullException(nameof(blobClient));
        }

        public async Task UploadAsync(
            BinaryData content,
            bool overwrite = false,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);

            var options = new BlobUploadOptions
            {
                HttpHeaders = string.IsNullOrWhiteSpace(contentType)
                    ? null
                    : new BlobHttpHeaders { ContentType = contentType },
                Conditions = overwrite
                    ? null
                    : new BlobRequestConditions { IfNoneMatch = ETag.All }
            };

            using var stream = content.ToStream();
            await _blobClient.UploadAsync(stream, options, cancellationToken).ConfigureAwait(false);
        }

        public async Task<AzureBlobDownloadResult?> DownloadAsync(CancellationToken cancellationToken = default)
        {
            if (!await ExistsAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var download = await _blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return new AzureBlobDownloadResult(download.Value.Content, download.Value.Details.ContentType);
        }

        public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
            await _blobClient.ExistsAsync(cancellationToken).ConfigureAwait(false);
    }
}
