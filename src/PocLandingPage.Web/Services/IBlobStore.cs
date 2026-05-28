namespace PocLandingPage.Web.Services;

public record BlobReadResult(byte[] Content, string ETag);

public class BlobConcurrencyException : Exception
{
    public BlobConcurrencyException(string message) : base(message) { }
}

public interface IBlobStore
{
    Task<BlobReadResult?> ReadAsync(CancellationToken ct = default);

    Task<string> WriteAsync(byte[] content, string? expectedETag, CancellationToken ct = default);
}
