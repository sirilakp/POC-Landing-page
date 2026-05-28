using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using PocLandingPage.Web.Options;

namespace PocLandingPage.Web.Services;

public class AzureBlobStore : IBlobStore
{
    private readonly BlobClient _client;

    public AzureBlobStore(BlobServiceClient service, IOptions<StorageOptions> opts)
    {
        var o = opts.Value;
        var container = service.GetBlobContainerClient(o.Container);
        _client = container.GetBlobClient(o.BlobName);
    }

    public async Task<BlobReadResult?> ReadAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.DownloadContentAsync(ct);
            return new BlobReadResult(
                response.Value.Content.ToArray(),
                response.Value.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<string> WriteAsync(byte[] content, string? expectedETag, CancellationToken ct = default)
    {
        var options = new BlobUploadOptions();
        if (expectedETag is not null)
        {
            options.Conditions = new BlobRequestConditions { IfMatch = new ETag(expectedETag) };
        }
        else
        {
            options.Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
        }

        try
        {
            var response = await _client.UploadAsync(BinaryData.FromBytes(content), options, ct);
            return response.Value.ETag.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            throw new BlobConcurrencyException(
                $"Blob '{_client.Name}' changed since read (ETag mismatch). Retry the operation.");
        }
    }
}
