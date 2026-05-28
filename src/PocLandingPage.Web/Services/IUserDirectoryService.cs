using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public record UserLookup(string Oid, string Email, string DisplayName);

public interface IGraphUserLookup
{
    Task<UserLookup?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<UserLookup>> GetByOidsAsync(IEnumerable<string> oids, CancellationToken ct = default);
}

public record ResolveEmailsResult(List<string> Oids, List<string> Unresolved);

public interface IUserDirectoryService
{
    Task<string?> ResolveEmailToOidAsync(string email, CancellationToken ct = default);
    Task<ResolveEmailsResult> ResolveEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default);
    Task<Dictionary<string, UserSummary>> GetUsersByOidAsync(IEnumerable<string> oids, CancellationToken ct = default);
}
