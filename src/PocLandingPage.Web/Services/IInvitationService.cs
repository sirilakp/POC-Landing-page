using PocLandingPage.Web.Models;

namespace PocLandingPage.Web.Services;

public interface IInvitationService
{
    Task<IReadOnlyList<GuestUser>> GetGuestsAsync(CancellationToken ct = default);
    Task InviteUserAsync(string email, CancellationToken ct = default);
}
