using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public interface IInvitationService
{
    Task<IReadOnlyList<GuestUser>> GetGuestsAsync(CancellationToken ct = default);
    Task InviteUserAsync(string email, string role, CancellationToken ct = default);
    Task UpdateRoleAsync(string userId, string newRole, CancellationToken ct = default);
    Task RevokeAccessAsync(string userId, CancellationToken ct = default);
}
