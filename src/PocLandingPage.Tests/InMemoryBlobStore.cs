using PocLandingPage.Web.Services;

namespace PocLandingPage.Tests;

public class InMemoryBlobStore : IBlobStore
{
    private byte[]? _content;
    private string? _etag;
    private int _version;

    public int Reads { get; private set; }
    public int Writes { get; private set; }

    public Task<BlobReadResult?> ReadAsync(CancellationToken ct = default)
    {
        Reads++;
        if (_content is null) return Task.FromResult<BlobReadResult?>(null);
        return Task.FromResult<BlobReadResult?>(new BlobReadResult(_content, _etag!));
    }

    public Task<string> WriteAsync(byte[] content, string? expectedETag, CancellationToken ct = default)
    {
        Writes++;

        if (_content is null && expectedETag is not null)
            throw new BlobConcurrencyException("Blob does not exist but ETag was supplied.");
        if (_content is not null && expectedETag is null)
            throw new BlobConcurrencyException("Blob exists but no ETag supplied (would overwrite).");
        if (_content is not null && expectedETag != _etag)
            throw new BlobConcurrencyException("ETag mismatch.");

        _content = content;
        _etag = $"\"v{++_version}\"";
        return Task.FromResult(_etag);
    }

    public void SimulateExternalWrite(byte[] content)
    {
        _content = content;
        _etag = $"\"v{++_version}\"";
    }
}
