using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public interface IPocService
{
    Task<List<PocEntry>> GetAllAsync(CancellationToken ct = default);
    Task<List<PocEntry>> GetVisibleForUserAsync(string userOid, CancellationToken ct = default);
    Task<PocEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PocEntry> AddAsync(PocEntry entry, CancellationToken ct = default);
    Task<PocEntry?> UpdateAsync(Guid id, Action<PocEntry> mutate, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> ReorderAsync(IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
