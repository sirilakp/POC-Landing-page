using Microsoft.Extensions.Caching.Memory;
using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public class UserDirectoryService : IUserDirectoryService
{
    private readonly IGraphUserLookup _graph;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public UserDirectoryService(IGraphUserLookup graph, IMemoryCache cache)
    {
        _graph = graph;
        _cache = cache;
    }

    public async Task<string?> ResolveEmailToOidAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var key = $"email:{email.Trim().ToLowerInvariant()}";

        if (_cache.TryGetValue<UserLookup>(key, out var hit) && hit is not null)
            return hit.Oid;

        var user = await _graph.FindByEmailAsync(email, ct);
        if (user is null) return null;

        _cache.Set(key, user, CacheTtl);
        _cache.Set($"oid:{user.Oid}", user, CacheTtl);
        return user.Oid;
    }

    public async Task<ResolveEmailsResult> ResolveEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default)
    {
        var oids = new List<string>();
        var unresolved = new List<string>();
        foreach (var email in emails.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var oid = await ResolveEmailToOidAsync(email, ct);
            if (oid is null) unresolved.Add(email);
            else oids.Add(oid);
        }
        return new ResolveEmailsResult(oids, unresolved);
    }

    public async Task<Dictionary<string, UserSummary>> GetUsersByOidAsync(IEnumerable<string> oids, CancellationToken ct = default)
    {
        var result = new Dictionary<string, UserSummary>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var oid in oids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_cache.TryGetValue<UserLookup>($"oid:{oid}", out var cached) && cached is not null)
            {
                result[oid] = new UserSummary { Email = cached.Email, DisplayName = cached.DisplayName };
            }
            else
            {
                missing.Add(oid);
            }
        }

        if (missing.Count > 0)
        {
            var fetched = await _graph.GetByOidsAsync(missing, ct);
            foreach (var u in fetched)
            {
                _cache.Set($"oid:{u.Oid}", u, CacheTtl);
                _cache.Set($"email:{u.Email.ToLowerInvariant()}", u, CacheTtl);
                result[u.Oid] = new UserSummary { Email = u.Email, DisplayName = u.DisplayName };
            }
        }

        return result;
    }
}
