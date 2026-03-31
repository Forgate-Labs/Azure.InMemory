using System.Text;
using Azure.InMemory.Blob;
using Azure.InMemory.Blob.InMemory;
using Azure.InMemory.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.InMemory.Tests.Blob.InMemory;

public sealed class InMemoryBlobBehaviorTests
{
    [Fact]
    public async Task BlobRoundTripThroughResolvedFactoryPreservesContainerStateContentAndContentType()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var state = provider.GetRequiredService<InMemoryBlobState>();

        Assert.False(state.ContainerExists("reports"));
        Assert.Empty(state.ContainerNames);

        var container = factory.GetContainer("reports");

        Assert.True(state.ContainerExists("reports"));
        Assert.Equal(["reports"], state.ContainerNames);

        var blob = container.GetBlobClient("daily.json");
        var payload = BinaryData.FromString("{\"status\":\"ok\"}");

        await blob.UploadAsync(payload, overwrite: false, contentType: "application/json");

        var exists = await blob.ExistsAsync();
        var download = await blob.DownloadAsync();

        Assert.True(exists);
        Assert.NotNull(download);
        Assert.True(state.BlobExists("reports", "daily.json"));
        Assert.Equal(payload.ToString(), download!.Content.ToString());
        Assert.Equal("application/json", download.ContentType);
    }

    [Fact]
    public async Task MissingBlobReturnsFalseAndNullWithoutSynthesizingBlobState()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var state = provider.GetRequiredService<InMemoryBlobState>();
        var blob = factory.GetContainer("reports").GetBlobClient("missing.json");

        var exists = await blob.ExistsAsync();
        var download = await blob.DownloadAsync();

        Assert.False(exists);
        Assert.Null(download);
        Assert.True(state.ContainerExists("reports"));
        Assert.False(state.BlobExists("reports", "missing.json"));
        Assert.Equal(["reports"], state.ContainerNames);
    }

    [Fact]
    public async Task DuplicateUploadWithoutOverwriteFailsActionablyAndOverwriteReplacesContentAndContentType()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var blob = factory.GetContainer("reports").GetBlobClient("daily.json");

        await blob.UploadAsync(BinaryData.FromString("first version"), overwrite: false, contentType: "text/plain");

        var duplicateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            blob.UploadAsync(BinaryData.FromString("second version"), overwrite: false, contentType: "application/json"));

        Assert.Contains("daily.json", duplicateException.Message, StringComparison.Ordinal);
        Assert.Contains("reports", duplicateException.Message, StringComparison.Ordinal);
        Assert.Contains("overwrite: true", duplicateException.Message, StringComparison.Ordinal);

        await blob.UploadAsync(BinaryData.FromString("second version"), overwrite: true, contentType: "application/json");

        var replacement = await blob.DownloadAsync();

        Assert.NotNull(replacement);
        Assert.Equal("second version", replacement!.Content.ToString());
        Assert.Equal("application/json", replacement.ContentType);
    }

    [Fact]
    public async Task ContainerAndBlobLookupAreCaseInsensitiveAndPreserveOneLogicalBlobIdentity()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var state = provider.GetRequiredService<InMemoryBlobState>();
        var originalBlob = factory.GetContainer("Reports").GetBlobClient("Daily.JSON");

        await originalBlob.UploadAsync(BinaryData.FromString("payload"), overwrite: false, contentType: "text/plain");

        var lookedUpByDifferentCase = factory.GetContainer("reports").GetBlobClient("daily.json");
        var exists = await lookedUpByDifferentCase.ExistsAsync();
        var download = await lookedUpByDifferentCase.DownloadAsync();
        var logicalContainerName = Assert.Single(state.ContainerNames);

        Assert.True(exists);
        Assert.NotNull(download);
        Assert.Equal("payload", download!.Content.ToString());
        Assert.True(state.ContainerExists("REPORTS"));
        Assert.True(state.BlobExists("reports", "daily.json"));
        Assert.True(state.BlobExists("REPORTS", "DAILY.JSON"));
        Assert.Equal("Reports", logicalContainerName, ignoreCase: false);
    }

    [Fact]
    public async Task UploadAndDownloadUseClonedSnapshotsSoInputBuffersAndEarlierReadsStayStable()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var blob = factory.GetContainer("snapshots").GetBlobClient("payload.txt");
        var sourceBytes = Encoding.UTF8.GetBytes("alpha");

        await blob.UploadAsync(BinaryData.FromBytes(sourceBytes), overwrite: false, contentType: "text/plain");
        sourceBytes[0] = (byte)'z';

        var firstDownload = await blob.DownloadAsync();

        Assert.NotNull(firstDownload);
        Assert.Equal("alpha", firstDownload!.Content.ToString());
        Assert.Equal("text/plain", firstDownload.ContentType);

        await blob.UploadAsync(BinaryData.FromString("beta"), overwrite: true, contentType: "application/octet-stream");

        var secondDownload = await blob.DownloadAsync();

        Assert.NotNull(secondDownload);
        Assert.NotSame(firstDownload.Content, secondDownload!.Content);
        Assert.Equal("alpha", firstDownload.Content.ToString());
        Assert.Equal("text/plain", firstDownload.ContentType);
        Assert.Equal("beta", secondDownload.Content.ToString());
        Assert.Equal("application/octet-stream", secondDownload.ContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void GetContainerRejectsBlankContainerNames(string containerName)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();

        var exception = Assert.Throws<ArgumentException>(() =>
            factory.GetContainer(containerName));

        Assert.Contains("containerName", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void GetBlobClientRejectsBlankBlobNames(string blobName)
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var container = factory.GetContainer("reports");

        var exception = Assert.Throws<ArgumentException>(() =>
            container.GetBlobClient(blobName));

        Assert.Contains("blobName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadRejectsNullContent()
    {
        using var provider = CreateServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobFactory>();
        var blob = factory.GetContainer("reports").GetBlobClient("daily.json");

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            blob.UploadAsync(null!));

        Assert.Equal("content", exception.ParamName);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureBlobInMemory();
        return services.BuildServiceProvider();
    }
}
