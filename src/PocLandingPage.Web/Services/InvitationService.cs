using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
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
        var users = await _graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter = "userType eq 'Guest'";
            req.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "externalUserState" };
        }, ct);

        var assignments = await _graph.ServicePrincipals[_spId].AppRoleAssignedTo.GetAsync(cancellationToken: ct);
        var sp = await _graph.ServicePrincipals[_spId].GetAsync(cancellationToken: ct);
        var roleMap = sp?.AppRoles?.ToDictionary(r => r.Id!.Value, r => r.Value!) ?? new();

        // A user can have several assignments (incl. the all-zeros "default access"
        // role, which Graph adds automatically and is NOT in roleMap). Pick the first
        // assignment that maps to a real POC role so default-access entries don't mask it.
        var assignmentLookup = (assignments?.Value ?? new List<AppRoleAssignment>())
            .Where(a => a.PrincipalId is not null)
            .GroupBy(a => a.PrincipalId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(a => a.AppRoleId is { } id && roleMap.ContainsKey(id))
                     ?? g.First());

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
                    Email = u.Mail ?? u.UserPrincipalName ?? "",
                    Role = role,
                    InviteStatus = u.ExternalUserState
                };
            })
            // Only show guests who actually have a POC role on this app —
            // hides unrelated tenant guests that were never invited here.
            .Where(g => g.Role is not null)
            .ToList();
    }

    public async Task InviteUserAsync(string email, string role, CancellationToken ct = default)
    {
        if (!Roles.IsValid(role)) throw new ArgumentException($"Invalid role '{role}'", nameof(role));

        // The B2B invitation API is for external guests. An email that already belongs
        // to a member of this tenant (e.g. an @inholland.nl colleague) cannot be invited —
        // Graph rejects it. Detect that up front and assign the role to the existing user
        // directly so admins can grant access to internal users too.
        var existing = await FindExistingUserAsync(email, ct);
        if (existing is not null)
        {
            await AssignRoleAsync(existing, role, ct);
            return;
        }

        var invite = await _graph.Invitations.PostAsync(new Invitation
        {
            InvitedUserEmailAddress = email,
            InviteRedirectUrl = _redirectUrl,
            SendInvitationMessage = true,
        }, cancellationToken: ct);

        if (invite?.InvitedUser?.Id is null)
            throw new InvalidOperationException("Invitation succeeded but invited user id was missing.");

        // A just-created guest object is not yet usable as the principal of an app role
        // assignment — for a few seconds Graph rejects the link with "Links to
        // EntitlementGrant are not supported between specified entities" because the new
        // principal hasn't replicated. Retry with backoff until it succeeds.
        await AssignRoleWithRetryAsync(invite.InvitedUser.Id, role, ct);
    }

    private async Task AssignRoleWithRetryAsync(string userId, string role, CancellationToken ct)
    {
        // Total wait budget ~15s: enough for directory replication of a new guest,
        // short enough that the admin isn't left staring at a hung request.
        var delays = new[] { 1, 2, 3, 4, 5 };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await AssignRoleAsync(userId, role, ct);
                return;
            }
            catch (ODataError ex) when (IsPrincipalNotReadyError(ex) && attempt < delays.Length)
            {
                await Task.Delay(TimeSpan.FromSeconds(delays[attempt]), ct);
            }
        }
    }

    // The "EntitlementGrant" link error means the principal isn't resolvable yet — the
    // only transient condition worth retrying here. Anything else should surface.
    private static bool IsPrincipalNotReadyError(ODataError ex) =>
        ex.Error?.Message?.Contains("EntitlementGrant", StringComparison.OrdinalIgnoreCase) == true
        || ex.Error?.Message?.Contains("does not exist", StringComparison.OrdinalIgnoreCase) == true;

    // Returns the object id of an existing tenant user matching this email
    // (by mail, userPrincipalName, or proxyAddresses), or null if none exists.
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

    private async Task AssignRoleAsync(string userId, string role, CancellationToken ct)
    {
        var roleId = await GetRoleIdAsync(role, ct);

        // Idempotent: a user who already has a role assignment on this app would
        // otherwise trigger Graph's "Permission being assigned already exists on the
        // object" error. Clear any existing assignments first so re-inviting (or
        // re-granting) the same user simply refreshes their role instead of failing.
        await RevokeAccessAsync(userId, ct);

        await _graph.ServicePrincipals[_spId].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(userId),
            ResourceId = Guid.Parse(_spId),
            AppRoleId = roleId,
        }, cancellationToken: ct);
    }

    public async Task UpdateRoleAsync(string userId, string newRole, CancellationToken ct = default)
    {
        if (!Roles.IsValid(newRole)) throw new ArgumentException($"Invalid role '{newRole}'", nameof(newRole));

        await RevokeAccessAsync(userId, ct);

        var roleId = await GetRoleIdAsync(newRole, ct);
        await _graph.ServicePrincipals[_spId].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(userId),
            ResourceId = Guid.Parse(_spId),
            AppRoleId = roleId,
        }, cancellationToken: ct);
    }

    public async Task RevokeAccessAsync(string userId, CancellationToken ct = default)
    {
        var assignments = await _graph.ServicePrincipals[_spId].AppRoleAssignedTo.GetAsync(req =>
        {
            req.QueryParameters.Filter = $"principalId eq {userId}";
        }, ct);

        foreach (var a in assignments?.Value ?? new List<AppRoleAssignment>())
        {
            if (a.Id is null) continue;
            await _graph.ServicePrincipals[_spId].AppRoleAssignedTo[a.Id].DeleteAsync(cancellationToken: ct);
        }
    }

    private async Task<Guid> GetRoleIdAsync(string roleName, CancellationToken ct)
    {
        var sp = await _graph.ServicePrincipals[_spId].GetAsync(cancellationToken: ct);
        var role = sp?.AppRoles?.FirstOrDefault(r => r.Value == roleName)
            ?? throw new InvalidOperationException($"App role '{roleName}' not found on service principal {_spId}.");
        return role.Id!.Value;
    }
}
