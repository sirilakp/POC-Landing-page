using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using PocLandingPage.Web.Models;
using PocLandingPage.Web.Options;

namespace PocLandingPage.Web.Services;

public class InvitationService : IInvitationService
{
    private readonly GraphServiceClient _graph;
    private readonly string _spId;
    private readonly string _redirectUrl;

    public InvitationService(
        GraphServiceClient graph,
        IOptions<AzureAdOptions> ad,
        IConfiguration config)
    {
        _graph = graph;
        _spId = ad.Value.ServicePrincipalId;
        _redirectUrl = config["App:BaseUrl"] ?? "https://localhost";
    }

    public async Task<IReadOnlyList<GuestUser>> GetGuestsAsync(CancellationToken ct = default)
    {
        var assignmentsTask = _graph.ServicePrincipals[_spId].AppRoleAssignedTo.GetAsync(cancellationToken: ct);
        var spTask = _graph.ServicePrincipals[_spId].GetAsync(cancellationToken: ct);
        await Task.WhenAll(assignmentsTask, spTask);

        var sp = await spTask;
        var roleMap = sp?.AppRoles?.ToDictionary(r => r.Id!.Value, r => r.Value!) ?? new();

        // Pick the first assignment that maps to a real POC role; fall back to whatever
        // assignment exists (e.g. the all-zeros "default access" role Graph adds automatically).
        var assignmentLookup = ((await assignmentsTask)?.Value ?? new List<AppRoleAssignment>())
            .Where(a => a.PrincipalId is not null)
            .GroupBy(a => a.PrincipalId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(a => a.AppRoleId is { } id && roleMap.ContainsKey(id))
                     ?? g.First());

        if (assignmentLookup.Count == 0)
            return Array.Empty<GuestUser>();

        // Bulk-fetch profiles for all assigned principals (members + guests).
        var oidFilter = string.Join(" or ", assignmentLookup.Keys.Select(id => $"id eq '{id}'"));
        var users = await _graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter = oidFilter;
            req.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "externalUserState", "userType" };
        }, ct);

        return (users?.Value ?? new List<User>())
            .Where(u => u.Id is not null)
            .Select(u =>
            {
                string? role = null;
                if (Guid.TryParse(u.Id, out var oid)
                    && assignmentLookup.TryGetValue(oid, out var a)
                    && a.AppRoleId is { } roleId
                    && roleMap.TryGetValue(roleId, out var roleName))
                {
                    role = roleName;
                }

                return new GuestUser
                {
                    Id = u.Id!,
                    DisplayName = u.DisplayName ?? "",
                    Email = ResolveEmail(u),
                    Role = role ?? "Default access",
                    InviteStatus = u.UserType == "Member" ? "Member" : u.ExternalUserState,
                    IsExternal = u.UserType == "Guest",
                };
            })
            .OrderBy(u => u.DisplayName)
            .ToList();
    }

    // B2B guest UPNs look like user_domain.com#EXT#@tenant.onmicrosoft.com when mail is null.
    // The last underscore before #EXT# represents the @ in the original email address.
    private static string ResolveEmail(User u)
    {
        if (!string.IsNullOrEmpty(u.Mail))
            return u.Mail;
        var upn = u.UserPrincipalName ?? "";
        var ext = upn.IndexOf("#EXT#", StringComparison.OrdinalIgnoreCase);
        if (ext > 0)
        {
            var local = upn[..ext];
            var lastUnderscore = local.LastIndexOf('_');
            if (lastUnderscore >= 0)
                return local[..lastUnderscore] + "@" + local[(lastUnderscore + 1)..];
        }
        return upn;
    }

    public async Task InviteUserAsync(string email, CancellationToken ct = default)
    {
        // Internal tenant members cannot be B2B invited — Graph rejects it.
        // They already have access once added to the enterprise app in the portal.
        var existing = await FindExistingUserAsync(email, ct);
        if (existing is not null)
            throw new InvalidOperationException($"{email} is already a member of this tenant. Assign their role directly in the Azure Portal under Enterprise Applications.");

        await _graph.Invitations.PostAsync(new Invitation
        {
            InvitedUserEmailAddress = email,
            InviteRedirectUrl = _redirectUrl,
            SendInvitationMessage = true,
        }, cancellationToken: ct);
    }

    private async Task<string?> FindExistingUserAsync(string email, CancellationToken ct)
    {
        var escaped = email.Replace("'", "''");
        var result = await _graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter =
                $"mail eq '{escaped}' or userPrincipalName eq '{escaped}'";
            req.QueryParameters.Select = new[] { "id" };
            req.QueryParameters.Top = 1;
        }, ct);

        return result?.Value?.FirstOrDefault()?.Id;
    }
}
