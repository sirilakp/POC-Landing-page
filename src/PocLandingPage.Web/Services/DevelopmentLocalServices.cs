using System.Text.Json;
using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public class DevelopmentBlobStore : IBlobStore
{
    private readonly object _sync = new();
    private byte[]? _content;
    private string? _etag;
    private int _version;

    public Task<BlobReadResult?> ReadAsync(CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (_content is null)
            {
                var seed = JsonSerializer.SerializeToUtf8Bytes(new List<PocEntry>());
                _content = seed;
                _etag = "\"v1\"";
                _version = 1;
            }

            return Task.FromResult<BlobReadResult?>(new BlobReadResult(_content.ToArray(), _etag!));
        }
    }

    public Task<string> WriteAsync(byte[] content, string? expectedETag, CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (_content is null && expectedETag is not null)
                throw new BlobConcurrencyException("Blob does not exist but ETag was supplied.");
            if (_content is not null && expectedETag is null)
                throw new BlobConcurrencyException("Blob exists but no ETag supplied (would overwrite).");
            if (_content is not null && expectedETag != _etag)
                throw new BlobConcurrencyException("ETag mismatch.");

            _content = content.ToArray();
            _etag = $"\"v{++_version}\"";
            return Task.FromResult(_etag);
        }
    }
}

public class DevelopmentUserDirectoryService : IUserDirectoryService
{
    public Task<string?> ResolveEmailToOidAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Task.FromResult<string?>(null);

        var normalized = email.Trim().ToLowerInvariant();
        return Task.FromResult<string?>($"local:{normalized}");
    }

    public async Task<ResolveEmailsResult> ResolveEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default)
    {
        var oids = new List<string>();

        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var oid = await ResolveEmailToOidAsync(email, ct);
            if (oid is not null)
                oids.Add(oid);
        }

        return new ResolveEmailsResult(oids, new List<string>());
    }

    public Task<Dictionary<string, UserSummary>> GetUsersByOidAsync(IEnumerable<string> oids, CancellationToken ct = default)
    {
        var users = oids
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                oid => oid,
                oid => new UserSummary
                {
                    DisplayName = $"Local User {oid[..Math.Min(8, oid.Length)]}",
                    Email = $"{oid[..Math.Min(8, oid.Length)]}@local.dev"
                },
                StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(users);
    }
}

public class DevelopmentInvitationService : IInvitationService
{
    public Task<IReadOnlyList<GuestUser>> GetGuestsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GuestUser>>(new List<GuestUser>());

    public Task InviteUserAsync(string email, string role, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UpdateRoleAsync(string userId, string newRole, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RevokeAccessAsync(string userId, CancellationToken ct = default)
        => Task.CompletedTask;
}
