using System.Text.Json;
using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public class PocService : IPocService
{
    private readonly IBlobStore _store;
    private const int MaxRetries = 3;

    public PocService(IBlobStore store) => _store = store;

    public async Task<List<PocEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var read = await _store.ReadAsync(ct);
        if (read is null) return new List<PocEntry>();
        return JsonSerializer.Deserialize<List<PocEntry>>(read.Content) ?? new();
    }

    public async Task<List<PocEntry>> GetVisibleForUserAsync(string userOid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userOid))
            throw new ArgumentException("userOid must be provided", nameof(userOid));

        var all = await GetAllAsync(ct);
        return all.Where(p => p.IsVisibleTo(userOid)).ToList();
    }

    public async Task<PocEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(p => p.Id == id);
    }

    public Task<PocEntry> AddAsync(PocEntry entry, CancellationToken ct = default)
    {
        if (entry.Id == Guid.Empty) entry.Id = Guid.NewGuid();
        return MutateAsync(list =>
        {
            list.Add(entry);
            return entry;
        }, ct);
    }

    public Task<PocEntry?> UpdateAsync(Guid id, Action<PocEntry> mutate, CancellationToken ct = default) =>
        MutateAsync<PocEntry?>(list =>
        {
            var found = list.FirstOrDefault(p => p.Id == id);
            if (found is null) return null;
            mutate(found);
            return found;
        }, ct);

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        MutateAsync(list =>
        {
            var found = list.FirstOrDefault(p => p.Id == id);
            if (found is null) return false;
            list.Remove(found);
            return true;
        }, ct);

    public Task<bool> ReorderAsync(IReadOnlyList<Guid> orderedIds, CancellationToken ct = default) =>
        MutateAsync(list =>
        {
            // Only accept a reorder that references exactly the current set of POCs,
            // so a stale/partial request can't drop or duplicate entries.
            var current = list.Select(p => p.Id).ToHashSet();
            if (orderedIds.Count != current.Count || !orderedIds.ToHashSet().SetEquals(current))
                return false;

            var byId = list.ToDictionary(p => p.Id);
            list.Clear();
            foreach (var id in orderedIds)
                list.Add(byId[id]);
            return true;
        }, ct);

    private async Task<T> MutateAsync<T>(Func<List<PocEntry>, T> mutate, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var read = await _store.ReadAsync(ct);
            var list = read is null
                ? new List<PocEntry>()
                : JsonSerializer.Deserialize<List<PocEntry>>(read.Content) ?? new();

            var result = mutate(list);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(list,
                new JsonSerializerOptions { WriteIndented = true });

            try
            {
                await _store.WriteAsync(bytes, read?.ETag, ct);
                return result;
            }
            catch (BlobConcurrencyException)
            {
                if (attempt == MaxRetries - 1) throw;
            }
        }
        throw new InvalidOperationException("unreachable");
    }
}
