using Microsoft.Graph;

namespace PocLandingPage.Web.Services;

public class GraphUserLookup : IGraphUserLookup
{
    private readonly GraphServiceClient _graph;

    public GraphUserLookup(GraphServiceClient graph) => _graph = graph;

    public async Task<UserLookup?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        // Try direct UPN/ID lookup first (fast path).
        try
        {
            var user = await _graph.Users[email].GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "displayName" };
            }, ct);
            if (user is not null) return Map(user);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404) { }

        // Fall back to $filter on mail + userPrincipalName for users whose UPN differs from their mail address.
        var escaped = email.Replace("'", "''");
        var result = await _graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter = $"mail eq '{escaped}' or userPrincipalName eq '{escaped}'";
            req.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "displayName" };
            req.QueryParameters.Top = 1;
        }, ct);

        var found = result?.Value?.FirstOrDefault();
        return found is null ? null : Map(found);
    }

    public async Task<IReadOnlyList<UserLookup>> GetByOidsAsync(IEnumerable<string> oids, CancellationToken ct = default)
    {
        var ids = oids.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<UserLookup>();

        var filter = string.Join(" or ", ids.Select(id => $"id eq '{id}'"));
        var result = await _graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter = filter;
            req.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "displayName" };
        }, ct);

        return result?.Value?.Select(Map).ToList() ?? new List<UserLookup>();
    }

    private static UserLookup Map(Microsoft.Graph.Models.User u) =>
        new(u.Id!, u.Mail ?? u.UserPrincipalName ?? "", u.DisplayName ?? "");
}
