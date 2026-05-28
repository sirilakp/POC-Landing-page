using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PocLandingPage.Web.Services;

namespace PocLandingPage.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
[Route("admin/invitations")]
public class InvitationsController : Controller
{
    private readonly IInvitationService _invitations;

    public InvitationsController(IInvitationService invitations) => _invitations = invitations;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _invitations.GetGuestsAsync(ct));
}
