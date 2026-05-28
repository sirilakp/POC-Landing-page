using Microsoft.Graph;

namespace PocLandingPage.Web.Services;

public class GraphUserLookup : IGraphUserLookup
{
    private readonly GraphServiceClient _graph;

    public GraphUserLookup(GraphServiceClient graph) => _graph = graph;

    public async Task<UserLookup?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var user = await _graph.Users[email].GetAsync(req =>
            {
                req.QueryParameters.Select = new[] { "id", "mail", "userPrincipalName", "displayName" };
            }, ct);
            return user is null ? null : Map(user);
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
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
