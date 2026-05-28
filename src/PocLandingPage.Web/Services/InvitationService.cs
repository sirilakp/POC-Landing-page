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
        var users = await _graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter = "userType eq 'Guest'";
            req.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "externalUserState" };
        }, ct);

        var assignments = await _graph.ServicePrincipals[_spId].AppRoleAssignedTo.GetAsync(cancellationToken: ct);
        var sp = await _graph.ServicePrincipals[_spId].GetAsync(cancellationToken: ct);
        var roleMap = sp?.AppRoles?.ToDictionary(r => r.Id!.Value, r => r.Value!) ?? new();

        var assignmentLookup = (assignments?.Value ?? new List<AppRoleAssignment>())
            .Where(a => a.PrincipalId is not null)
            .GroupBy(a => a.PrincipalId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

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
            .ToList();
    }

    public async Task InviteUserAsync(string email, string role, CancellationToken ct = default)
    {
        if (!Roles.IsValid(role)) throw new ArgumentException($"Invalid role '{role}'", nameof(role));

        var invite = await _graph.Invitations.PostAsync(new Invitation
        {
            InvitedUserEmailAddress = email,
            InviteRedirectUrl = _redirectUrl,
            SendInvitationMessage = true,
        }, cancellationToken: ct);

        if (invite?.InvitedUser?.Id is null)
            throw new InvalidOperationException("Invitation succeeded but invited user id was missing.");

        var roleId = await GetRoleIdAsync(role, ct);
        await _graph.ServicePrincipals[_spId].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(invite.InvitedUser.Id),
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
